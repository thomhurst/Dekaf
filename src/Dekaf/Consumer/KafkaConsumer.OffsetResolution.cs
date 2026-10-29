using System.Runtime.ExceptionServices;
using Dekaf.Errors;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;

namespace Dekaf.Consumer;

public sealed partial class KafkaConsumer<TKey, TValue>
{
    // Allocated only for assignment/reset/seek work, never for positioned fetches or records.
    private sealed class OffsetResolution(long timestamp)
    {
        public long Timestamp { get; set; } = timestamp;
        public long? Offset { get; set; }
        public Exception? Error { get; set; }
        public bool IsPending => Offset is null && Error is null;
    }

    private async ValueTask ResolveOffsetsAsync(
        Dictionary<TopicPartition, OffsetResolution> resolutions,
        CancellationToken cancellationToken)
    {
        if (resolutions.Count == 0)
            return;

        try
        {
            await RetryHelper.WithRetryAsync(async () =>
            {
                await RefreshMissingOffsetLeadersAsync(resolutions, cancellationToken).ConfigureAwait(false);

                // A successful timestamp lookup can require a Latest fallback. Keep that second
                // phase batched too, without treating it as a failed attempt or adding backoff.
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var byBroker = new Dictionary<int, Dictionary<TopicPartition, OffsetResolution>>();
                    Exception? retryFailure = null;
                    foreach (var (partition, resolution) in resolutions)
                    {
                        if (!resolution.IsPending)
                            continue;

                        var leader = _metadataManager.TryGetCachedPartitionLeader(partition.Topic, partition.Partition);
                        if (leader is null)
                        {
                            retryFailure ??= CreateOffsetResolutionUnavailableException(partition);
                            continue;
                        }

                        if (!byBroker.TryGetValue(leader.NodeId, out var brokerPartitions))
                            byBroker.Add(leader.NodeId, brokerPartitions = []);
                        brokerPartitions.Add(partition, resolution);
                    }

                    var tasks = new Task<Exception?>[byBroker.Count];
                    var index = 0;
                    foreach (var (brokerId, brokerPartitions) in byBroker)
                    {
                        tasks[index++] = ResolveBrokerOffsetsAsync(brokerId, brokerPartitions, cancellationToken);
                    }

                    // Observe every broker before retrying or returning, including cancellation.
                    var failures = await Task.WhenAll(tasks).ConfigureAwait(false);
                    foreach (var failure in failures)
                        retryFailure ??= failure;
                    if (retryFailure is not null)
                        ExceptionDispatchInfo.Capture(retryFailure).Throw();

                    var hasPending = false;
                    foreach (var resolution in resolutions.Values)
                        hasPending |= resolution.IsPending;
                    if (!hasPending)
                        return;
                }
            }, _metadataManager, cancellationToken, _options.RetryBackoffMs, _options.RetryBackoffMaxMs,
                deadline: OffsetLookupDeadline("ListOffsets", TimeSpan.FromMilliseconds(_options.RequestTimeoutMs)))
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Preserve successful partitions when another leader exhausts its retry budget.
            // Callers publish successes before surfacing the first partition error.
            foreach (var resolution in resolutions.Values)
            {
                if (resolution.IsPending)
                    resolution.Error = ex;
            }
        }
    }

    private async ValueTask RefreshMissingOffsetLeadersAsync(
        Dictionary<TopicPartition, OffsetResolution> resolutions,
        CancellationToken cancellationToken)
    {
        HashSet<string>? missingTopics = null;
        foreach (var (partition, resolution) in resolutions)
        {
            if (resolution.IsPending
                && _metadataManager.TryGetCachedPartitionLeader(partition.Topic, partition.Partition) is null)
                (missingTopics ??= new HashSet<string>(StringComparer.Ordinal)).Add(partition.Topic);
        }
        if (missingTopics is null)
            return;

        try
        {
            // A named request retains auto-creation on a cold assignment. Force it even when
            // the topic is cached but its partition has no leader. Batch names once per attempt.
            await _metadataManager.RefreshMetadataAsync(missingTopics, forceRefresh: true, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex is not ObjectDisposedException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Match RetryHelper's best-effort refresh: resolve available leaders before retrying.
        }
        catch (Exception ex) when (
            RetryHelper.IsRetriableRequestFailure(ex) && !cancellationToken.IsCancellationRequested)
        {
            // Missing leaders below retain their contextual error; other brokers can succeed.
        }
    }

    private async Task<Exception?> ResolveBrokerOffsetsAsync(
        int brokerId,
        Dictionary<TopicPartition, OffsetResolution> resolutions,
        CancellationToken cancellationToken)
    {
        try
        {
            var connectionIndex = ConsumerCoordinator.GetCoordinationConnectionIndex(
                Volatile.Read(ref _appliedConnectionCount));
            using var lease = await _connectionPool.LeaseConnectionByIndexAsync(brokerId, connectionIndex, cancellationToken)
                .ConfigureAwait(false);
            var connection = lease.Connection;
            var version = _metadataManager.GetNegotiatedApiVersion(connection, ApiKey.ListOffsets,
                ListOffsetsRequest.LowestSupportedVersion, ListOffsetsRequest.HighestSupportedVersion);

            var byTopic = new Dictionary<string, List<ListOffsetsRequestPartition>>(StringComparer.Ordinal);
            foreach (var (partition, resolution) in resolutions)
            {
                if (!byTopic.TryGetValue(partition.Topic, out var topicPartitions))
                    byTopic.Add(partition.Topic, topicPartitions = []);
                topicPartitions.Add(new ListOffsetsRequestPartition
                {
                    PartitionIndex = partition.Partition,
                    Timestamp = resolution.Timestamp,
                    CurrentLeaderEpoch = GetCurrentLeaderEpoch(partition)
                });
            }

            var topics = new List<ListOffsetsRequestTopic>(byTopic.Count);
            foreach (var (topic, partitions) in byTopic)
                topics.Add(new ListOffsetsRequestTopic { Name = topic, Partitions = partitions });
            var response = await connection.SendWithClientTelemetryAsync<ListOffsetsRequest, ListOffsetsResponse>(
                new ListOffsetsRequest
                {
                    ReplicaId = -1,
                    IsolationLevel = _options.IsolationLevel,
                    Topics = topics
                }, version, _telemetryMetricCollector, cancellationToken).ConfigureAwait(false);

            // Index once: response processing stays linear for large assignments.
            var responses = new Dictionary<TopicPartition, ListOffsetsResponsePartition>();
            foreach (var topic in response.Topics)
            {
                foreach (var partition in topic.Partitions)
                    responses[new TopicPartition(topic.Name, partition.PartitionIndex)] = partition;
            }

            Exception? retryFailure = null;
            foreach (var (partition, resolution) in resolutions)
            {
                KafkaException? error = null;
                if (!responses.TryGetValue(partition, out var result))
                    error = new KafkaException(ErrorCode.UnknownTopicOrPartition,
                        $"ListOffsets response did not contain {partition}.");
                else if (result.ErrorCode != ErrorCode.None)
                    error = new ConsumeException(result.ErrorCode,
                        $"ListOffsets failed for {partition}: {result.ErrorCode}");

                if (error is not null)
                {
                    if (error.IsRetriable)
                        retryFailure ??= error;
                    else
                        resolution.Error = error;
                }
                else if (resolution.Timestamp >= 0 && result!.Offset == TopicPartitionTimestamp.Latest)
                {
                    resolution.Timestamp = TopicPartitionTimestamp.Latest;
                }
                else
                {
                    resolution.Offset = result!.Offset;
                }
            }

            return retryFailure;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure = ex is TimeoutException
                ? new KafkaException(ErrorCode.RequestTimedOut, $"ListOffsets request timed out for broker {brokerId}.", ex)
                : ex;
            if (TransportFailureClassifier.IsRetriable(failure, TransportRetryPolicy.Request,
                    ownerDisposed: Volatile.Read(ref _consumerDisposed) != 0))
                return failure;

            foreach (var resolution in resolutions.Values)
            {
                if (resolution.IsPending)
                    resolution.Error = failure;
            }
            return null;
        }
    }
}
