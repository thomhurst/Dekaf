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
    private HashSet<TopicPartition> _assignedPartitions = [];
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;

    private CoordinatorState _state = CoordinatorState.Unjoined;
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

            // Find coordinator
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
        finally
        {
            _lock.Release();
        }
    }

    private async ValueTask FindCoordinatorAsync(CancellationToken cancellationToken)
    {
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken)
            .ConfigureAwait(false);

        var request = new FindCoordinatorRequest
        {
            Key = _options.GroupId!,
            KeyType = CoordinatorType.Group
        };

        var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            request,
            FindCoordinatorRequest.HighestSupportedVersion,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != ErrorCode.None)
        {
            throw new Errors.GroupException(response.ErrorCode, $"FindCoordinator failed: {response.ErrorCode}")
            {
                GroupId = _options.GroupId
            };
        }

        _coordinatorId = response.NodeId;
        _connectionPool.RegisterBroker(response.NodeId, response.Host!, response.Port);

        _logger?.LogDebug("Found coordinator {NodeId} for group {GroupId}", _coordinatorId, _options.GroupId);
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

        var response = await connection.SendAsync<JoinGroupRequest, JoinGroupResponse>(
            request,
            JoinGroupRequest.HighestSupportedVersion,
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

        // TODO: If leader, compute assignments using assignor

        var request = new SyncGroupRequest
        {
            GroupId = _options.GroupId!,
            GenerationId = _generationId,
            MemberId = _memberId!,
            GroupInstanceId = _options.GroupInstanceId,
            Assignments = assignments
        };

        var response = await connection.SendAsync<SyncGroupRequest, SyncGroupResponse>(
            request,
            SyncGroupRequest.HighestSupportedVersion,
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

                if (ex is Errors.GroupException ge &&
                    (ge.ErrorCode == ErrorCode.RebalanceInProgress ||
                     ge.ErrorCode == ErrorCode.UnknownMemberId ||
                     ge.ErrorCode == ErrorCode.IllegalGeneration))
                {
                    _state = CoordinatorState.Unjoined;
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

        var response = await connection.SendAsync<HeartbeatRequest, HeartbeatResponse>(
            request,
            HeartbeatRequest.HighestSupportedVersion,
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

        var topicOffsets = offsets.GroupBy(o => o.Topic).Select(g => new OffsetCommitRequestTopic
        {
            Name = g.Key,
            Partitions = g.Select(o => new OffsetCommitRequestPartition
            {
                PartitionIndex = o.Partition,
                CommittedOffset = o.Offset
            }).ToList()
        }).ToList();

        var request = new OffsetCommitRequest
        {
            GroupId = _options.GroupId,
            GenerationIdOrMemberEpoch = _generationId,
            MemberId = _memberId,
            GroupInstanceId = _options.GroupInstanceId,
            Topics = topicOffsets
        };

        var response = await connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            request,
            OffsetCommitRequest.HighestSupportedVersion,
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

        var topicPartitions = partitions.GroupBy(p => p.Topic).Select(g => new OffsetFetchRequestTopic
        {
            Name = g.Key,
            PartitionIndexes = g.Select(p => p.Partition).ToList()
        }).ToList();

        var request = new OffsetFetchRequest
        {
            GroupId = _options.GroupId,
            Topics = topicPartitions
        };

        var response = await connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            request,
            OffsetFetchRequest.HighestSupportedVersion,
            cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<TopicPartition, long>();

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

        return result;
    }

    private static byte[] BuildSubscriptionMetadata(IReadOnlySet<string> topics)
    {
        // Simple subscription format
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Protocol.KafkaProtocolWriter(buffer);

        writer.WriteInt16(0); // Version
        writer.WriteArray(
            topics.ToArray().AsSpan(),
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
