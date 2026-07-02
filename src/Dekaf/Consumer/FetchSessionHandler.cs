using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;

namespace Dekaf.Consumer;

internal sealed class FetchSessionHandler
{
    private const int LegacySessionEpoch = -1;
    private const int InitialSessionEpoch = 0;
    private const int FirstIncrementalEpoch = 1;

    private int _sessionId;
    private int _nextEpoch;
    private Dictionary<TopicPartition, CachedPartitionData> _sessionPartitions = [];
    private Dictionary<TopicPartition, CachedPartitionData> _lastDesiredPartitions = [];
    private bool _lastBuildWasFull;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public Task WaitAsync(CancellationToken cancellationToken) => _gate.WaitAsync(cancellationToken);

    public void Release() => _gate.Release();

    public bool HasActiveSession => _sessionId != 0;

    public FetchSessionBuildResult Build(IReadOnlyList<FetchRequestTopic> desiredTopics, ClusterMetadata? clusterMetadata)
    {
        var desiredPartitions = FlattenDesiredPartitions(desiredTopics);
        _lastDesiredPartitions = desiredPartitions;

        if (desiredPartitions.Count == 0 && _sessionId != 0)
            return Close();

        if (_sessionId == 0 || _nextEpoch == InitialSessionEpoch)
        {
            _lastBuildWasFull = true;
            return new FetchSessionBuildResult(
                _sessionId,
                InitialSessionEpoch,
                desiredTopics,
                ForgottenTopicsData: null,
                IsFull: true);
        }

        var changed = new List<(string Topic, Guid TopicId, FetchRequestPartition Partition)>();
        foreach (var topic in desiredTopics)
        {
            var topicName = ResolveTopicName(topic, clusterMetadata);
            foreach (var partition in topic.Partitions)
            {
                var tp = new TopicPartition(topicName, partition.Partition);
                var desired = CachedPartitionData.From(partition);
                if (!_sessionPartitions.TryGetValue(tp, out var cached) || cached != desired)
                    changed.Add((topicName, topic.TopicId, partition));
            }
        }

        var forgotten = BuildForgottenTopicsData(desiredPartitions, clusterMetadata);
        _lastBuildWasFull = false;

        return new FetchSessionBuildResult(
            _sessionId,
            _nextEpoch,
            BuildTopics(changed),
            forgotten.Count == 0 ? null : forgotten,
            IsFull: false);
    }

    public bool HandleResponse(FetchResponse response)
    {
        if (response.ErrorCode != ErrorCode.None)
        {
            HandleTopLevelError(response.ErrorCode);
            return false;
        }

        if (response.SessionId != 0)
            _sessionId = response.SessionId;

        if (_lastBuildWasFull || _sessionId != 0)
            _sessionPartitions = new Dictionary<TopicPartition, CachedPartitionData>(_lastDesiredPartitions);

        _nextEpoch = _sessionId == 0 ? InitialSessionEpoch : NextEpoch(_nextEpoch);
        return true;
    }

    public void HandleError()
    {
        if (_sessionId == 0)
        {
            Reset();
            return;
        }

        _nextEpoch = InitialSessionEpoch;
    }

    public void Reset()
    {
        _sessionId = 0;
        _nextEpoch = InitialSessionEpoch;
        _sessionPartitions.Clear();
        _lastDesiredPartitions.Clear();
        _lastBuildWasFull = false;
    }

    public FetchSessionBuildResult Close()
    {
        var sessionId = _sessionId;
        Reset();
        return new FetchSessionBuildResult(
            sessionId,
            LegacySessionEpoch,
            Array.Empty<FetchRequestTopic>(),
            ForgottenTopicsData: null,
            IsFull: true);
    }

    private void HandleTopLevelError(ErrorCode errorCode)
    {
        if (errorCode == ErrorCode.FetchSessionIdNotFound)
        {
            Reset();
            return;
        }

        if (errorCode is ErrorCode.InvalidFetchSessionEpoch or ErrorCode.FetchSessionTopicIdError)
        {
            HandleError();
            return;
        }

        Reset();
    }

    private List<ForgottenTopic> BuildForgottenTopicsData(
        Dictionary<TopicPartition, CachedPartitionData> desiredPartitions,
        ClusterMetadata? clusterMetadata)
    {
        Dictionary<string, List<int>>? forgottenPartitions = null;
        foreach (var tp in _sessionPartitions.Keys)
        {
            if (desiredPartitions.ContainsKey(tp))
                continue;

            forgottenPartitions ??= [];
            if (!forgottenPartitions.TryGetValue(tp.Topic, out var partitions))
            {
                partitions = [];
                forgottenPartitions[tp.Topic] = partitions;
            }
            partitions.Add(tp.Partition);
        }

        if (forgottenPartitions is null)
            return [];

        var result = new List<ForgottenTopic>(forgottenPartitions.Count);
        foreach (var kvp in forgottenPartitions)
        {
            result.Add(new ForgottenTopic
            {
                Topic = kvp.Key,
                TopicId = clusterMetadata?.GetTopic(kvp.Key)?.TopicId ?? Guid.Empty,
                Partitions = [.. kvp.Value]
            });
        }

        return result;
    }

    private static Dictionary<TopicPartition, CachedPartitionData> FlattenDesiredPartitions(
        IReadOnlyList<FetchRequestTopic> desiredTopics)
    {
        var result = new Dictionary<TopicPartition, CachedPartitionData>();
        foreach (var topic in desiredTopics)
        {
            var topicName = topic.Topic ?? string.Empty;
            foreach (var partition in topic.Partitions)
            {
                result[new TopicPartition(topicName, partition.Partition)] = CachedPartitionData.From(partition);
            }
        }

        return result;
    }

    private static List<FetchRequestTopic> BuildTopics(
        List<(string Topic, Guid TopicId, FetchRequestPartition Partition)> partitions)
    {
        var byTopic = new Dictionary<string, (Guid TopicId, List<FetchRequestPartition> Partitions)>();
        foreach (var (topic, topicId, partition) in partitions)
        {
            if (!byTopic.TryGetValue(topic, out var entry))
            {
                entry = (topicId, []);
                byTopic[topic] = entry;
            }
            entry.Partitions.Add(partition);
        }

        var result = new List<FetchRequestTopic>(byTopic.Count);
        foreach (var (topic, entry) in byTopic)
        {
            result.Add(new FetchRequestTopic
            {
                Topic = topic,
                TopicId = entry.TopicId,
                Partitions = entry.Partitions
            });
        }

        return result;
    }

    private static string ResolveTopicName(FetchRequestTopic topic, ClusterMetadata? clusterMetadata)
    {
        if (!string.IsNullOrEmpty(topic.Topic))
            return topic.Topic;

        return topic.TopicId == Guid.Empty
            ? string.Empty
            : clusterMetadata?.GetTopic(topic.TopicId)?.Name ?? string.Empty;
    }

    private static int NextEpoch(int current)
    {
        if (current == int.MaxValue)
            return FirstIncrementalEpoch;

        return current < FirstIncrementalEpoch ? FirstIncrementalEpoch : current + 1;
    }

    internal readonly record struct CachedPartitionData(
        long FetchOffset,
        long LogStartOffset,
        int PartitionMaxBytes,
        int CurrentLeaderEpoch)
    {
        public static CachedPartitionData From(FetchRequestPartition partition) => new(
            partition.FetchOffset,
            partition.LogStartOffset,
            partition.PartitionMaxBytes,
            partition.CurrentLeaderEpoch);
    }
}

internal readonly record struct FetchSessionBuildResult(
    int SessionId,
    int SessionEpoch,
    IReadOnlyList<FetchRequestTopic> Topics,
    IReadOnlyList<ForgottenTopic>? ForgottenTopicsData,
    bool IsFull);
