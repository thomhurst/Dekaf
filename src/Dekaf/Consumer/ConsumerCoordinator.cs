using System.Buffers;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Producer;
using Microsoft.Extensions.Logging;

namespace Dekaf.Consumer;

/// <summary>
/// Handles consumer group coordination.
/// </summary>
public sealed class ConsumerCoordinator : IAsyncDisposable
{
    private readonly ConsumerOptions _options;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly IRebalanceListener? _rebalanceListener;
    private readonly ILogger<ConsumerCoordinator>? _logger;

    private int _coordinatorId = -1;
    private string? _memberId;
    private int _generationId = -1;
    private string? _leaderId;
    private IReadOnlyList<JoinGroupResponseMember>? _groupMembers;
    private HashSet<TopicPartition> _assignedPartitions = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    private volatile CoordinatorState _state = CoordinatorState.Unjoined;
    private volatile bool _disposed;

    public ConsumerCoordinator(
        ConsumerOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILogger<ConsumerCoordinator>? logger = null)
    {
        _options = options;
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _rebalanceListener = options.RebalanceListener;
        _logger = logger;
    }

    public string? MemberId => _memberId;
    public int GenerationId => _generationId;
    public bool IsLeader => _memberId is not null && _memberId == _leaderId;
    public CoordinatorState State => _state;
    public IReadOnlySet<TopicPartition> Assignment => _assignedPartitions;

    /// <summary>
    /// Ensures the consumer has joined the group.
    /// </summary>
    public async ValueTask EnsureActiveGroupAsync(
        IReadOnlySet<string> topics,
        CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ConsumerCoordinator));

        if (string.IsNullOrEmpty(_options.GroupId))
            return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_state == CoordinatorState.Stable)
                return;

            var deadline = DateTime.UtcNow.AddMilliseconds(_options.RebalanceTimeoutMs);
            var retryDelayMs = 200;

            while (_state != CoordinatorState.Stable)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException(
                        $"Failed to join group '{_options.GroupId}' within rebalance timeout ({_options.RebalanceTimeoutMs}ms)");
                }

                try
                {
                    // Find coordinator if unknown
                    if (_coordinatorId < 0)
                    {
                        await FindCoordinatorAsync(cancellationToken).ConfigureAwait(false);
                    }

                    // Join group
                    _state = CoordinatorState.Joining;
                    await JoinGroupAsync(topics, cancellationToken).ConfigureAwait(false);

                    // Sync group
                    _state = CoordinatorState.Syncing;
                    await SyncGroupAsync(topics, cancellationToken).ConfigureAwait(false);

                    _state = CoordinatorState.Stable;

                    // Start heartbeat
                    StartHeartbeat();

                    _logger?.LogInformation(
                        "Joined group {GroupId} as member {MemberId} (generation {Generation})",
                        _options.GroupId, _memberId, _generationId);
                }
                catch (Errors.GroupException ex) when (IsRetriableCoordinatorError(ex.ErrorCode))
                {
                    // Coordinator has changed, is unavailable, or still loading - mark unknown and retry
                    _logger?.LogDebug(
                        "Retriable coordinator error {ErrorCode}, will re-discover coordinator",
                        ex.ErrorCode);

                    MarkCoordinatorUnknown();

                    await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                    retryDelayMs = Math.Min(retryDelayMs * 2, 2000);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Marks the coordinator as unknown, forcing re-discovery on next operation.
    /// </summary>
    private void MarkCoordinatorUnknown()
    {
        _coordinatorId = -1;
        _state = CoordinatorState.Unjoined;
    }

    /// <summary>
    /// Returns true if the error code indicates a retriable coordinator error.
    /// </summary>
    private static bool IsRetriableCoordinatorError(ErrorCode? errorCode) =>
        errorCode is ErrorCode.NotCoordinator
            or ErrorCode.CoordinatorNotAvailable
            or ErrorCode.CoordinatorLoadInProgress;

    /// <summary>
    /// Returns true if the error code indicates a rejoin is needed.
    /// </summary>
    private static bool IsRejoinNeededError(ErrorCode? errorCode) =>
        errorCode is ErrorCode.RebalanceInProgress
            or ErrorCode.UnknownMemberId
            or ErrorCode.IllegalGeneration
            or ErrorCode.NotCoordinator
            or ErrorCode.CoordinatorNotAvailable
            or ErrorCode.CoordinatorLoadInProgress;

    private async ValueTask FindCoordinatorAsync(CancellationToken cancellationToken)
    {
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var request = new FindCoordinatorRequest
        {
            Key = _options.GroupId!,
            KeyType = CoordinatorType.Group
        };

        // Retry loop for transient errors (CoordinatorNotAvailable, CoordinatorLoadInProgress)
        const int maxRetries = 5;
        var retryDelayMs = 100;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken)
                .ConfigureAwait(false);

            // Use negotiated API version
            var findCoordinatorVersion = _metadataManager.GetNegotiatedApiVersion(
                ApiKey.FindCoordinator,
                FindCoordinatorRequest.LowestSupportedVersion,
                FindCoordinatorRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                request,
                findCoordinatorVersion,
                cancellationToken).ConfigureAwait(false);

            // For v4+, coordinator info is in the Coordinators array
            int nodeId;
            string host;
            int port;
            ErrorCode errorCode;

            if (response.Coordinators is { Count: > 0 })
            {
                var coordinator = response.Coordinators[0];
                errorCode = coordinator.ErrorCode;
                nodeId = coordinator.NodeId;
                host = coordinator.Host;
                port = coordinator.Port;
            }
            else
            {
                // v0-v3 format
                errorCode = response.ErrorCode;
                nodeId = response.NodeId;
                host = response.Host ?? throw new InvalidOperationException("Coordinator host is null");
                port = response.Port;
            }

            // Retry on transient coordinator errors
            if (errorCode == ErrorCode.CoordinatorNotAvailable ||
                errorCode == ErrorCode.CoordinatorLoadInProgress)
            {
                _logger?.LogDebug(
                    "Coordinator not available (attempt {Attempt}/{MaxRetries}), retrying in {Delay}ms",
                    attempt + 1, maxRetries, retryDelayMs);

                await Task.Delay(retryDelayMs, cancellationToken).ConfigureAwait(false);
                retryDelayMs = Math.Min(retryDelayMs * 2, 1000); // Exponential backoff, max 1s
                continue;
            }

            if (errorCode != ErrorCode.None)
            {
                throw new Errors.GroupException(errorCode, $"FindCoordinator failed: {errorCode}")
                {
                    GroupId = _options.GroupId
                };
            }

            _coordinatorId = nodeId;
            _connectionPool.RegisterBroker(nodeId, host, port);

            _logger?.LogDebug("Found coordinator {NodeId} for group {GroupId}", _coordinatorId, _options.GroupId);
            return;
        }

        throw new Errors.GroupException(ErrorCode.CoordinatorNotAvailable,
            $"FindCoordinator failed after {maxRetries} retries: CoordinatorNotAvailable")
        {
            GroupId = _options.GroupId
        };
    }

    private async ValueTask JoinGroupAsync(IReadOnlySet<string> topics, CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(_coordinatorId, cancellationToken)
            .ConfigureAwait(false);

        // Build subscription metadata
        var metadata = BuildSubscriptionMetadata(topics);

        var request = new JoinGroupRequest
        {
            GroupId = _options.GroupId!,
            SessionTimeoutMs = _options.SessionTimeoutMs,
            RebalanceTimeoutMs = _options.RebalanceTimeoutMs,
            MemberId = _memberId ?? string.Empty,
            GroupInstanceId = _options.GroupInstanceId,
            ProtocolType = "consumer",
            Protocols =
            [
                new JoinGroupRequestProtocol
                {
                    Name = GetAssignorName(),
                    Metadata = metadata
                }
            ]
        };

        // Use negotiated API version
        var joinGroupVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.JoinGroup,
            JoinGroupRequest.LowestSupportedVersion,
            JoinGroupRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
            request,
            joinGroupVersion,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode == ErrorCode.MemberIdRequired)
        {
            // Retry with assigned member ID
            _memberId = ((JoinGroupResponse)response).MemberId;
            await JoinGroupAsync(topics, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (response.ErrorCode != ErrorCode.None)
        {
            throw new Errors.GroupException(response.ErrorCode, $"JoinGroup failed: {response.ErrorCode}")
            {
                GroupId = _options.GroupId
            };
        }

        _memberId = response.MemberId;
        _generationId = response.GenerationId;
        _leaderId = response.Leader;

        // Store members list if we're the leader (need it for assignment)
        _groupMembers = response.IsLeader ? response.Members : null;

        _logger?.LogDebug(
            "Joined group {GroupId}, member={MemberId}, generation={Generation}, isLeader={IsLeader}",
            _options.GroupId, _memberId, _generationId, IsLeader);
    }

    private async ValueTask SyncGroupAsync(IReadOnlySet<string> topics, CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(_coordinatorId, cancellationToken)
            .ConfigureAwait(false);

        // Only leader sends assignments
        var assignments = Array.Empty<SyncGroupRequestAssignment>();

        if (IsLeader && _groupMembers is not null)
        {
            assignments = await ComputeAssignmentsAsync(topics, _groupMembers, cancellationToken)
                .ConfigureAwait(false);
            _logger?.LogDebug("Leader computed {Count} assignments", assignments.Length);
        }

        var request = new SyncGroupRequest
        {
            GroupId = _options.GroupId!,
            GenerationId = _generationId,
            MemberId = _memberId!,
            GroupInstanceId = _options.GroupInstanceId,
            ProtocolType = "consumer",  // Must match JoinGroup protocol type
            ProtocolName = GetAssignorName(),  // Must match JoinGroup protocol name
            Assignments = assignments
        };

        // Use negotiated API version
        var syncGroupVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.SyncGroup,
            SyncGroupRequest.LowestSupportedVersion,
            SyncGroupRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
            request,
            syncGroupVersion,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            throw new Errors.GroupException(response.ErrorCode, $"SyncGroup failed: {response.ErrorCode}")
            {
                GroupId = _options.GroupId
            };
        }

        // Parse assignment
        var oldAssignment = _assignedPartitions;
        _assignedPartitions = ParseAssignment(response.Assignment);

        // Notify listener
        if (_rebalanceListener is not null)
        {
            var revoked = oldAssignment.Except(_assignedPartitions);
            var assigned = _assignedPartitions.Except(oldAssignment);

            if (revoked.Any())
            {
                await _rebalanceListener.OnPartitionsRevokedAsync(revoked, cancellationToken).ConfigureAwait(false);
            }

            if (assigned.Any())
            {
                await _rebalanceListener.OnPartitionsAssignedAsync(assigned, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger?.LogDebug("Received assignment: {Partitions}", string.Join(", ", _assignedPartitions));
    }

    private void StartHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = HeartbeatLoopAsync(_heartbeatCts.Token);
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.HeartbeatIntervalMs, cancellationToken).ConfigureAwait(false);
                await SendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Heartbeat failed");

                if (ex is Errors.GroupException ge && IsRejoinNeededError(ge.ErrorCode))
                {
                    // Mark coordinator unknown if it's a coordinator error
                    if (IsRetriableCoordinatorError(ge.ErrorCode))
                    {
                        MarkCoordinatorUnknown();
                    }
                    else
                    {
                        _state = CoordinatorState.Unjoined;
                    }

                    break;
                }
            }
        }
    }

    private async ValueTask SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionPool.GetConnectionAsync(_coordinatorId, cancellationToken)
            .ConfigureAwait(false);

        var request = new HeartbeatRequest
        {
            GroupId = _options.GroupId!,
            GenerationId = _generationId,
            MemberId = _memberId!,
            GroupInstanceId = _options.GroupInstanceId
        };

        // Use negotiated API version
        var heartbeatVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.Heartbeat,
            HeartbeatRequest.LowestSupportedVersion,
            HeartbeatRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
            request,
            heartbeatVersion,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            throw new Errors.GroupException(response.ErrorCode, $"Heartbeat failed: {response.ErrorCode}")
            {
                GroupId = _options.GroupId
            };
        }
    }

    /// <summary>
    /// Commits offsets for the group.
    /// </summary>
    public async ValueTask CommitOffsetsAsync(
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.GroupId))
            return;

        var connection = await _connectionPool.GetConnectionAsync(_coordinatorId, cancellationToken)
            .ConfigureAwait(false);

        // Group offsets by topic without LINQ to avoid allocations
        Dictionary<string, List<OffsetCommitRequestPartition>>? topicGroups = null;
        foreach (var offset in offsets)
        {
            topicGroups ??= new Dictionary<string, List<OffsetCommitRequestPartition>>();
            if (!topicGroups.TryGetValue(offset.Topic, out var partitions))
            {
                partitions = new List<OffsetCommitRequestPartition>();
                topicGroups[offset.Topic] = partitions;
            }
            partitions.Add(new OffsetCommitRequestPartition
            {
                PartitionIndex = offset.Partition,
                CommittedOffset = offset.Offset
            });
        }

        var topicOffsets = new List<OffsetCommitRequestTopic>();
        if (topicGroups is not null)
        {
            foreach (var kvp in topicGroups)
            {
                topicOffsets.Add(new OffsetCommitRequestTopic
                {
                    Name = kvp.Key,
                    Partitions = kvp.Value
                });
            }
        }

        var request = new OffsetCommitRequest
        {
            GroupId = _options.GroupId,
            GenerationIdOrMemberEpoch = _generationId,
            MemberId = _memberId,
            GroupInstanceId = _options.GroupInstanceId,
            Topics = topicOffsets
        };

        // Use negotiated API version
        var offsetCommitVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            OffsetCommitRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            request,
            offsetCommitVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for errors
        foreach (var topic in response.Topics)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.ErrorCode != ErrorCode.None)
                {
                    throw new Errors.GroupException(partition.ErrorCode,
                        $"OffsetCommit failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}")
                    {
                        GroupId = _options.GroupId
                    };
                }
            }
        }
    }

    /// <summary>
    /// Fetches committed offsets for the group.
    /// </summary>
    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> FetchOffsetsAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.GroupId))
            return new Dictionary<TopicPartition, long>();

        var connection = await _connectionPool.GetConnectionAsync(_coordinatorId, cancellationToken)
            .ConfigureAwait(false);

        // Group partitions by topic without LINQ to avoid allocations
        Dictionary<string, List<int>>? topicGroups = null;
        foreach (var partition in partitions)
        {
            topicGroups ??= new Dictionary<string, List<int>>();
            if (!topicGroups.TryGetValue(partition.Topic, out var indexes))
            {
                indexes = new List<int>();
                topicGroups[partition.Topic] = indexes;
            }
            indexes.Add(partition.Partition);
        }

        var topicPartitions = new List<OffsetFetchRequestTopic>();
        if (topicGroups is not null)
        {
            foreach (var kvp in topicGroups)
            {
                topicPartitions.Add(new OffsetFetchRequestTopic
                {
                    Name = kvp.Key,
                    PartitionIndexes = kvp.Value
                });
            }
        }

        var request = new OffsetFetchRequest
        {
            GroupId = _options.GroupId,
            Topics = topicPartitions
        };

        // Use negotiated API version
        var offsetFetchVersion = _metadataManager.GetNegotiatedApiVersion(
            ApiKey.OffsetFetch,
            OffsetFetchRequest.LowestSupportedVersion,
            OffsetFetchRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            request,
            offsetFetchVersion,
            cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<TopicPartition, long>();

        // v0-v7: Topics field
        if (response.Topics is not null)
        {
            foreach (var topic in response.Topics)
            {
                foreach (var partition in topic.Partitions)
                {
                    if (partition.ErrorCode == ErrorCode.None && partition.CommittedOffset >= 0)
                    {
                        result[new TopicPartition(topic.Name, partition.PartitionIndex)] = partition.CommittedOffset;
                    }
                }
            }
        }

        // v8+: Groups field
        if (response.Groups is not null)
        {
            foreach (var group in response.Groups)
            {
                if (group.ErrorCode != ErrorCode.None)
                    continue;

                foreach (var topic in group.Topics)
                {
                    foreach (var partition in topic.Partitions)
                    {
                        if (partition.ErrorCode == ErrorCode.None && partition.CommittedOffset >= 0)
                        {
                            result[new TopicPartition(topic.Name, partition.PartitionIndex)] = partition.CommittedOffset;
                        }
                    }
                }
            }
        }

        return result;
    }

    private static byte[] BuildSubscriptionMetadata(IReadOnlySet<string> topics)
    {
        // Simple subscription format - convert set to list for writer
        var topicList = new List<string>(topics.Count);
        foreach (var topic in topics)
        {
            topicList.Add(topic);
        }

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Protocol.KafkaProtocolWriter(buffer);

        writer.WriteInt16(0); // Version
        writer.WriteArray(
            topicList,
            (ref Protocol.KafkaProtocolWriter w, string t) => w.WriteString(t));
        writer.WriteBytes([]); // User data

        return buffer.WrittenSpan.ToArray();
    }

    private static HashSet<TopicPartition> ParseAssignment(byte[] data)
    {
        if (data.Length == 0)
            return [];

        var result = new HashSet<TopicPartition>();
        var reader = new Protocol.KafkaProtocolReader(data);

        var version = reader.ReadInt16();
        var topics = reader.ReadArray((ref Protocol.KafkaProtocolReader r) =>
        {
            var topic = r.ReadString()!;
            var partitions = r.ReadArray((ref Protocol.KafkaProtocolReader r2) => r2.ReadInt32());
            return (topic, partitions);
        });

        foreach (var (topic, partitions) in topics)
        {
            foreach (var partition in partitions)
            {
                result.Add(new TopicPartition(topic, partition));
            }
        }

        return result;
    }

    private string GetAssignorName() => _options.PartitionAssignmentStrategy switch
    {
        PartitionAssignmentStrategy.Range => "range",
        PartitionAssignmentStrategy.RoundRobin => "roundrobin",
        PartitionAssignmentStrategy.Sticky => "sticky",
        PartitionAssignmentStrategy.CooperativeSticky => "cooperative-sticky",
        _ => "range"
    };

    private async ValueTask<SyncGroupRequestAssignment[]> ComputeAssignmentsAsync(
        IReadOnlySet<string> topics,
        IReadOnlyList<JoinGroupResponseMember> members,
        CancellationToken cancellationToken)
    {
        // Get partition info for all subscribed topics
        var topicPartitions = new Dictionary<string, int>(); // topic -> partition count
        foreach (var topic in topics)
        {
            var topicInfo = await _metadataManager.GetTopicMetadataAsync(topic, cancellationToken)
                .ConfigureAwait(false);
            if (topicInfo is not null && topicInfo.PartitionCount > 0)
            {
                topicPartitions[topic] = topicInfo.PartitionCount;
            }
        }

        // Parse each member's subscription
        var memberSubscriptions = new Dictionary<string, HashSet<string>>(); // memberId -> subscribed topics
        foreach (var member in members)
        {
            var subscribedTopics = ParseSubscriptionMetadata(member.Metadata);
            memberSubscriptions[member.MemberId] = subscribedTopics;
        }

        // Compute assignments using range assignor (simple per-topic partitioning)
        var memberAssignments = new Dictionary<string, List<TopicPartition>>();
        foreach (var member in members)
        {
            memberAssignments[member.MemberId] = [];
        }

        foreach (var (topic, partitionCount) in topicPartitions)
        {
            // Get members interested in this topic
            var interestedMembers = members
                .Where(m => memberSubscriptions[m.MemberId].Contains(topic))
                .Select(m => m.MemberId)
                .OrderBy(id => id) // Sort for deterministic assignment
                .ToList();

            if (interestedMembers.Count == 0)
                continue;

            // Range assignment: divide partitions evenly among interested members
            var partitionsPerMember = partitionCount / interestedMembers.Count;
            var extraPartitions = partitionCount % interestedMembers.Count;

            var partitionIndex = 0;
            for (var memberIdx = 0; memberIdx < interestedMembers.Count; memberIdx++)
            {
                var memberId = interestedMembers[memberIdx];
                var assignedCount = partitionsPerMember + (memberIdx < extraPartitions ? 1 : 0);

                for (var i = 0; i < assignedCount; i++)
                {
                    memberAssignments[memberId].Add(new TopicPartition(topic, partitionIndex++));
                }
            }
        }

        // Build SyncGroupRequestAssignment for each member
        var result = new List<SyncGroupRequestAssignment>();
        foreach (var (memberId, partitions) in memberAssignments)
        {
            var assignmentBytes = BuildAssignmentData(partitions);
            result.Add(new SyncGroupRequestAssignment
            {
                MemberId = memberId,
                Assignment = assignmentBytes
            });

            _logger?.LogDebug(
                "Assigned {Count} partitions to member {MemberId}: {Partitions}",
                partitions.Count, memberId, string.Join(", ", partitions));
        }

        return result.ToArray();
    }

    private static HashSet<string> ParseSubscriptionMetadata(byte[] data)
    {
        if (data.Length == 0)
            return [];

        var result = new HashSet<string>();
        var reader = new Protocol.KafkaProtocolReader(data);

        var version = reader.ReadInt16();
        var topics = reader.ReadArray((ref Protocol.KafkaProtocolReader r) => r.ReadString()!);

        foreach (var topic in topics)
        {
            result.Add(topic);
        }

        return result;
    }

    private static byte[] BuildAssignmentData(List<TopicPartition> partitions)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Protocol.KafkaProtocolWriter(buffer);

        // Group partitions by topic without LINQ
        var byTopic = new Dictionary<string, List<int>>();
        foreach (var p in partitions)
        {
            if (!byTopic.TryGetValue(p.Topic, out var list))
            {
                list = new List<int>();
                byTopic[p.Topic] = list;
            }
            list.Add(p.Partition);
        }

        // Convert to list for the writer
        var topicAssignments = new List<(string Topic, List<int> Partitions)>(byTopic.Count);
        foreach (var kvp in byTopic)
        {
            topicAssignments.Add((kvp.Key, kvp.Value));
        }

        // Write assignment format
        writer.WriteInt16(0); // Version

        // Write topics array
        writer.WriteArray(
            topicAssignments,
            (ref Protocol.KafkaProtocolWriter w, (string Topic, List<int> Partitions) tp) =>
            {
                w.WriteString(tp.Topic); // Topic name
                w.WriteArray(
                    tp.Partitions,
                    (ref Protocol.KafkaProtocolWriter w2, int partition) => w2.WriteInt32(partition));
            });

        writer.WriteBytes([]); // User data

        return buffer.WrittenSpan.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        _heartbeatCts?.Cancel();

        if (_heartbeatTask is not null)
        {
            try
            {
                await _heartbeatTask.ConfigureAwait(false);
            }
            catch
            {
                // Ignore
            }
        }

        _heartbeatCts?.Dispose();
        _lock.Dispose();
    }
}

/// <summary>
/// Consumer group coordinator state.
/// </summary>
public enum CoordinatorState
{
    Unjoined,
    Joining,
    Syncing,
    Stable
}
