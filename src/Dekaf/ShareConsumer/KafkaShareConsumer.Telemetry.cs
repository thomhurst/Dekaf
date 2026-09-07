using Dekaf.Telemetry;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    /// <inheritdoc />
    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        ThrowIfDisposed();
        _telemetryMetricCollector.RegisterMetricForSubscription(metric);
    }

    /// <inheritdoc />
    public void UnregisterMetricFromSubscription(string name)
    {
        ThrowIfDisposed();
        _telemetryMetricCollector.UnregisterMetricFromSubscription(name);
    }

    private static long CountSentAcknowledgements(ShareFetchRequest request)
    {
        long count = 0;
        for (var topic = 0; topic < request.Topics.Count; topic++)
        {
            var partitions = request.Topics[topic].Partitions;
            for (var partition = 0; partition < partitions.Count; partition++)
            {
                var batches = partitions[partition].AcknowledgementBatches;
                if (batches is null) continue;
                for (var batch = 0; batch < batches.Count; batch++)
                    count += batches[batch].LastOffset - batches[batch].FirstOffset + 1;
            }
        }
        return count;
    }

    private static long CountSentAcknowledgements(ShareAcknowledgeRequest request)
    {
        long count = 0;
        for (var topic = 0; topic < request.Topics.Count; topic++)
        {
            var partitions = request.Topics[topic].Partitions;
            for (var partition = 0; partition < partitions.Count; partition++)
            {
                var batches = partitions[partition].AcknowledgementBatches;
                for (var batch = 0; batch < batches.Count; batch++)
                    count += batches[batch].LastOffset - batches[batch].FirstOffset + 1;
            }
        }
        return count;
    }

    private static long CountAcknowledgements(List<AcknowledgementBatchData> batches)
    {
        long count = 0;
        // The tracker emits contiguous runs of actual delivered/explicitly acknowledged
        // offsets. It splits offset gaps, so no scan of the per-record types is needed.
        for (var i = 0; i < batches.Count; i++)
            count += batches[i].LastOffset - batches[i].FirstOffset + 1;
        return count;
    }

    private long CountFailedAcknowledgements(ShareFetchResponse response,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements, long sentCount)
    {
        if (response.ErrorCode != ErrorCode.None) return sentCount;
        long errors = 0;
        for (var topicIndex = 0; topicIndex < response.Responses.Count; topicIndex++)
        {
            var topic = response.Responses[topicIndex];
            for (var partitionIndex = 0; partitionIndex < topic.Partitions.Count; partitionIndex++)
            {
                var partition = topic.Partitions[partitionIndex];
                if (partition.AcknowledgeErrorCode == ErrorCode.None) continue;
                var topicInfo = _metadataManager.Metadata.GetTopic(topic.TopicId);
                if (topicInfo is null) return sentCount;
                if (acknowledgements.TryGetValue(new TopicPartition(topicInfo.Name, partition.PartitionIndex), out var batches))
                    errors += CountAcknowledgements(batches);
            }
        }
        return errors;
    }

    private long CountFailedAcknowledgements(ShareAcknowledgeResponse response,
        Dictionary<TopicPartition, List<AcknowledgementBatchData>> acknowledgements, long sentCount)
    {
        if (response.ErrorCode != ErrorCode.None) return sentCount;
        long errors = 0;
        for (var topicIndex = 0; topicIndex < response.Responses.Count; topicIndex++)
        {
            var topic = response.Responses[topicIndex];
            for (var partitionIndex = 0; partitionIndex < topic.Partitions.Count; partitionIndex++)
            {
                var partition = topic.Partitions[partitionIndex];
                if (partition.ErrorCode == ErrorCode.None) continue;
                var topicInfo = _metadataManager.Metadata.GetTopic(topic.TopicId);
                if (topicInfo is null) return sentCount;
                if (acknowledgements.TryGetValue(new TopicPartition(topicInfo.Name, partition.PartitionIndex), out var batches))
                    errors += CountAcknowledgements(batches);
            }
        }
        return errors;
    }
}
