using Dekaf.Consumer;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Protocol.Records;

namespace Dekaf.Tests.Unit.Consumer;

public sealed partial class ConsumeOneFastPathTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RawTracking_AfterSuspendedPreparation_PreservesEmptyBytes(bool empty)
    {
        ReadOnlyMemory<byte> bytes = empty ? default : "value"u8.ToArray();
        var fetch = PendingFetchData.Create(Topic, Partition, [CreateBatch(0, new Record
        {
            Key = bytes,
            Value = bytes,
            IsKeyNull = false,
            IsValueNull = false
        })]);
        var deserializer = new GatedPreparedStringDeserializer();
        await using var consumer = CreateInitializedConsumerWithDeserializers(fetch, valueDeserializer: deserializer);
        MarkManualAssignmentCurrent(consumer);
        var accessor = (IRawRecordAccessor)consumer;
        accessor.EnableRawRecordTracking();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var consume = consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), timeout.Token);
        await deserializer.PreparationStarted.Task.WaitAsync(timeout.Token);
        await Assert.That(consume.IsCompleted).IsFalse();
        deserializer.ReleasePreparation();

        await Assert.That(await consume).IsNotNull();
        await AssertRawBytesAsync(accessor, bytes, isNull: false);
    }

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, 2)]
    public async Task RawTracking_PreservesWireNullability(bool useIterator, int payloadKind)
    {
        ReadOnlyMemory<byte> bytes = payloadKind == 2 ? "value"u8.ToArray() : default;
        var fetch = PendingFetchData.Create(Topic, Partition, [CreateBatch(0, new Record
        {
            Key = bytes,
            Value = bytes,
            IsKeyNull = payloadKind == 0,
            IsValueNull = payloadKind == 0
        })]);
        await using var consumer = CreateInitializedConsumer(fetch);
        var accessor = (IRawRecordAccessor)consumer;
        accessor.EnableRawRecordTracking();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        if (useIterator)
        {
            await using var iterator = consumer.ConsumeAsync(timeout.Token).GetAsyncEnumerator();
            await Assert.That(await iterator.MoveNextAsync()).IsTrue();
            await AssertRawBytesAsync(accessor, bytes, payloadKind == 0);
        }
        else
        {
            await Assert.That(await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(5), timeout.Token)).IsNotNull();
            await AssertRawBytesAsync(accessor, bytes, payloadKind == 0);
        }
    }

    private static async Task AssertRawBytesAsync(IRawRecordAccessor accessor, ReadOnlyMemory<byte> bytes, bool isNull)
    {
        await Assert.That(accessor.TryGetCurrentRawRecord(out var key, out var value)).IsTrue();
        await Assert.That(key.Equals(default(ReadOnlyMemory<byte>))).IsEqualTo(isNull);
        await Assert.That(value.Equals(default(ReadOnlyMemory<byte>))).IsEqualTo(isNull);
        await Assert.That(key.Span.SequenceEqual(bytes.Span)).IsTrue();
        await Assert.That(value.Span.SequenceEqual(bytes.Span)).IsTrue();
    }
}
