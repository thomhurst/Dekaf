using System.Buffers;
using System.Reflection;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;
using Dekaf.ShareConsumer;
using NSubstitute;

namespace Dekaf.Tests.Unit.ShareConsumer;

public sealed class ShareConsumerRecordPoolingTests
{
    [Test]
    [NotInParallel]
    public async Task ParsePartitionRecords_DeserializerThrows_ReturnsBatchToPool()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var source = new RecordBatch
        {
            BaseOffset = 17,
            Records = [new Record { IsKeyNull = true, Value = "value"u8.ToArray() }]
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
        valueDeserializer.Deserialize(
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<SerializationContext>())
            .Returns(_ => throw new InvalidOperationException("Deserializer failure"));
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
                            LastOffset = 17,
                            DeliveryCount = 1
                        }
                    ]
                },
                1
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
    public async Task ParsePartitionRecords_RecordHeaderDecoratorsShareOneMaterialization()
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
        var method = typeof(KafkaShareConsumer<string, string>).GetMethod(
            "ParsePartitionRecords",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        _ = method.Invoke(consumer,
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
                        LastOffset = 17,
                        DeliveryCount = 1
                    }
                ]
            },
            1
        ]);

        await Assert.That(keyDeserializer.HeaderCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.HeaderCount).IsEqualTo(2);
    }

    [Test]
    [NotInParallel]
    public async Task ParsePartitionRecords_ColdHeaderPreparer_RetainsResultsAndCopiesHeader()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var source = new RecordBatch
        {
            BaseOffset = 17,
            Records =
            [
                new Record
                {
                    IsKeyNull = true,
                    Value = "warm"u8.ToArray()
                },
                new Record
                {
                    OffsetDelta = 1,
                    IsKeyNull = true,
                    Value = "payload"u8.ToArray(),
                    Headers = [new Header("schema-guid", "identity"u8.ToArray())],
                    HeaderCount = 1
                }
            ]
        };
        source.Write(buffer);

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
                    LastOffset = 18,
                    DeliveryCount = 1
                }
            ]
        };
        var consumerType = typeof(KafkaShareConsumer<string, string>);
        var parseMethod = consumerType.GetMethod(
            "ParsePartitionRecordsWithPreparation",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var results = new List<ShareConsumeResult<string, string>>();

        RecordBatch.BeginTrackingPoolReturnsForCurrentThread();
        object firstParse;
        int returnedBatchCount;
        try
        {
            firstParse = parseMethod.Invoke(consumer,
            [
                new TopicInfo { Name = "topic", Partitions = [] },
                partition,
                2,
                results,
                null,
                false,
                null
            ])!;
        }
        finally
        {
            returnedBatchCount = RecordBatch.EndTrackingPoolReturnsForCurrentThread();
        }

        var prepareMethod = consumerType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method =>
                method.Name == "PrepareDeserializerAsync" &&
                method.GetParameters() is [{ ParameterType.Name: "PendingDeserializerPreparation" }, _]);
        var preparation = (ValueTask)prepareMethod.Invoke(
            consumer,
            [firstParse, CancellationToken.None])!;
        await preparation;

        parseMethod.Invoke(consumer,
        [
            new TopicInfo { Name = "topic", Partitions = [] },
            partition,
            2,
            results,
            18L,
            false,
            null
        ]);

        await Assert.That(returnedBatchCount).IsEqualTo(1);
        await Assert.That(valueDeserializer.PrepareCalls).IsEqualTo(1);
        await Assert.That(valueDeserializer.WarmDeserializeCalls).IsEqualTo(1);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Value).IsEqualTo("warm");
        await Assert.That(results[1].Value).IsEqualTo("payload");
    }

    private sealed class ColdHeaderPreparer :
        IDeserializer<string>,
        IAsyncDeserializerPreparer<string>,
        IRecordHeaderAsyncDeserializerPreparer<string>,
        IRecordHeaderRoutingProvider
    {
        private bool _prepared;

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

            if (!_prepared)
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
            if (!headers.TryGetLast("schema-guid", out var header) ||
                !header.Value.Span.SequenceEqual("identity"u8))
            {
                throw new InvalidOperationException("Durable identity header was not available.");
            }

            PrepareCalls++;
            _prepared = true;
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
}
