using Dekaf.Consumer;

namespace Dekaf.Tests.Unit.Consumer;

internal static class PartitionLaneTestExtensions
{
    internal static bool TryEnqueueForTest<TKey, TValue>(
        this PartitionLane<TKey, TValue> lane, in ConsumeResult<TKey, TValue> message)
    {
        var batch = lane.CreateCompletionBatch(1);
        var published = lane.TryEnqueue(message, batch);
        lane.EndBatch(batch, published && !message.IsPartitionEof ? 1 : 0);
        return published;
    }
}
