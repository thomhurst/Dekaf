using Dekaf.Consumer;
using Dekaf.Serialization;

namespace Dekaf.Tests.Unit.Consumer;

public sealed class PartitionedNullKeyTests
{
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public Task WireNullAndEmptyKeys_RunInSeparateLanes(int keyKind) => keyKind switch
    {
        0 => VerifyAsync(Serializers.ByteArray),
        1 => VerifyAsync(Serializers.RawBytes),
        2 => VerifyAsync(new BinaryDeserializer<Memory<byte>>(static data => data.ToArray().AsMemory())),
        3 => VerifyAsync(new BinaryDeserializer<ArraySegment<byte>>(static data => new(data.ToArray()))),
        _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
    };

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public Task WireNullAndDeserializedNullKeys_RunInSeparateLanes(int keyKind) => keyKind switch
    {
        0 => VerifyAsync(new BinaryDeserializer<byte[]?>(static _ => null)),
        1 => VerifyAsync(new BinaryDeserializer<string?>(static _ => null)),
        2 => VerifyAsync(new BinaryDeserializer<int?>(static _ => null)),
        _ => throw new ArgumentOutOfRangeException(nameof(keyKind))
    };

    private static async Task VerifyAsync<TKey>(IDeserializer<TKey> deserializer)
    {
        var lane = new PartitionLane<TKey, string>(new TopicPartition("topic", 0), 8,
            static (_, _) => ValueTask.CompletedTask, static _ => { }, static (_, error) => throw error);
        var context = new PartitionProcessorContext<TKey, string>(lane);
        var firstStarted = NewSignal();
        var releaseFirst = NewSignal();
        var secondNullStarted = NewSignal();
        var nonNullWireStarted = NewSignal();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var dispatcher = new KeyOrderedPartitionDispatcher<TKey, string>(context, 1, 2, 8,
            async (records, token) =>
            {
                switch (records[0].Offset)
                {
                    case 0:
                        firstStarted.TrySetResult();
                        await releaseFirst.Task.WaitAsync(token);
                        break;
                    case 1:
                        secondNullStarted.TrySetResult();
                        break;
                    case 2:
                        nonNullWireStarted.TrySetResult();
                        break;
                }
                context.MarkProcessed(records[0]);
            });
        var running = dispatcher.RunAsync(timeout.Token).AsTask();
        try
        {
            lane.TryEnqueue(Message(0, true, deserializer));
            await firstStarted.Task.WaitAsync(timeout.Token);
            lane.TryEnqueue(Message(1, true, deserializer));
            lane.TryEnqueue(Message(2, false, deserializer));
            await nonNullWireStarted.Task.WaitAsync(timeout.Token);
            await Assert.That(secondNullStarted.Task.IsCompleted).IsFalse();
            releaseFirst.TrySetResult();
            await secondNullStarted.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await timeout.CancelAsync();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                await Assert.That(running.IsCanceled).IsTrue();
            }
        }
    }

    private static ConsumeResult<TKey, string> Message<TKey>(long offset, bool isKeyNull, IDeserializer<TKey> deserializer)
        => new("topic", 0, offset, ReadOnlyMemory<byte>.Empty, isKeyNull, "value"u8.ToArray(), false,
            null, 0, TimestampType.CreateTime, null, deserializer, Serializers.String);

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class BinaryDeserializer<TKey>(Func<ReadOnlyMemory<byte>, TKey> deserialize) : IDeserializer<TKey>
    {
        public TKey Deserialize(ReadOnlyMemory<byte> data, SerializationContext context) => deserialize(data);
    }
}
