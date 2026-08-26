using System.Buffers;

namespace Dekaf.ShareConsumer;

internal static class AcknowledgementCommitCallbackInvoker
{
    internal static void Invoke(
        ShareAcknowledgementCommitCallback callback,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements,
        Dictionary<TopicPartition, Exception>? errors)
    {
        var results = ArrayPool<ShareAcknowledgementCommitResult>.Shared.Rent(acknowledgements.Count);
        try
        {
            var index = 0;
            foreach (var (topicPartition, batches) in acknowledgements)
            {
                Exception? exception = null;
                errors?.TryGetValue(topicPartition, out exception);
                results[index++] = new ShareAcknowledgementCommitResult(
                    topicPartition,
                    new ShareAcknowledgedOffsets(batches),
                    exception);
            }

            System.Array.Sort(results, 0, acknowledgements.Count, ResultComparer.Instance);
            callback(results.AsSpan(0, acknowledgements.Count));
        }
        finally
        {
            ArrayPool<ShareAcknowledgementCommitResult>.Shared.Return(results, clearArray: true);
        }
    }

    private sealed class ResultComparer : IComparer<ShareAcknowledgementCommitResult>
    {
        internal static readonly ResultComparer Instance = new();

        public int Compare(
            ShareAcknowledgementCommitResult left,
            ShareAcknowledgementCommitResult right)
        {
            var topicComparison = string.CompareOrdinal(
                left.TopicPartition.Topic,
                right.TopicPartition.Topic);
            return topicComparison != 0
                ? topicComparison
                : left.TopicPartition.Partition.CompareTo(right.TopicPartition.Partition);
        }
    }
}
