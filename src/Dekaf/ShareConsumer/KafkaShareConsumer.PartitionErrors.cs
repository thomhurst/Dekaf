using System.Diagnostics;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Retry;
using Microsoft.Extensions.Logging;
#if !NET9_0_OR_GREATER
using TopicPartitionSet = System.Collections.Generic.IReadOnlyCollection<Dekaf.TopicPartition>;
#else
using TopicPartitionSet = System.Collections.Generic.IReadOnlySet<Dekaf.TopicPartition>;
#endif

namespace Dekaf.ShareConsumer;

internal sealed partial class KafkaShareConsumer<TKey, TValue>
{
    // Partitions whose latest ShareFetch failed with a partition-level error. Allocated on the
    // first error; a healthy poll only checks this field for null. An entry stays until the
    // partition fetches successfully or leaves the assignment, so repeated errors back off.
    private Dictionary<TopicPartition, PartitionFetchError>? _partitionFetchErrors;
    private TopicPartitionSet? _partitionFetchErrorsAssignment;
    // Set by fetch and acknowledgement tasks; the next poll refreshes metadata before fetching.
    private volatile bool _partitionMetadataRefreshRequested;

    /// <summary>
    /// Applies partition-level ShareFetch errors once per response: reroutes a moved partition
    /// to the leader the broker named, or suspends the partition with backoff and requests a
    /// metadata refresh. Never runs per record.
    /// </summary>
    private void HandlePartitionFetchErrors(int brokerId, ShareFetchResponse response)
    {
        var topics = response.Responses;
        for (var topicIndex = 0; topicIndex < topics.Count; topicIndex++)
        {
            var topic = topics[topicIndex];
            var partitions = topic.Partitions;
            TopicInfoLookup lookup = default;
            for (var partitionIndex = 0; partitionIndex < partitions.Count; partitionIndex++)
            {
                var partition = partitions[partitionIndex];
                if (partition.ErrorCode == ErrorCode.None)
                {
                    if (_partitionFetchErrors is { Count: > 0 } errors
                        && lookup.Get(_metadataManager, topic.TopicId) is { } recoveredTopic)
                        errors.Remove(new TopicPartition(recoveredTopic.Name, partition.PartitionIndex));
                    continue;
                }

                // Metadata without this topic ID, for example after the topic was deleted and
                // metadata refreshed, gives its partitions no leader, so no fetch includes them.
                // The broker can still report them while its share session remembers them.
                if (lookup.Get(_metadataManager, topic.TopicId) is not { } topicInfo)
                    continue;

                HandlePartitionFetchError(
                    brokerId,
                    new TopicPartition(topicInfo.Name, partition.PartitionIndex),
                    partition,
                    response.NodeEndpoints);
            }
        }
    }

    private void HandlePartitionFetchError(
        int brokerId,
        TopicPartition topicPartition,
        ShareFetchResponsePartition partition,
        IReadOnlyList<ShareFetchNodeEndpoint> nodeEndpoints)
    {
        if (!_assignmentSnapshot.Contains(topicPartition))
            return;

        // A share session keeps a partition after the client stops fetching it from that broker.
        // Once metadata routes the partition elsewhere, a later error from the old broker is stale.
        var leader = _metadataManager.Metadata.GetPartitionLeader(topicPartition.Topic, topicPartition.Partition);
        if (leader is not null && leader.NodeId != brokerId)
            return;

        var errorCode = partition.ErrorCode;
        var currentLeader = partition.CurrentLeader;
        if (IsLeaderChangeError(errorCode)
            && TryApplyLeaderHint(topicPartition, currentLeader.LeaderId, currentLeader.LeaderEpoch,
                FindNodeEndpoint(nodeEndpoints, currentLeader.LeaderId)))
        {
            _partitionFetchErrors?.Remove(topicPartition);
            LogPartitionLeaderMoved(topicPartition.Topic, topicPartition.Partition, errorCode, currentLeader.LeaderId);
            return;
        }

        var errors = _partitionFetchErrors ??= [];
        var now = Stopwatch.GetTimestamp();
        // The broker's share session keeps a suspended partition while another partition on the
        // same broker is fetched, so it can report the error again during the backoff. Keep the
        // deadline and the requested refresh, so each partition refreshes at most once per backoff.
        if (IsPartitionFetchSuspended(errors, topicPartition, now))
            return;

        // An incremental response can omit a recovered partition, so an entry can outlive its
        // error. Continue a streak only when this error follows the previous retry promptly.
        var streakWindow = (long)(_options.RetryBackoffMaxMs + _options.FetchMaxWaitMs) * Stopwatch.Frequency / 1000;
        var consecutiveErrors = errors.TryGetValue(topicPartition, out var previous)
            && previous.ErrorCode == errorCode && now - previous.RetryAtTimestamp <= streakWindow
                ? previous.ConsecutiveErrors + 1
                : 1;
        var backoffMs = ExponentialRetryBackoff.CalculateDelayMilliseconds(
            _options.RetryBackoffMs,
            _options.RetryBackoffMaxMs,
            consecutiveErrors);
        errors[topicPartition] = new PartitionFetchError(
            errorCode,
            consecutiveErrors,
            now + backoffMs * Stopwatch.Frequency / 1000);
        _partitionMetadataRefreshRequested = true;

        // Warn when an error starts or changes. Repeats of the same error are expected while
        // metadata catches up, so they log at debug level.
        if (consecutiveErrors == 1)
            LogPartitionFetchError(topicPartition.Topic, topicPartition.Partition, errorCode, backoffMs);
        else
            LogPartitionFetchErrorRepeated(topicPartition.Topic, topicPartition.Partition, errorCode,
                consecutiveErrors, backoffMs);
    }

    /// <summary>
    /// Applies the leader hints of a ShareAcknowledge response whose partitions moved, so the
    /// next fetch reaches the new leader. Requests a metadata refresh when a hint is missing.
    /// </summary>
    private void ApplyAcknowledgeLeaderHints(ShareAcknowledgeResponse response)
    {
        var topics = response.Responses;
        for (var topicIndex = 0; topicIndex < topics.Count; topicIndex++)
        {
            var topic = topics[topicIndex];
            var partitions = topic.Partitions;
            TopicInfoLookup lookup = default;
            for (var partitionIndex = 0; partitionIndex < partitions.Count; partitionIndex++)
            {
                var partition = partitions[partitionIndex];
                if (!IsLeaderChangeError(partition.ErrorCode))
                    continue;

                var currentLeader = partition.CurrentLeader;
                if (lookup.Get(_metadataManager, topic.TopicId) is not { } topicInfo
                    || !TryApplyLeaderHint(
                        new TopicPartition(topicInfo.Name, partition.PartitionIndex),
                        currentLeader.LeaderId,
                        currentLeader.LeaderEpoch,
                        FindNodeEndpoint(response.NodeEndpoints, currentLeader.LeaderId)))
                {
                    _partitionMetadataRefreshRequested = true;
                }
            }
        }
    }

    private bool TryApplyLeaderHint(TopicPartition topicPartition, int leaderId, int leaderEpoch, NodeEndpoint? endpoint)
        => _metadataManager.TryUpdatePartitionLeader(
            topicPartition.Topic,
            topicPartition.Partition,
            leaderId,
            leaderEpoch,
            endpoint);

    private static NodeEndpoint? FindNodeEndpoint(IReadOnlyList<ShareFetchNodeEndpoint> endpoints, int nodeId)
    {
        for (var index = 0; index < endpoints.Count; index++)
        {
            var endpoint = endpoints[index];
            if (endpoint.NodeId == nodeId)
                return new NodeEndpoint { NodeId = nodeId, Host = endpoint.Host, Port = endpoint.Port, Rack = endpoint.Rack };
        }
        return null;
    }

    private static NodeEndpoint? FindNodeEndpoint(IReadOnlyList<ShareAcknowledgeNodeEndpoint> endpoints, int nodeId)
    {
        for (var index = 0; index < endpoints.Count; index++)
        {
            var endpoint = endpoints[index];
            if (endpoint.NodeId == nodeId)
                return new NodeEndpoint { NodeId = nodeId, Host = endpoint.Host, Port = endpoint.Port, Rack = endpoint.Rack };
        }
        return null;
    }

    private static bool IsLeaderChangeError(ErrorCode errorCode)
        => errorCode is ErrorCode.NotLeaderOrFollower or ErrorCode.FencedLeaderEpoch;

    private static string? LeaderChangeAcknowledgementHint(ErrorCode errorCode)
        => IsLeaderChangeError(errorCode)
            ? " The partition leader changed, so this acquisition is lost; the new leader redelivers the records."
            : null;

    /// <summary>
    /// Errors after which the broker no longer holds the partition's acquisitions for this member.
    /// The share-partition state moves with leadership: the new leader reloads it and makes
    /// in-flight records available again, so resending these acknowledgements can never succeed.
    /// </summary>
    private static bool IsAcquisitionLostError(ErrorCode errorCode)
        => errorCode is ErrorCode.InvalidRecordState
            or ErrorCode.NotLeaderOrFollower
            or ErrorCode.FencedLeaderEpoch
            or ErrorCode.UnknownTopicOrPartition
            or ErrorCode.UnknownTopicId;

    private static bool IsPartitionFetchSuspended(
        Dictionary<TopicPartition, PartitionFetchError> errors, TopicPartition topicPartition, long now)
        => errors.TryGetValue(topicPartition, out var error) && error.RetryAtTimestamp > now;

    /// <summary>
    /// Returns the fetch-error entries that can suspend partitions of this assignment, or null.
    /// Drops entries for partitions that left the assignment when the assignment changes.
    /// </summary>
    private Dictionary<TopicPartition, PartitionFetchError>? GetPartitionFetchErrors(TopicPartitionSet assignment)
    {
        if (_partitionFetchErrors is not { Count: > 0 } errors)
            return null;
        if (!ReferenceEquals(_partitionFetchErrorsAssignment, assignment))
        {
            _partitionFetchErrorsAssignment = assignment;
            List<TopicPartition>? revoked = null;
            foreach (var topicPartition in errors.Keys)
            {
                if (!assignment.Contains(topicPartition))
                    (revoked ??= []).Add(topicPartition);
            }
            if (revoked is not null)
            {
                foreach (var topicPartition in revoked)
                    errors.Remove(topicPartition);
            }
        }
        return errors.Count == 0 ? null : errors;
    }

    /// <summary>
    /// Returns how long to wait before a suspended partition may be fetched again, or 0 when an
    /// assigned partition with a leader can be fetched now or none is suspended.
    /// </summary>
    private int GetPartitionFetchResumeDelayMs(TopicPartitionSet assignment)
    {
        if (GetPartitionFetchErrors(assignment) is not { } errors)
            return 0;

        var now = Stopwatch.GetTimestamp();
        var earliest = long.MaxValue;
        foreach (var topicPartition in assignment)
        {
            if (_metadataManager.Metadata.GetPartitionLeader(topicPartition.Topic, topicPartition.Partition) is null)
                continue;
            if (!errors.TryGetValue(topicPartition, out var error) || error.RetryAtTimestamp <= now)
                return 0;
            earliest = Math.Min(earliest, error.RetryAtTimestamp);
        }

        if (earliest == long.MaxValue)
            return 0;
        return (int)Math.Max(1, (earliest - now + Stopwatch.Frequency / 1000 - 1) * 1000 / Stopwatch.Frequency);
    }

    private ValueTask RefreshMetadataForPartitionErrorsAsync(CancellationToken cancellationToken)
        => _partitionMetadataRefreshRequested
            ? RefreshMetadataForPartitionErrorsCoreAsync(cancellationToken)
            : default;

    private async ValueTask RefreshMetadataForPartitionErrorsCoreAsync(CancellationToken cancellationToken)
    {
        _partitionMetadataRefreshRequested = false;
        try
        {
            await _metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not ObjectDisposedException && !cancellationToken.IsCancellationRequested)
        {
            // Best effort: suspended partitions keep backing off, and the next error asks again.
            LogPartitionErrorMetadataRefreshFailed(ex);
        }
    }

    private readonly record struct PartitionFetchError(ErrorCode ErrorCode, int ConsecutiveErrors, long RetryAtTimestamp);

    // Resolves a response topic's metadata once, and only for partitions that need it.
    private struct TopicInfoLookup
    {
        private bool _resolved;
        private TopicInfo? _topicInfo;

        internal TopicInfo? Get(MetadataManager metadataManager, Guid topicId)
        {
            if (!_resolved)
            {
                _topicInfo = metadataManager.Metadata.GetTopic(topicId);
                _resolved = true;
            }
            return _topicInfo;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "ShareFetch partition error: {Topic}-{Partition}: {ErrorCode}. Refreshing metadata and retrying the partition in {BackoffMs} ms")]
    private partial void LogPartitionFetchError(string topic, int partition, ErrorCode errorCode, int backoffMs);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "ShareFetch partition error repeated: {Topic}-{Partition}: {ErrorCode} ({ConsecutiveErrors} in a row). Retrying the partition in {BackoffMs} ms")]
    private partial void LogPartitionFetchErrorRepeated(string topic, int partition, ErrorCode errorCode,
        int consecutiveErrors, int backoffMs);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "ShareFetch partition {Topic}-{Partition} moved ({ErrorCode}); fetching from leader {LeaderId}")]
    private partial void LogPartitionLeaderMoved(string topic, int partition, ErrorCode errorCode, int leaderId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Metadata refresh after a ShareFetch partition error failed")]
    private partial void LogPartitionErrorMetadataRefreshFailed(Exception exception);
}
