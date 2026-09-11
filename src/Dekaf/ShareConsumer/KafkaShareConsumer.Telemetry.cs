using Dekaf.Networking;
using Dekaf.Protocol.Messages;
using Dekaf.Telemetry;

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    // Value-type specializations let the JIT remove disabled record accounting.
    // Select once per parsing window; record loops do not read subscription state.
    private readonly struct RecordTelemetryEnabled { }
    private readonly struct RecordTelemetryDisabled { }

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

    // Count acknowledgement outcomes per request/partition. Gap entries are protocol
    // placeholders, not acknowledged records. This scan runs only for subscribed metrics.
    private void RecordAcknowledgementFailureMetrics(
        Dictionary<TopicPartition, List<AcknowledgementBatchData>>? acknowledgements)
    {
        var metrics = ShareMetrics;
        if (acknowledgements is null || metrics is null
            || !metrics.Enabled(ShareConsumerTelemetryMetrics.Groups.Acknowledgements))
            return;

        long count = 0;
        foreach (var batches in acknowledgements.Values)
        {
            for (var index = 0; index < batches.Count; index++)
            {
                var batch = batches[index];
                count += CountAcknowledgedRecords(batch.FirstOffset, batch.LastOffset, batch.AcknowledgeTypes);
            }
        }
        metrics.AcknowledgementsFailed(count);
    }

    private KafkaRequestWriteContext? PrepareFetchTelemetry(
        int brokerId, ShareFetchRequest request, KafkaRequestWriteContext? context)
    {
        var metrics = ShareMetrics;
        if (metrics is null || !metrics.Enabled(ShareConsumerTelemetryMetrics.Groups.Fetch
            | ShareConsumerTelemetryMetrics.Groups.Acknowledgements))
            return _hostedRequestCancellationToken.CanBeCanceled ? context : null;
        if (context is null)
        {
            context = metrics.GetRequestWriteContext(brokerId);
            context.Reset();
        }
        if (!metrics.Enabled(ShareConsumerTelemetryMetrics.Groups.Acknowledgements))
        {
            metrics.StartFetch(brokerId, 0);
            return context;
        }
        long count = 0;
        for (var topicIndex = 0; topicIndex < request.Topics.Count; topicIndex++)
        {
            var partitions = request.Topics[topicIndex].Partitions;
            for (var partitionIndex = 0; partitionIndex < partitions.Count; partitionIndex++)
            {
                var batches = partitions[partitionIndex].AcknowledgementBatches;
                if (batches is null) continue;
                for (var index = 0; index < batches.Count; index++)
                {
                    var batch = batches[index];
                    count += CountAcknowledgedRecords(batch.FirstOffset, batch.LastOffset, batch.AcknowledgeTypes);
                }
            }
        }
        metrics.StartFetch(brokerId, count);
        return context;
    }

    private KafkaRequestWriteContext? PrepareAcknowledgementTelemetry(
        int brokerId, ShareAcknowledgeRequest request, KafkaRequestWriteContext? context)
    {
        var metrics = ShareMetrics;
        if (metrics is null || !metrics.Enabled(ShareConsumerTelemetryMetrics.Groups.Acknowledgements))
            return _hostedRequestCancellationToken.CanBeCanceled ? context : null;
        if (context is null)
        {
            context = metrics.GetRequestWriteContext(brokerId);
            context.Reset();
        }
        long count = 0;
        for (var topicIndex = 0; topicIndex < request.Topics.Count; topicIndex++)
        {
            var partitions = request.Topics[topicIndex].Partitions;
            for (var partitionIndex = 0; partitionIndex < partitions.Count; partitionIndex++)
            {
                var batches = partitions[partitionIndex].AcknowledgementBatches;
                for (var index = 0; index < batches.Count; index++)
                {
                    var batch = batches[index];
                    count += CountAcknowledgedRecords(batch.FirstOffset, batch.LastOffset, batch.AcknowledgeTypes);
                }
            }
        }
        metrics.AcknowledgementRequestStarted(brokerId, count);
        return context;
    }

    private KafkaRequestWriteContext? PrepareSessionCloseTelemetry(int brokerId)
    {
        var metrics = ShareMetrics;
        if (metrics is null || !metrics.Enabled(ShareConsumerTelemetryMetrics.Groups.Fetch)) return null;
        var context = metrics.GetRequestWriteContext(brokerId);
        context.Reset();
        metrics.StartFetch(brokerId, 0, resetRecords: false);
        return context;
    }

    private static long CountAcknowledgedRecords(long firstOffset, long lastOffset, IReadOnlyList<byte> types)
    {
        if (types.Count == 1)
            return types[0] == (byte)AcknowledgeType.Gap ? 0 : lastOffset - firstOffset + 1;
        long count = types.Count;
        if (types is byte[] array)
        {
            // The producer of these batches uses arrays. IndexOf skips contiguous
            // non-gap regions with the runtime's span implementation.
            ReadOnlySpan<byte> remaining = array;
            int gap;
            while ((gap = remaining.IndexOf((byte)AcknowledgeType.Gap)) >= 0)
            {
                count--;
                remaining = remaining[(gap + 1)..];
            }
        }
        else
        {
            for (var index = 0; index < types.Count; index++)
                if (types[index] == (byte)AcknowledgeType.Gap) count--;
        }
        return count;
    }
}
