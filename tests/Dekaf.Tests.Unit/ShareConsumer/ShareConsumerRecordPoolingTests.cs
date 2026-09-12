using System.Buffers;
using System.Reflection;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerRecordPoolingTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RawCapture_PreservesNullEmptyAndNonEmptyBytesAcrossBufferGrowth(bool preparationPath)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var largeValue = new byte[16_384];
        Array.Fill(largeValue, (byte)'x');
        using var source = new RecordBatch
        {
            BaseOffset = 17,
            LastOffsetDelta = 2,
            Records =
            [
                new Record { OffsetDelta = 0, IsKeyNull = true, IsValueNull = true },
                new Record { OffsetDelta = 1, Key = ReadOnlyMemory<byte>.Empty, Value = ReadOnlyMemory<byte>.Empty },
                new Record { OffsetDelta = 2, Key = "key"u8.ToArray(), Value = largeValue }
            ]
        };
        source.Write(buffer);
        var options = new ShareConsumerOptions { BootstrapServers = ["localhost:9092"], GroupId = "raw-share" };
        var pool = Substitute.For<IConnectionPool>();
        await using var metadata = new MetadataManager(pool, options.BootstrapServers);
        await using var consumer = new KafkaShareConsumer<string, string>(options, Serializers.String, Serializers.String, pool, metadata);
        var raw = (IRawShareRecordAccessor)consumer;
        raw.EnableRawRecordTracking();
        var partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0, CurrentLeader = new ShareFetchLeaderIdAndEpoch(), RecordBytes = buffer.WrittenMemory,
            AcquiredRecords = [new ShareFetchAcquiredRecords { FirstOffset = 17, LastOffset = 19, DeliveryCount = 1 }]
        };
        var topic = new TopicInfo { Name = "topic", Partitions = [] };
        if (preparationPath)
        {
            var state = new KafkaShareConsumer<string, string>.DeserializerPreparationParserState();
            try
            {
                var pending = consumer.ParsePartitionRecordsWithPreparation(topic, partition, 3, [], ref state, false, null);
                await Assert.That(pending).IsNull();
            }
            finally
            {
                state.DisposeCurrentBatch();
            }
        }
        else
        {
            var method = typeof(KafkaShareConsumer<string, string>).GetMethod("ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _ = method.Invoke(consumer, [topic, partition, 3]);
        }
        await Assert.That(raw.TryGetRawRecord(new TopicPartitionOffset("topic", 0, 17), out var nullKey, out var nullValue)).IsTrue();
        await Assert.That(nullKey).IsNull();
        await Assert.That(nullValue).IsNull();
        await Assert.That(raw.TryGetRawRecord(new TopicPartitionOffset("topic", 0, 18), out var emptyKey, out var emptyValue)).IsTrue();
        await Assert.That(emptyKey).IsNotNull();
        await Assert.That(emptyKey!.Length).IsEqualTo(0);
        await Assert.That(emptyValue).IsNotNull();
        await Assert.That(emptyValue!.Length).IsEqualTo(0);
        await Assert.That(raw.TryGetRawRecord(new TopicPartitionOffset("topic", 0, 19), out var key, out var value)).IsTrue();
        await Assert.That(key!.AsSpan().SequenceEqual("key"u8)).IsTrue();
        await Assert.That(value!.AsSpan().SequenceEqual(largeValue)).IsTrue();
    }

    [Test]
    public async Task NextPoll_ReusedOwnerRejectsStaleRenewal()
    {
        var consumer = CreateBorrowedConsumer(out var metadata);
        await using var metadataScope = metadata;
        await using var consumerScope = consumer;
        ShareConsumeResult<string, ReadOnlyMemory<byte>> original;
        using (consumer.BeginRecordBatchScope())
            original = ParseBorrowedRecords(consumer, "first")[0];
        var owner = original.BatchOwner;
        using (consumer.BeginRecordBatchScope())
        {
            var current = ParseBorrowedRecords(consumer, "other")[0];
            await Assert.That(current.BatchOwner).IsSameReferenceAs(owner);
            await Assert.That(original.Topic).IsEqualTo("topic");
            await Assert.That(original.DeliveryCount).IsEqualTo(1);
            await Assert.That(() => consumer.Acknowledge(original, AcknowledgeType.Renew))
                .Throws<InvalidOperationException>();
            await Assert.That(original.AcknowledgeType).IsEqualTo(AcknowledgeType.Accept);
            await Assert.That(System.Text.Encoding.UTF8.GetString(current.Value.Span)).IsEqualTo("other");
            await Assert.That(() => consumer.Acknowledge(current, (AcknowledgeType)255))
                .Throws<ArgumentOutOfRangeException>();
            await Assert.That(current.BatchOwner).IsSameReferenceAs(owner);
            await Assert.That(current.AcknowledgeType).IsEqualTo(AcknowledgeType.Accept);
            consumer.Acknowledge(current, AcknowledgeType.Renew);
        }
    }

    [Test]
    [NotInParallel]
    public async Task ParsePartitionRecords_TwoBatches_PreservesBorrowedValuesAndHeaders()
    {
        var consumer = CreateBorrowedConsumer(out var metadata);
        await using var metadataScope = metadata;
        await using var consumerScope = consumer;
        using var recordScope = consumer.BeginRecordBatchScope();
        var records = ParseBorrowedRecords(consumer, "first", "other");

        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(System.Text.Encoding.UTF8.GetString(records[0].Value.Span)).IsEqualTo("first");
        await Assert.That(System.Text.Encoding.UTF8.GetString(records[0].Headers[0].Value.Span)).IsEqualTo("first");
        await Assert.That(System.Text.Encoding.UTF8.GetString(records[1].Value.Span)).IsEqualTo("other");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompletedParsing_ReleasesMaterializedRecordsButRetainsPayload(bool prepared)
    {
        var consumer = CreateBorrowedConsumer(out var metadata);
        await using var metadataScope = metadata;
        await using var consumerScope = consumer;
        using var recordScope = consumer.BeginRecordBatchScope();
        var records = ParseBorrowedRecords(consumer, prepared, "first", "other");

        for (var index = 0; index < records.Count; index++)
        {
            var owner = records[index].BatchOwner!;
            var batch = typeof(ShareRecordBatchOwner)
                .GetField("_batchStorage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner);
            await Assert.That(batch).IsNull();
            var expected = index == 0 ? "first" : "other";
            await Assert.That(System.Text.Encoding.UTF8.GetString(records[index].Value.Span)).IsEqualTo(expected);
            await Assert.That(System.Text.Encoding.UTF8.GetString(records[index].Headers[0].Value.Span)).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task Dispose_ReturnsCachedPayloadsAfterResubscription()
    {
        var consumer = CreateBorrowedConsumer(out var metadata);
        await using var metadataScope = metadata;
        await using var consumerScope = consumer;
        using (consumer.BeginRecordBatchScope())
            _ = ParseBorrowedRecords(consumer, "first");
        using (consumer.BeginRecordBatchScope()) { }
        var pool = (ShareRecordBufferPool)consumer.GetType()
            .GetField("_recordBuffers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
        await Assert.That(pool.RetainedBytes).IsGreaterThan(0);
        consumer.Subscribe("topic");
        using (consumer.BeginRecordBatchScope())
            _ = ParseBorrowedRecords(consumer, "other");
        await consumer.DisposeAsync();
        await Assert.That(pool.RetainedBytes).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [NotInParallel]
    public async Task RenewedReplay_TerminalAcknowledgement_KeepsPayloadUntilNextPoll(bool exhaustGeneration)
    {
        var consumer = CreateBorrowedConsumer(out var metadata);
        await using var metadataScope = metadata;
        await using var consumerScope = consumer;
        ShareConsumeResult<string, ReadOnlyMemory<byte>> record;
        using (consumer.BeginRecordBatchScope())
        {
            record = ParseBorrowedRecords(consumer, "first")[0];
        }
        // Consuming a single record disposes its iterator before acknowledging it.
        consumer.Acknowledge(record, AcknowledgeType.Renew);
        ApplyAcknowledgement(consumer, AcknowledgeType.Renew);

        var getActive = consumer.GetType().GetMethod("GetActiveRenewedRecords",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var round = 0; round < 4; round++)
        {
            if (exhaustGeneration && round == 1)
            {
                var owner = record.BatchOwner!;
                typeof(ShareRecordBatchOwner).GetProperty("Generation",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(owner, ShareRecordBatchOwner.MaximumGeneration);
                record.AttachBatchOwner(owner);
            }
            using var poll = consumer.BeginRecordBatchScope();
            await Assert.That(() => record.BatchOwner).Throws<InvalidOperationException>();
            var replay = (List<ShareConsumeResult<string, ReadOnlyMemory<byte>>>)getActive.Invoke(consumer,
                [new HashSet<TopicPartition> { new("topic", 0) }, 10])!;
            await Assert.That(replay[0]).IsSameReferenceAs(record);
            await Assert.That(record.BatchOwner).IsNotNull();
            await Assert.That(System.Text.Encoding.UTF8.GetString(record.Value.Span)).IsEqualTo("first");
            consumer.Acknowledge(record, AcknowledgeType.Renew);
            ApplyAcknowledgement(consumer, AcknowledgeType.Renew);
        }

        var replayScope = consumer.BeginRecordBatchScope();
        var buffers = (ShareRecordBufferPool)consumer.GetType()
            .GetField("_recordBuffers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
        int returnedBeforeScopeEnd;
        int returnedAtScopeEnd;
        string payload;
        try
        {
            var replay = (List<ShareConsumeResult<string, ReadOnlyMemory<byte>>>)getActive.Invoke(consumer,
                [new HashSet<TopicPartition> { new("topic", 0) }, 10])!;
            await Assert.That(replay[0]).IsSameReferenceAs(record);
            try
            {
                ApplyAcknowledgement(consumer, AcknowledgeType.Accept);
                payload = System.Text.Encoding.UTF8.GetString(replay[0].Value.Span);
            }
            finally
            {
                returnedBeforeScopeEnd = buffers.RetainedBytes;
            }
        }
        finally
        {
            replayScope.Dispose();
            using var nextPoll = consumer.BeginRecordBatchScope();
            returnedAtScopeEnd = buffers.RetainedBytes;
        }

        await Assert.That(payload).IsEqualTo("first");
        await Assert.That(returnedBeforeScopeEnd).IsEqualTo(0);
        await Assert.That(returnedAtScopeEnd).IsGreaterThan(0);
        await Assert.That(record.Topic).IsEqualTo("topic");
    }

    [Test]
    [NotInParallel]
    public async Task RenewAfterNextPollStarts_ThrowsWithoutQueueingAcknowledgement()
    {
        var consumer = CreateBorrowedConsumer(out var metadata);
        await using var metadataScope = metadata;
        await using var consumerScope = consumer;
        ShareConsumeResult<string, ReadOnlyMemory<byte>> record;
        using (consumer.BeginRecordBatchScope())
            record = ParseBorrowedRecords(consumer, "first")[0];
        using var nextPoll = consumer.BeginRecordBatchScope();

        await Assert.That(() => consumer.Acknowledge(record, AcknowledgeType.Renew))
            .Throws<InvalidOperationException>();
        var tracker = (AcknowledgementTracker)consumer.GetType()
            .GetField("_ackTracker", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
        await Assert.That(tracker.HasPending).IsFalse();
    }

    private static void ApplyAcknowledgement(
        KafkaShareConsumer<string, ReadOnlyMemory<byte>> consumer, AcknowledgeType type)
    {
        var method = consumer.GetType().GetMethod("ApplySuccessfulAcknowledgements",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements = new()
        {
            [new TopicPartition("topic", 0)] = [new AcknowledgementBatchData(0, 0, [(byte)type])]
        };
        method.Invoke(consumer, [acknowledgements, 0L]);
    }

    private static KafkaShareConsumer<string, ReadOnlyMemory<byte>> CreateBorrowedConsumer(
        out MetadataManager metadata, IDeserializer<ReadOnlyMemory<byte>>? valueDeserializer = null)
    {
        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"], GroupId = "share-borrowed-records",
            AcknowledgementMode = ShareAcknowledgementMode.Explicit
        };
        var pool = Substitute.For<IConnectionPool>();
        metadata = new MetadataManager(pool, options.BootstrapServers);
        return new KafkaShareConsumer<string, ReadOnlyMemory<byte>>(
            options, Serializers.String, valueDeserializer ?? Serializers.RawBytes, pool, metadata);
    }

    private static List<ShareConsumeResult<string, ReadOnlyMemory<byte>>> ParseBorrowedRecords(
        KafkaShareConsumer<string, ReadOnlyMemory<byte>> consumer, params string[] values)
        => ParseBorrowedRecords(consumer, false, values);

    private static List<ShareConsumeResult<string, ReadOnlyMemory<byte>>> ParseBorrowedRecords(
        KafkaShareConsumer<string, ReadOnlyMemory<byte>> consumer, bool prepared, params string[] values)
    {
        var buffer = new ArrayBufferWriter<byte>();
        for (var offset = 0; offset < values.Length; offset++)
        {
            var payload = System.Text.Encoding.UTF8.GetBytes(values[offset]);
            using var batch = new RecordBatch
            {
                BaseOffset = offset,
                Records = [new Record
                {
                    IsKeyNull = true,
                    Value = payload,
                    Headers = [new Header("test", payload)],
                    HeaderCount = 1
                }]
            };
            batch.Write(buffer);
        }

        var topic = new TopicInfo { Name = "topic", Partitions = [] };
        var partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            RecordBytes = buffer.WrittenMemory,
            AcquiredRecords = [new ShareFetchAcquiredRecords
            {
                FirstOffset = 0, LastOffset = values.Length - 1, DeliveryCount = 1
            }]
        };
        if (prepared)
        {
            var records = new List<ShareConsumeResult<string, ReadOnlyMemory<byte>>>();
            var state = new KafkaShareConsumer<string, ReadOnlyMemory<byte>>.DeserializerPreparationParserState();
            try
            {
                if (consumer.ParsePartitionRecordsWithPreparation(topic, partition, values.Length,
                    records, ref state, false, null) is not null)
                    throw new InvalidOperationException("The raw deserializer must not require preparation.");
                return records;
            }
            finally
            {
                state.DisposeCurrentBatch();
            }
        }
        var parse = typeof(KafkaShareConsumer<string, ReadOnlyMemory<byte>>).GetMethod(
            "ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (List<ShareConsumeResult<string, ReadOnlyMemory<byte>>>)parse.Invoke(consumer,
            [topic, partition, values.Length])!;
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [NotInParallel]
    public async Task ParsePartitionRecords_DeserializerThrows_ReturnsBatchToPool(int successfulRecords)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var source = new RecordBatch
        {
            BaseOffset = 17,
            Records =
            [
                new Record { IsKeyNull = true, Value = "first"u8.ToArray() },
                new Record { OffsetDelta = 1, IsKeyNull = true, Value = "second"u8.ToArray() }
            ]
        };
        source.Write(buffer);

        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-pooling-test"
        };
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        var valueDeserializer = Substitute.For<IDeserializer<string>>();
        var calls = 0;
        valueDeserializer.Deserialize(
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<SerializationContext>())
            .Returns(_ => calls++ < successfulRecords ? "value"
                : throw new InvalidOperationException("Deserializer failure"));
        await using var consumer = new KafkaShareConsumer<string, string>(
            options,
            Serializers.String,
            valueDeserializer,
            pool,
            metadataManager);

        var method = typeof(KafkaShareConsumer<string, string>).GetMethod(
            "ParsePartitionRecords",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        TargetInvocationException? thrown = null;
        RecordBatch.BeginTrackingPoolReturnsForCurrentThread();
        int returnedBatchCount;
        try
        {
            method.Invoke(consumer,
            [
                new TopicInfo { Name = "topic", Partitions = [] },
                new ShareFetchResponsePartition
                {
                    PartitionIndex = 0,
                    CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
                    RecordBytes = buffer.WrittenMemory,
                    AcquiredRecords =
                    [
                        new ShareFetchAcquiredRecords
                        {
                            FirstOffset = 17,
                            LastOffset = 18,
                            DeliveryCount = 1
                        }
                    ]
                },
                2
            ]);
        }
        catch (TargetInvocationException exception)
        {
            thrown = exception;
        }
        finally
        {
            returnedBatchCount = RecordBatch.EndTrackingPoolReturnsForCurrentThread();
        }

        await Assert.That(thrown?.InnerException).IsTypeOf<InvalidOperationException>();
        await Assert.That(returnedBatchCount).IsEqualTo(1);
    }

    [Test]
    [NotInParallel]
    public async Task ParsePartitionRecords_LaterBatchFails_ReleasesUndisclosedOwnersOnly()
    {
        var valueDeserializer = Substitute.For<IDeserializer<ReadOnlyMemory<byte>>>();
        valueDeserializer.Deserialize(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<SerializationContext>())
            .Returns(call => call.ArgAt<ReadOnlyMemory<byte>>(0).Span.SequenceEqual("failure"u8)
                ? throw new InvalidOperationException("Deserializer failure")
                : call.ArgAt<ReadOnlyMemory<byte>>(0));
        var consumer = CreateBorrowedConsumer(out var metadata, valueDeserializer);
        await using var metadataScope = metadata;
        await using var consumerScope = consumer;
        var delivered = ParseBorrowedRecords(consumer, "retained")[0];

        TargetInvocationException? thrown = null;
        int failedCallReturns;
        RecordBatch.BeginTrackingPoolReturnsForCurrentThread();
        try
        {
            ParseBorrowedRecords(consumer, "first", "failure");
        }
        catch (TargetInvocationException exception)
        {
            thrown = exception;
        }
        finally
        {
            failedCallReturns = RecordBatch.EndTrackingPoolReturnsForCurrentThread();
        }

        await Assert.That(thrown?.InnerException).IsTypeOf<InvalidOperationException>();
        await Assert.That(failedCallReturns).IsEqualTo(2);
        await Assert.That(System.Text.Encoding.UTF8.GetString(delivered.Value.Span)).IsEqualTo("retained");
        await Assert.That(System.Text.Encoding.UTF8.GetString(delivered.Headers[0].Value.Span)).IsEqualTo("retained");

        var buffers = (ShareRecordBufferPool)consumer.GetType()
            .GetField("_recordBuffers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(consumer)!;
        var cachedBeforeNextPoll = buffers.RetainedBytes;
        using (consumer.BeginRecordBatchScope()) { }
        await Assert.That(buffers.RetainedBytes).IsGreaterThan(cachedBeforeNextPoll);
    }

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ParsePartitionRecords_RecordHeaderDecoratorsShareOneMaterialization(bool borrowed)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var source = new RecordBatch
        {
            BaseOffset = 17,
            Records =
            [
                new Record
                {
                    Key = "key"u8.ToArray(),
                    Value = "value"u8.ToArray(),
                    Headers = [new Header("trace-id", "abc"u8.ToArray())],
                    HeaderCount = 1
                }
            ]
        };
        source.Write(buffer);

        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-header-materialization-test"
        };
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        var keyDeserializer = new HeaderMutatingStringDeserializer(addMarker: true);
        var valueDeserializer = new HeaderMutatingStringDeserializer(addMarker: false);
        await using var consumer = new KafkaShareConsumer<string, string>(
            options,
            keyDeserializer,
            valueDeserializer,
            pool,
            metadataManager);
        var partition = CreatePartition(buffer.WrittenMemory);
        if (borrowed)
        {
            var position = 0;
            using var batch = await consumer.ParseRecordBatchAsync(
                new TopicPartition("topic", 0), ReadNextBatch(buffer.WrittenMemory, ref position),
                partition.AcquiredRecords, 1, default);
            var records = batch.GetEnumerator();
            await Assert.That(records.MoveNext()).IsTrue();
            await Assert.That(records.Current.Headers.Count).IsEqualTo(1);
        }
        else
        {
            var method = typeof(KafkaShareConsumer<string, string>).GetMethod(
                "ParsePartitionRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
            _ = method.Invoke(consumer, [new TopicInfo { Name = "topic", Partitions = [] }, partition, 1]);
        }

        await Assert.That(keyDeserializer.HeaderCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.HeaderCount).IsEqualTo(2);
    }

    [Test]
    [NotInParallel]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ParsePartitionRecords_ReentrantConsumerPreservesOuterRecordContext(bool outerPrepared, bool nestedPrepared)
    {
        var nestedBuffer = new ArrayBufferWriter<byte>();
        using var nestedBatch = new RecordBatch
        {
            BaseOffset = 17,
            Records =
            [
                new Record
                {
                    Key = "nested-key"u8.ToArray(),
                    Value = "nested-value"u8.ToArray(),
                    Headers = [new Header("record-id", "nested"u8.ToArray())],
                    HeaderCount = 1
                }
            ]
        };
        nestedBatch.Write(nestedBuffer);

        var outerBuffer = new ArrayBufferWriter<byte>();
        using var outerBatch = new RecordBatch
        {
            BaseOffset = 17,
            Records =
            [
                new Record
                {
                    Key = "outer-key"u8.ToArray(),
                    Value = "outer-value"u8.ToArray(),
                    Headers = [new Header("record-id", "outer"u8.ToArray())],
                    HeaderCount = 1
                }
            ]
        };
        outerBatch.Write(outerBuffer);

        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-header-reentrancy-test"
        };
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        var nestedValueDeserializer = new HeaderValueCapturingStringDeserializer();
        await using var nestedConsumer = new KafkaShareConsumer<string, string>(
            options,
            Serializers.String,
            nestedValueDeserializer,
            pool,
            metadataManager);
        var method = typeof(KafkaShareConsumer<string, string>).GetMethod(
            "ParsePartitionRecords",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var outerTopic = new TopicInfo { Name = "outer-topic", Partitions = [] };
        var nestedTopic = new TopicInfo { Name = "nested-topic", Partitions = [] };
        var nestedPartition = CreatePartition(nestedBuffer.WrittenMemory);
        var outerValueDeserializer = new HeaderValueCapturingStringDeserializer();
        var outerKeyDeserializer = new CallbackStringDeserializer(() =>
            Parse(nestedConsumer, nestedTopic, nestedPartition, nestedPrepared));
        await using var outerConsumer = new KafkaShareConsumer<string, string>(
            options,
            outerKeyDeserializer,
            outerValueDeserializer,
            pool,
            metadataManager);

        Parse(outerConsumer, outerTopic, CreatePartition(outerBuffer.WrittenMemory), outerPrepared);

        await Assert.That(nestedValueDeserializer.HeaderValue).IsEqualTo("nested");
        await Assert.That(outerValueDeserializer.HeaderValue).IsEqualTo("outer");
        await Assert.That(nestedValueDeserializer.Topic).IsEqualTo("nested-topic");
        await Assert.That(outerValueDeserializer.Topic).IsEqualTo("outer-topic");

        void Parse(KafkaShareConsumer<string, string> consumer, TopicInfo topic,
            ShareFetchResponsePartition partition, bool prepared)
        {
            if (!prepared)
            {
                _ = method.Invoke(consumer, [topic, partition, 1]);
                return;
            }
            var state = new KafkaShareConsumer<string, string>.DeserializerPreparationParserState();
            try
            {
                if (consumer.ParsePartitionRecordsWithPreparation(topic, partition, 1, [], ref state, false, null) is not null)
                    throw new InvalidOperationException("The test deserializers must complete synchronously.");
            }
            finally
            {
                state.DisposeCurrentBatch();
            }
        }
    }

    [Test]
    [NotInParallel]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ParsePartitionRecords_ColdHeaderPreparers_ParseEachBatchOnce(bool borrowed)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var warmBatch = new RecordBatch
        {
            BaseOffset = 17,
            Records =
            [
                new Record
                {
                    IsKeyNull = true,
                    Value = "warm"u8.ToArray()
                }
            ]
        };
        warmBatch.Write(buffer);

        using var coldBatch = new RecordBatch
        {
            BaseOffset = 18,
            Records =
            [
                new Record
                {
                    IsKeyNull = true,
                    Value = "first"u8.ToArray(),
                    Headers = [new Header("schema-guid", "identity-a"u8.ToArray())],
                    HeaderCount = 1
                },
                new Record
                {
                    OffsetDelta = 1,
                    IsKeyNull = true,
                    Value = "second"u8.ToArray(),
                    Headers = [new Header("schema-guid", "identity-b"u8.ToArray())],
                    HeaderCount = 1
                }
            ]
        };
        coldBatch.Write(buffer);

        var options = new ShareConsumerOptions
        {
            BootstrapServers = ["localhost:9092"],
            GroupId = "share-header-preparer-test"
        };
        var pool = Substitute.For<IConnectionPool>();
        await using var metadataManager = new MetadataManager(pool, options.BootstrapServers);
        var valueDeserializer = new ColdHeaderPreparer();
        await using var consumer = new KafkaShareConsumer<string, string>(
            options,
            Serializers.String,
            valueDeserializer,
            pool,
            metadataManager);
        var partition = new ShareFetchResponsePartition
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            RecordBytes = buffer.WrittenMemory,
            AcquiredRecords =
            [
                new ShareFetchAcquiredRecords
                {
                    FirstOffset = 17,
                    LastOffset = 19,
                    DeliveryCount = 1
                }
            ]
        };
        var results = new List<ShareConsumeResult<string, string>>();
        var topicInfo = new TopicInfo { Name = "topic", Partitions = [] };
        var parserState = new KafkaShareConsumer<string, string>
            .DeserializerPreparationParserState();

        var recordScope = consumer.BeginRecordBatchScope();
        RecordBatch.BeginTrackingPoolReturnsForCurrentThread();
        int returnedBatchCount;
        try
        {
            if (borrowed)
            {
                var position = 0;
                while (position < buffer.WrittenCount)
                {
                    using var batch = await consumer.ParseRecordBatchAsync(
                        new TopicPartition("topic", 0), ReadNextBatch(buffer.WrittenMemory, ref position),
                        partition.AcquiredRecords, 3, default);
                    foreach (var record in batch)
                    {
                        results.Add(new ShareConsumeResult<string, string>
                        {
                            Topic = record.Topic,
                            Partition = record.Partition,
                            Offset = record.Offset,
                            Value = record.Value,
                            DeliveryCount = record.DeliveryCount
                        });
                    }
                }
            }
            else
            {
                var firstPreparation = consumer.ParsePartitionRecordsWithPreparation(
                    topicInfo, partition, 3, results, ref parserState, false, null);
                await consumer.PrepareDeserializerAsync(firstPreparation!, CancellationToken.None);

                var secondPreparation = consumer.ParsePartitionRecordsWithPreparation(
                    topicInfo, partition, 3, results, ref parserState, false, null);
                await consumer.PrepareDeserializerAsync(secondPreparation!, CancellationToken.None);

                var finalPreparation = consumer.ParsePartitionRecordsWithPreparation(
                    topicInfo, partition, 3, results, ref parserState, false, null);
                await Assert.That(finalPreparation).IsNull();
            }
        }
        finally
        {
            parserState.DisposeCurrentBatch();
            recordScope.Dispose();
            using var nextPoll = consumer.BeginRecordBatchScope();
            returnedBatchCount = RecordBatch.EndTrackingPoolReturnsForCurrentThread();
        }

        await Assert.That(returnedBatchCount).IsEqualTo(2);
        await Assert.That(valueDeserializer.PrepareCalls).IsEqualTo(2);
        await Assert.That(valueDeserializer.WarmDeserializeCalls).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Value).IsEqualTo("warm");
        await Assert.That(results[1].Value).IsEqualTo("first");
        await Assert.That(results[2].Value).IsEqualTo("second");
    }

    private static RecordBatch ReadNextBatch(ReadOnlyMemory<byte> bytes, ref int position)
    {
        var reader = new KafkaProtocolReader(bytes[position..]);
        var batch = RecordBatch.Read(ref reader);
        position += (int)reader.Consumed;
        return batch;
    }

    private sealed class ColdHeaderPreparer :
        IDeserializer<string>,
        IAsyncDeserializerPreparer<string>,
        IRecordHeaderAsyncDeserializerPreparer<string>,
        IRecordHeaderRoutingProvider
    {
        private bool _firstPrepared;
        private bool _secondPrepared;

        internal int PrepareCalls { get; private set; }
        internal int WarmDeserializeCalls { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) =>
            throw new InvalidOperationException("The header-aware path must be used.");

        public bool TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            out string value) =>
            throw new InvalidOperationException("The header-aware path must be used.");

        public ValueTask PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The header-aware path must be used.");

        bool IRecordHeaderAsyncDeserializerPreparer<string>.TryDeserialize(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            in RecordHeaderRoutingLookup headers,
            out string value)
        {
            if (data.Span.SequenceEqual("warm"u8))
            {
                WarmDeserializeCalls++;
                value = "warm";
                return true;
            }

            var prepared = data.Span.SequenceEqual("first"u8)
                ? _firstPrepared
                : _secondPrepared;
            if (!prepared)
            {
                value = string.Empty;
                return false;
            }

            value = System.Text.Encoding.UTF8.GetString(data.Span);
            return true;
        }

        ValueTask IRecordHeaderAsyncDeserializerPreparer<string>.PrepareAsync(
            ReadOnlyMemory<byte> data,
            SerializationContext context,
            RecordHeaderRoutingLookup headers,
            CancellationToken cancellationToken)
        {
            if (!headers.TryGetLast("schema-guid", out var header))
            {
                throw new InvalidOperationException("Durable identity header was not available.");
            }

            if (header.Value.Span.SequenceEqual("identity-a"u8))
                _firstPrepared = true;
            else if (header.Value.Span.SequenceEqual("identity-b"u8))
                _secondPrepared = true;
            else
                throw new InvalidOperationException("Unexpected identity header.");

            PrepareCalls++;
            return ValueTask.CompletedTask;
        }

        void IRecordHeaderRoutingProvider.CollectHeaderNames(List<string> names) =>
            names.Add("schema-guid");
    }

    private sealed class HeaderMutatingStringDeserializer(bool addMarker) :
        IDeserializer<string>,
        IRecordHeaderDeserializer
    {
        public bool ConsumesRecordHeaders => true;

        internal int HeaderCount { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            HeaderCount = context.Headers?.Count ?? 0;
            if (addMarker)
                context.Headers!.Add("key-visited", Array.Empty<byte>());
            return System.Text.Encoding.UTF8.GetString(data.Span);
        }
    }

    private static ShareFetchResponsePartition CreatePartition(ReadOnlyMemory<byte> recordBytes) =>
        new()
        {
            PartitionIndex = 0,
            CurrentLeader = new ShareFetchLeaderIdAndEpoch(),
            RecordBytes = recordBytes,
            AcquiredRecords =
            [
                new ShareFetchAcquiredRecords
                {
                    FirstOffset = 17,
                    LastOffset = 17,
                    DeliveryCount = 1
                }
            ]
        };

    private sealed class HeaderValueCapturingStringDeserializer :
        IDeserializer<string>,
        IRecordHeaderDeserializer
    {
        public bool ConsumesRecordHeaders => true;

        internal string? HeaderValue { get; private set; }
        internal string? Topic { get; private set; }

        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Topic = context.Topic;
            HeaderValue = context.Headers?[0].GetValueAsString();
            return System.Text.Encoding.UTF8.GetString(data.Span);
        }
    }

    private sealed class CallbackStringDeserializer(Action callback) : IDeserializer<string>
    {
        public string Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            callback();
            return System.Text.Encoding.UTF8.GetString(data.Span);
        }
    }
}
