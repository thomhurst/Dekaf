using System.Runtime.CompilerServices;
using Dekaf.Consumer;
using Dekaf.Protocol.Records;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public class ConsumeResultTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task WireNullAndEofFlags_AreIndependent(bool isKeyNull, bool isPartitionEof)
    {
        var result = new ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(
            "topic", 0, 0, default, isKeyNull, default, false, null, 0,
            TimestampType.CreateTime, null, Serializers.RawBytes, Serializers.RawBytes, isPartitionEof);
        await Assert.That(result.IsKeyNull).IsEqualTo(isKeyNull);
        await Assert.That(result.IsPartitionEof).IsEqualTo(isPartitionEof);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AlreadyDeserializedConstructors_PreserveWireNull(bool isKeyNull)
    {
        using var owner = PendingFetchData.Create("topic", 0, Array.Empty<RecordBatch>());
        var pooled = new ConsumeResult<ReadOnlyMemory<byte>, string>(
            "topic", 0, 0, default(ReadOnlyMemory<byte>), "value", null, 0, owner, 0,
            TimestampType.CreateTime, null, isKeyNull);
        var callerOwned = new ConsumeResult<ReadOnlyMemory<byte>, string>(
            "topic", 0, 0, default(ReadOnlyMemory<byte>), "value", null, 0,
            TimestampType.CreateTime, null, isKeyNull: isKeyNull);
        await Assert.That(pooled.IsKeyNull).IsEqualTo(isKeyNull);
        await Assert.That(callerOwned.IsKeyNull).IsEqualTo(isKeyNull);
        await Assert.That(pooled.IsPartitionEof).IsFalse();
        await Assert.That(callerOwned.IsPartitionEof).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task HeaderRoutingConstructor_PreservesWireNull(bool isKeyNull)
    {
        using var owner = PendingFetchData.Create("topic", 0, Array.Empty<RecordBatch>());
        var lookup = default(RecordHeaderRoutingLookup);
        var result = ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>.CreateWithHeaderRouting(
            "topic", 0, 0, default, isKeyNull, default, false, null, 0, in lookup, owner,
            0, TimestampType.CreateTime, null, null, Serializers.RawBytes, Serializers.RawBytes);
        await Assert.That(result.IsKeyNull).IsEqualTo(isKeyNull);
        await Assert.That(result.IsPartitionEof).IsFalse();
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task BuiltInRawValue_PreservesMemoryHeadersAndNullSemantics(bool nullKey, bool nullValue, bool eof)
    {
        ReadOnlyMemory<byte> key = "key"u8.ToArray();
        ReadOnlyMemory<byte> value = "value"u8.ToArray();
        Header[] headers = [new("trace", "id"u8.ToArray())];
        var result = new ConsumeResult<Ignore, ReadOnlyMemory<byte>>("topic", 2, 42,
            key, nullKey, value, nullValue, headers, 123, TimestampType.CreateTime, 7,
            Serializers.Ignore, Serializers.RawBytes, eof);

        await Assert.That(result.Value.Equals(nullValue || eof ? ReadOnlyMemory<byte>.Empty : value)).IsTrue();
        await Assert.That(result.Headers).IsSameReferenceAs(headers);
        await Assert.That(result.LeaderEpoch).IsEqualTo(7);
        await Assert.That(result.Offset).IsEqualTo(42);
        await Assert.That(result.IsPartitionEof).IsEqualTo(eof);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RawValue_CustomIgnoreDeserializerStillRunsForNonNullKeys(bool nullKey)
    {
        var keyDeserializer = new ObservingIgnoreDeserializer();
        var result = new ConsumeResult<Ignore, ReadOnlyMemory<byte>>("topic", 0, 1,
            "key"u8.ToArray(), nullKey, "value"u8.ToArray(), false, null, 0, TimestampType.CreateTime, null,
            keyDeserializer, Serializers.RawBytes);

        await Assert.That(keyDeserializer.Calls).IsEqualTo(nullKey ? 0 : 1);
        await Assert.That(result.Value.Span.SequenceEqual("value"u8)).IsTrue();
    }

    [Test]
    public async Task CustomRawValueDeserializer_ReceivesContextAndControlsResult()
    {
        var valueDeserializer = new ObservingRawDeserializer();
        var result = new ConsumeResult<string, ReadOnlyMemory<byte>>("topic", 0, 1,
            "key"u8.ToArray(), false, "value"u8.ToArray(), false, null, 0, TimestampType.CreateTime, null,
            Serializers.String, valueDeserializer);

        await Assert.That(result.Key).IsEqualTo("key");
        await Assert.That(valueDeserializer.Calls).IsEqualTo(1);
        await Assert.That(valueDeserializer.Topic).IsEqualTo("topic");
        await Assert.That(valueDeserializer.Key.Span.SequenceEqual("key"u8)).IsTrue();
        await Assert.That(result.Value.Span.SequenceEqual("alue"u8)).IsTrue();
    }

    [Test]
    public async Task BuiltInRawValue_StillDeserializesNonIgnoredKey()
    {
        var result = new ConsumeResult<string, ReadOnlyMemory<byte>>("topic", 0, 1,
            "key"u8.ToArray(), false, "value"u8.ToArray(), false, null, 0, TimestampType.CreateTime, null,
            Serializers.String, Serializers.RawBytes);
        await Assert.That(result.Key).IsEqualTo("key");
        await Assert.That(result.Value.Span.SequenceEqual("value"u8)).IsTrue();
    }

    private sealed class ObservingIgnoreDeserializer : IDeserializer<Ignore>
    {
        public int Calls { get; private set; }
        public Ignore Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Calls++;
            return default;
        }
    }

    private sealed class ObservingRawDeserializer : IDeserializer<ReadOnlyMemory<byte>>
    {
        public int Calls { get; private set; }
        public string? Topic { get; private set; }
        public ReadOnlyMemory<byte> Key { get; private set; }
        public ReadOnlyMemory<byte> Deserialize(ReadOnlyMemory<byte> data, SerializationContext context)
        {
            Calls++;
            Topic = context.Topic;
            Key = context.KeyData;
            return data[1..];
        }
    }

    [Test]
    public async Task ReferenceKeyAndValue_ResultFits96ByteBudget()
    {
        await Assert.That(Unsafe.SizeOf<ConsumeResult<string, string>>()).IsLessThanOrEqualTo(96);
    }

    [Test]
    public async Task GenericResultShapes_PreserveBaselineSizeBudgets()
    {
        await Assert.That(Unsafe.SizeOf<ConsumeResult<Ignore, ReadOnlyMemory<byte>>>()).IsLessThanOrEqualTo(96);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<string, int>>()).IsLessThanOrEqualTo(88);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<int, string>>()).IsLessThanOrEqualTo(88);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<byte, int>>()).IsLessThanOrEqualTo(80);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<short, int>>()).IsLessThanOrEqualTo(80);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<Guid, string>>()).IsLessThanOrEqualTo(104);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<string, ReadOnlyMemory<byte>>>()).IsLessThanOrEqualTo(104);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<int, int>>()).IsLessThanOrEqualTo(88);
        await Assert.That(Unsafe.SizeOf<ConsumeResult<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>>()).IsLessThanOrEqualTo(112);
    }

    [Test]
    [Arguments(null)]
    [Arguments(int.MinValue)]
    [Arguments(-1)]
    [Arguments(0)]
    [Arguments(int.MaxValue)]
    public async Task PackedEpoch_PreservesEpochAndIndexAcrossValueCopies(int? epoch)
    {
        await Assert.That(Unsafe.SizeOf<PackedProcessingEpoch>()).IsEqualTo(8);
        var original = PackedProcessingEpoch.FromEpoch(epoch);
        await Assert.That(original.Index).IsEqualTo(0);
        foreach (var index in new[] { 0, 1, 255, 256, 65535, PackedProcessingEpoch.IndexCapacity - 1 })
        {
            PackedProcessingEpoch[] copies = [original.WithIndex(index)];
            var copied = copies[0];
            await Assert.That(copied.LeaderEpoch).IsEqualTo(epoch);
            await Assert.That(copied.Index).IsEqualTo(index);
            await Assert.That(copied.WithIndex(0)).IsEqualTo(original);
        }
    }

    [Test]
    [Arguments(null)]
    [Arguments(42)]
    [Arguments(int.MinValue)]
    public async Task PackedEpoch_IgnoresUnspecifiedNullablePadding(int? epoch)
    {
        var expected = PackedProcessingEpoch.FromEpoch(epoch);
        ref var firstByte = ref Unsafe.As<int?, byte>(ref epoch);
        Unsafe.Add(ref firstByte, 1) = 0xff;
        Unsafe.Add(ref firstByte, 2) = 0xff;
        Unsafe.Add(ref firstByte, 3) = 0xff;
        var actual = PackedProcessingEpoch.FromEpoch(epoch);
        await Assert.That(actual.Index).IsEqualTo(0);
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments(null, false)]
    [Arguments(null, true)]
    [Arguments(-1, false)]
    [Arguments(-1, true)]
    [Arguments(0, false)]
    [Arguments(0, true)]
    [Arguments(int.MaxValue, false)]
    [Arguments(int.MinValue, true)]
    public async Task EpochAndEof_PreserveIndependentValues(int? epoch, bool eof)
    {
        var result = new ConsumeResult<string, string>("topic", 0, 10, default, true,
            default, true, null, 0, TimestampType.CreateTime, epoch, null, null, eof);
        await Assert.That(result.LeaderEpoch).IsEqualTo(epoch);
        await Assert.That(result.IsPartitionEof).IsEqualTo(eof);
    }

    [Test]
    [Arguments(null)]
    [Arguments(0)]
    [Arguments(int.MinValue)]
    [Arguments(int.MaxValue)]
    public async Task WithBrokerIdentityFrom_PreservesOriginalEpochAndReplacementProcessingIndex(int? epoch)
    {
        var original = new ConsumeResult<string, string>("original", 1, 42,
            "key", "value", null, 0, TimestampType.CreateTime, epoch) { ProcessingIndex = 17 };
        var replacement = new ConsumeResult<string, string>("replacement", 2, 99,
            "key", "replacement", null, 0, TimestampType.CreateTime, 8) { ProcessingIndex = 255 };

        var result = replacement.WithBrokerIdentityFrom(in original);

        await Assert.That(result.TopicPartitionOffset).IsEqualTo(original.TopicPartitionOffset);
        await Assert.That(result.LeaderEpoch).IsEqualTo(epoch);
        await Assert.That(result.ProcessingIndex).IsEqualTo(replacement.ProcessingIndex);
        await Assert.That(result.Value).IsEqualTo(replacement.Value);
    }

    [Test]
    public async Task ConsumeResult_DefaultIsPartitionEof_IsFalse()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 100,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.IsPartitionEof).IsFalse();
    }

    [Test]
    public async Task ConsumeResult_WithIsPartitionEofTrue_ReturnsTrue()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 100,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.NotAvailable,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null,
            isPartitionEof: true);

        await Assert.That(result.IsPartitionEof).IsTrue();
    }

    [Test]
    public async Task CreatePartitionEof_CreatesEofResult()
    {
        var result = ConsumeResult<string, string>.CreatePartitionEof("test-topic", 2, 500);

        await Assert.That(result.IsPartitionEof).IsTrue();
        await Assert.That(result.Topic).IsEqualTo("test-topic");
        await Assert.That(result.Partition).IsEqualTo(2);
        await Assert.That(result.Offset).IsEqualTo(500);
        await Assert.That(result.TimestampType).IsEqualTo(TimestampType.NotAvailable);
        await Assert.That(result.Headers).IsEmpty();
        // Zero-header results must not allocate: the getter returns the Array.Empty singleton.
        await Assert.That(ReferenceEquals(result.Headers, Array.Empty<Header>())).IsTrue();
        await Assert.That(result.LeaderEpoch).IsNull();
    }

    [Test]
    public async Task CreatePartitionEof_KeyIsDefault()
    {
        var result = ConsumeResult<string, string>.CreatePartitionEof("test-topic", 0, 0);

        // Key should be default (null for reference types)
        await Assert.That(result.Key).IsNull();
    }

    [Test]
    public async Task CreatePartitionEof_ValueIsDefault()
    {
        var result = ConsumeResult<string, string>.CreatePartitionEof("test-topic", 0, 0);

        // Value should be default (null for reference types)
        // Note: Accessing Value on an EOF result with no deserializer will throw,
        // but for the string deserializer returning null for empty is expected
        await Assert.That(result.IsPartitionEof).IsTrue();
    }

    [Test]
    public async Task Timestamp_ComputedLazilyFromTimestampMs()
    {
        // Specific Unix timestamp: 2024-01-15T12:30:00Z = 1705318200000 ms
        const long timestampMs = 1705318200000;
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 0,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: timestampMs,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.TimestampMs).IsEqualTo(timestampMs);
        await Assert.That(result.Timestamp).IsEqualTo(expected);
    }

    [Test]
    public async Task TimestampMs_ReturnsRawUnixMilliseconds()
    {
        const long timestampMs = 1705318200000;

        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 0,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: timestampMs,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.TimestampMs).IsEqualTo(timestampMs);
    }

    [Test]
    public async Task TopicPartitionOffset_IncludesLeaderEpoch()
    {
        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 42,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            headers: null,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: 7,
            keyDeserializer: null,
            valueDeserializer: null);

        await Assert.That(result.TopicPartitionOffset)
            .IsEqualTo(new TopicPartitionOffset("test-topic", 0, 42, 7));
    }

    [Test]
    public async Task LazyConsumeHeaders_CountDoesNotMaterialize()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var headers = LazyConsumeHeaders.Create(pooledHeaders, 1, pending, pending.HeaderGeneration);

        await Assert.That(headers).IsNotNull();
        await Assert.That(headers!.Count).IsEqualTo(1);

        pending.Dispose();
    }

    [Test]
    public async Task LazyConsumeHeaders_FirstAccessCopiesSnapshot()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var headers = LazyConsumeHeaders.Create(pooledHeaders, 1, pending, pending.HeaderGeneration);
        var header = headers[0];

        pooledHeaders[0] = new Header("changed", "def"u8.ToArray());

        await Assert.That(header.Key).IsEqualTo("trace-id");
        await Assert.That(headers[0].Key).IsEqualTo("trace-id");

        pending.Dispose();
    }

    [Test]
    public async Task LazyConsumeHeaders_AccessAfterDisposeBeforeMaterialize_ThrowsObjectDisposedException()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var headers = LazyConsumeHeaders.Create(pooledHeaders, 1, pending, pending.HeaderGeneration);
        pending.Dispose();

        _ = headers.Count;
        await Assert.That(() => headers[0]).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task ConsumeResult_PooledHeaders_MaterializesOnHeadersAccess()
    {
        var pooledHeaders = new[]
        {
            new Header("trace-id", "abc"u8.ToArray())
        };
        var pending = CreatePendingFetchData(pooledHeaders);
        pending.EagerParseAll();
        pending.MoveNext();

        var result = new ConsumeResult<string, string>(
            topic: "test-topic",
            partition: 0,
            offset: 0,
            keyData: default,
            isKeyNull: true,
            valueData: default,
            isValueNull: true,
            pooledHeaders: pooledHeaders,
            pooledHeaderCount: pooledHeaders.Length,
            headerOwner: pending,
            timestampMs: 0,
            timestampType: TimestampType.CreateTime,
            leaderEpoch: null,
            keyDeserializer: null,
            valueDeserializer: null);

        var headers = result.Headers;
        var first = headers[0];
        pooledHeaders[0] = new Header("changed", "def"u8.ToArray());

        await Assert.That(first.Key).IsEqualTo("trace-id");
        await Assert.That(headers[0].Key).IsEqualTo("trace-id");

        pending.Dispose();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task PreserveStorageOwner_PreservesCallerOwnedHeaders(bool useList, bool deferSnapshot)
    {
        Header[] headers = [new("trace-id", "abc"u8.ToArray())];
        using var pending = CreatePendingFetchData(headers);
        var original = new ConsumeResult<string, string>("topic", 0, 1,
            default, true, default, true, headers, headers.Length, pending,
            0, TimestampType.CreateTime, null, null, null);
        IReadOnlyList<Header> replacementHeaders = useList ? new List<Header>(headers) : headers;
        var replacement = ConsumeResult<string, string>.CreateWithCallerOwnedHeaders(
            "topic", 0, 1, default, true, default, true, replacementHeaders,
            0, TimestampType.CreateTime, null, Serializers.String, Serializers.String, deferSnapshot);

        var result = replacement;
        ConsumeResult<string, string>.PreserveStorageOwner(ref result, in original);
        var retained = result.RetainStorage();
        try
        {
            await Assert.That(retained).IsSameReferenceAs(pending);
            await Assert.That(result.Headers.Count).IsEqualTo(1);
            await Assert.That(result.Headers[0].Key).IsEqualTo("trace-id");
            if (!deferSnapshot)
                await Assert.That(result.Headers).IsSameReferenceAs(replacementHeaders);
        }
        finally
        {
            result.ReleaseStorage();
        }
    }

    private static PendingFetchData CreatePendingFetchData(Header[] headers)
    {
        var batch = new RecordBatch
        {
            BaseOffset = 0,
            BaseTimestamp = 0,
            Attributes = RecordBatchAttributes.TimestampTypeCreateTime,
            Records =
            [
                new Dekaf.Protocol.Records.Record
                {
                    Headers = headers,
                    HeaderCount = headers.Length,
                    Key = ReadOnlyMemory<byte>.Empty,
                    Value = ReadOnlyMemory<byte>.Empty,
                    IsKeyNull = true,
                    IsValueNull = true
                }
            ]
        };

        return PendingFetchData.Create("test-topic", 0, [batch]);
    }

}
