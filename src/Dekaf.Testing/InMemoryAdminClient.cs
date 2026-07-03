using Dekaf.Admin;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Telemetry;

namespace Dekaf.Testing;

/// <summary>
/// In-memory <see cref="IAdminClient"/> for common topic and group-offset test operations.
/// </summary>
public sealed class InMemoryAdminClient : IAdminClient
{
    private static readonly TimeSpan DefaultDelegationTokenLifetime = TimeSpan.FromHours(24);

    private readonly InMemoryKafkaCluster _cluster;
    private readonly Dictionary<ClientQuotaEntity, Dictionary<string, double>> _clientQuotas = new();
    private readonly object _delegationTokenGate = new();
    private readonly Dictionary<string, DelegationToken> _delegationTokens = new(StringComparer.Ordinal);
    private bool _disposed;

    public InMemoryAdminClient(InMemoryKafkaCluster cluster)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
    }

    public ClusterMetadata Metadata { get; } = new();

    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        ThrowIfDisposed();
    }

    public void UnregisterMetricFromSubscription(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ThrowIfDisposed();
    }

    public ValueTask CreateTopicsAsync(
        IEnumerable<NewTopic> topics,
        CreateTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topics);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (options?.ValidateOnly == true)
            return ValueTask.CompletedTask;

        foreach (var topic in topics)
            _cluster.CreateTopic(topic.Name, topic.NumPartitions, topic.Configs);

        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteTopicsAsync(
        IEnumerable<string> topicNames,
        DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicNames);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        foreach (var topicName in topicNames)
            _cluster.DeleteTopic(topicName);

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<TopicListing>> ListTopicsAsync(
        ListTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult(_cluster.TopicListings(options?.ListInternal == true));
    }

    public ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicsAsync(
        IEnumerable<string> topicNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicNames);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult(_cluster.DescribeTopics(topicNames));
    }

    public ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicPartitionsAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return DescribeTopicsAsync(topicNames, cancellationToken);
    }

    public ValueTask<DescribeTopicPartitionsPage> DescribeTopicPartitionsPageAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsPageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicNames);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        return ValueTask.FromResult(new DescribeTopicPartitionsPage
        {
            Topics = _cluster.DescribeTopics(topicNames),
            NextCursor = null
        });
    }

    public ValueTask<ClusterDescription> DescribeClusterAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        return ValueTask.FromResult(new ClusterDescription
        {
            ClusterId = _cluster.Options.ClusterId,
            ControllerId = 0,
            Nodes =
            [
                new BrokerNode
                {
                    NodeId = 0,
                    Host = "in-memory",
                    Port = 0
                }
            ]
        });
    }

    public ValueTask<IReadOnlyDictionary<string, GroupDescription>> DescribeConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = groupIds.ToDictionary(
            groupId => groupId,
            groupId => new GroupDescription
            {
                GroupId = groupId,
                ProtocolType = "consumer",
                State = "Stable",
                Members = []
            },
            StringComparer.Ordinal);

        return ValueTask.FromResult<IReadOnlyDictionary<string, GroupDescription>>(result);
    }

    public ValueTask<IReadOnlyList<GroupListing>> ListConsumerGroupsAsync(
        ListConsumerGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        IReadOnlyList<GroupListing> result = _cluster.ListGroups()
            .Select(groupId => new GroupListing
            {
                GroupId = groupId,
                ProtocolType = "consumer",
                State = "Stable"
            })
            .ToArray();

        return ValueTask.FromResult(result);
    }

    public ValueTask DeleteConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        foreach (var groupId in groupIds)
            _cluster.DeleteGroup(groupId);

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, long>> ListConsumerGroupOffsetsAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult(_cluster.GetGroupOffsets(groupId));
    }

    public ValueTask AlterConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        _cluster.CommitOffsets(groupId, offsets);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, long>> DeleteRecordsAsync(
        IReadOnlyDictionary<TopicPartition, long> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult(_cluster.DeleteRecords(offsets));
    }

    public ValueTask CreatePartitionsAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPartitionCounts);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        foreach (var (topicName, partitionCount) in newPartitionCounts)
            _cluster.CreatePartitions(topicName, partitionCount);

        return ValueTask.CompletedTask;
    }

    public ValueTask AlterPartitionReassignmentsAsync(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reassignments);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        foreach (var (topicPartition, reassignment) in reassignments)
        {
            ValidateTopicPartition(topicPartition);
            if (reassignment.HasValue)
            {
                foreach (var replica in reassignment.Value.TargetReplicas)
                    ArgumentOutOfRangeException.ThrowIfNegative(replica);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, PartitionReassignment>> ListPartitionReassignmentsAsync(
        IEnumerable<TopicPartition>? partitions = null,
        ListPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (partitions is not null)
        {
            foreach (var partition in partitions)
                ValidateTopicPartition(partition);
        }

        return ValueTask.FromResult<IReadOnlyDictionary<TopicPartition, PartitionReassignment>>(
            new Dictionary<TopicPartition, PartitionReassignment>());
    }

    public ValueTask<IReadOnlyDictionary<string, IReadOnlyList<ScramCredentialInfo>>> DescribeUserScramCredentialsAsync(
        IEnumerable<string>? users = null,
        DescribeUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = (users ?? []).ToDictionary(
            user => user,
            _ => (IReadOnlyList<ScramCredentialInfo>)Array.Empty<ScramCredentialInfo>(),
            StringComparer.Ordinal);

        return ValueTask.FromResult<IReadOnlyDictionary<string, IReadOnlyList<ScramCredentialInfo>>>(result);
    }

    public ValueTask AlterUserScramCredentialsAsync(
        IEnumerable<UserScramCredentialAlteration> alterations,
        AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alterations);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        _ = alterations.Count();
        return ValueTask.CompletedTask;
    }

    public ValueTask<DelegationToken> CreateDelegationTokenAsync(
        DelegationTokenPrincipal? owner = null,
        IEnumerable<DelegationTokenPrincipal>? renewers = null,
        TimeSpan? maxLifetime = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var lifetime = ValidateDelegationTokenDuration(
            maxLifetime,
            nameof(maxLifetime),
            DefaultDelegationTokenLifetime);
        var now = DateTimeOffset.UtcNow;
        var maxTimestamp = now + lifetime;
        var hmac = Guid.NewGuid().ToByteArray();
        var token = new DelegationToken
        {
            Owner = owner ?? new DelegationTokenPrincipal("User", "in-memory"),
            TokenRequester = new DelegationTokenPrincipal("User", "in-memory"),
            IssueTimestamp = now,
            ExpiryTimestamp = maxTimestamp,
            MaxTimestamp = maxTimestamp,
            TokenId = Guid.NewGuid().ToString("N"),
            Hmac = hmac,
            Renewers = renewers?.ToArray() ?? []
        };

        lock (_delegationTokenGate)
            _delegationTokens[DelegationTokenKey(hmac)] = CloneDelegationToken(token);

        return ValueTask.FromResult(token);
    }

    public ValueTask<DateTimeOffset> RenewDelegationTokenAsync(
        byte[] hmac,
        TimeSpan? renewPeriod = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hmac);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var period = ValidateDelegationTokenDuration(
            renewPeriod,
            nameof(renewPeriod),
            DefaultDelegationTokenLifetime);
        var key = DelegationTokenKey(hmac);

        return ValueTask.FromResult(UpdateDelegationTokenExpiry(key, period));
    }

    public ValueTask<DateTimeOffset> ExpireDelegationTokenAsync(
        byte[] hmac,
        TimeSpan? expiryTimePeriod = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hmac);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var period = ValidateDelegationTokenDuration(
            expiryTimePeriod,
            nameof(expiryTimePeriod),
            TimeSpan.Zero);
        var key = DelegationTokenKey(hmac);

        return ValueTask.FromResult(UpdateDelegationTokenExpiry(key, period));
    }

    public ValueTask<IReadOnlyList<DelegationToken>> DescribeDelegationTokensAsync(
        IEnumerable<DelegationTokenPrincipal>? owners = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var ownerFilter = owners?.ToHashSet();
        lock (_delegationTokenGate)
        {
            IReadOnlyList<DelegationToken> tokens = _delegationTokens.Values
                .Where(token => ownerFilter is null || ownerFilter.Contains(token.Owner))
                .Select(token => CloneDelegationToken(token))
                .ToArray();

            return ValueTask.FromResult(tokens);
        }
    }

    public ValueTask<IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>>> DescribeConfigsAsync(
        IEnumerable<ConfigResource> resources,
        DescribeConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resources);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = resources.ToDictionary(
            resource => resource,
            _ => (IReadOnlyList<ConfigEntry>)Array.Empty<ConfigEntry>());

        return ValueTask.FromResult<IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>>>(result);
    }

    public ValueTask AlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs,
        AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ValueTask IncrementalAlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs,
        IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ValueTask CreateAclsAsync(
        IEnumerable<AclBinding> aclBindings,
        CreateAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aclBindings);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<AclBinding>> DeleteAclsAsync(
        IEnumerable<AclBindingFilter> filters,
        DeleteAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult<IReadOnlyList<AclBinding>>(Array.Empty<AclBinding>());
    }

    public ValueTask<IReadOnlyList<AclBinding>> DescribeAclsAsync(
        AclBindingFilter filter,
        DescribeAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        return ValueTask.FromResult<IReadOnlyList<AclBinding>>(Array.Empty<AclBinding>());
    }

    public ValueTask DeleteConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition> partitions,
        DeleteConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(partitions);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        _cluster.DeleteGroupOffsets(groupId, partitions);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>> ListOffsetsAsync(
        IEnumerable<TopicPartitionOffsetSpec> specs,
        ListOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = specs.ToDictionary(
            spec => spec.TopicPartition,
            spec =>
            {
                var offset = spec.Spec switch
                {
                    OffsetSpec.Earliest => _cluster.GetWatermarks(spec.TopicPartition).Low,
                    OffsetSpec.Latest or OffsetSpec.MaxTimestamp => _cluster.GetWatermarks(spec.TopicPartition).High,
                    OffsetSpec.Timestamp => _cluster.GetOffsetForTimestamp(spec.TopicPartition, spec.Timestamp ?? 0),
                    _ => -1
                };

                return new ListOffsetsResultInfo
                {
                    Offset = offset,
                    Timestamp = spec.Spec == OffsetSpec.Timestamp ? spec.Timestamp ?? -1 : -1,
                    LeaderEpoch = 0
                };
            });

        return ValueTask.FromResult<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>>(result);
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, ElectLeadersResultInfo>> ElectLeadersAsync(
        ElectionType electionType,
        IEnumerable<TopicPartition>? partitions = null,
        ElectLeadersOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = (partitions ?? []).ToDictionary(
            partition => partition,
            partition => new ElectLeadersResultInfo
            {
                TopicPartition = partition,
                ErrorCode = ErrorCode.None
            });

        return ValueTask.FromResult<IReadOnlyDictionary<TopicPartition, ElectLeadersResultInfo>>(result);
    }

    public ValueTask<IReadOnlyDictionary<ClientQuotaEntity, IReadOnlyDictionary<string, double>>> DescribeClientQuotasAsync(
        ClientQuotaFilter filter,
        DescribeClientQuotasOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (filter.Components is null)
            throw new ArgumentException("Client quota filter components must not be null.", nameof(filter));

        foreach (var component in filter.Components)
        {
            ArgumentNullException.ThrowIfNull(component);
            component.Validate();
        }

        Dictionary<ClientQuotaEntity, IReadOnlyDictionary<string, double>> result;
        lock (_clientQuotas)
        {
            result = _clientQuotas
                .Where(quota => MatchesClientQuotaFilter(quota.Key, filter))
                .ToDictionary(
                    quota => CloneClientQuotaEntity(quota.Key),
                    quota => (IReadOnlyDictionary<string, double>)new Dictionary<string, double>(quota.Value, StringComparer.Ordinal));
        }

        return ValueTask.FromResult<IReadOnlyDictionary<ClientQuotaEntity, IReadOnlyDictionary<string, double>>>(result);
    }

    public ValueTask AlterClientQuotasAsync(
        IEnumerable<ClientQuotaAlteration> alterations,
        AlterClientQuotasOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alterations);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var materialized = alterations.Select(alteration =>
        {
            ArgumentNullException.ThrowIfNull(alteration);
            alteration.Validate();
            return alteration;
        }).ToArray();

        if (options?.ValidateOnly == true)
            return ValueTask.CompletedTask;

        lock (_clientQuotas)
        {
            foreach (var alteration in materialized)
                ApplyClientQuotaAlteration(alteration);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<ListTransactionsResult> ListTransactionsAsync(
        ListTransactionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        return ValueTask.FromResult(new ListTransactionsResult
        {
            UnknownStateFilters = [],
            Transactions = []
        });
    }

    public ValueTask<IReadOnlyDictionary<string, TransactionDescription>> DescribeTransactionsAsync(
        IEnumerable<string> transactionalIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionalIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = transactionalIds.ToDictionary(
            transactionalId => transactionalId,
            transactionalId => new TransactionDescription
            {
                TransactionalId = transactionalId,
                ErrorCode = ErrorCode.TransactionalIdNotFound,
                TransactionState = "Unknown",
                TransactionTimeoutMs = 0,
                TransactionStartTimeMs = -1,
                ProducerId = -1,
                ProducerEpoch = -1,
                CoordinatorId = 0,
                TopicPartitions = []
            },
            StringComparer.Ordinal);

        return ValueTask.FromResult<IReadOnlyDictionary<string, TransactionDescription>>(result);
    }

    public ValueTask<IReadOnlyDictionary<TopicPartition, DescribeProducersResultInfo>> DescribeProducersAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = partitions
            .Select(partition =>
            {
                ValidateTopicPartition(partition);
                return partition;
            })
            .ToDictionary(
                partition => partition,
                partition => new DescribeProducersResultInfo
                {
                    TopicPartition = partition,
                    ErrorCode = ErrorCode.None,
                    ActiveProducers = []
                });

        return ValueTask.FromResult<IReadOnlyDictionary<TopicPartition, DescribeProducersResultInfo>>(result);
    }

    public ValueTask<IReadOnlyDictionary<int, IReadOnlyDictionary<string, LogDirDescription>>> DescribeLogDirsAsync(
        IEnumerable<int> brokerIds,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brokerIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var targetPartitions = partitions?.Distinct().ToArray() ??
            _cluster.ListTopics().SelectMany(_cluster.GetTopicPartitions).ToArray();
        foreach (var partition in targetPartitions)
            ValidateTopicPartition(partition);

        var result = new Dictionary<int, IReadOnlyDictionary<string, LogDirDescription>>();
        foreach (var brokerId in brokerIds.Distinct().Order())
        {
            ArgumentOutOfRangeException.ThrowIfNegative(brokerId);

            if (brokerId != 0)
            {
                result[brokerId] = new Dictionary<string, LogDirDescription>();
                continue;
            }

            var replicas = targetPartitions.ToDictionary(
                partition => partition,
                partition =>
                {
                    var watermarks = _cluster.GetWatermarks(partition);
                    return new ReplicaLogDirInfo
                    {
                        Size = Math.Max(0, watermarks.High - watermarks.Low),
                        OffsetLag = 0,
                        IsFuture = false
                    };
                });

            result[brokerId] = new Dictionary<string, LogDirDescription>(StringComparer.Ordinal)
            {
                ["in-memory"] = new()
                {
                    ErrorCode = ErrorCode.None,
                    ReplicaInfos = replicas
                }
            };
        }

        return ValueTask.FromResult<IReadOnlyDictionary<int, IReadOnlyDictionary<string, LogDirDescription>>>(result);
    }

    public ValueTask<IReadOnlyDictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>> AlterReplicaLogDirsAsync(
        IReadOnlyDictionary<TopicPartitionReplica, string> replicaAssignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replicaAssignments);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = new Dictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>(replicaAssignments.Count);
        foreach (var (replica, logDir) in replicaAssignments)
        {
            ValidateTopicPartition(replica.TopicPartition);
            ArgumentOutOfRangeException.ThrowIfNegative(replica.BrokerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(logDir);

            result[replica] = new AlterReplicaLogDirResultInfo
            {
                TopicPartitionReplica = replica,
                ErrorCode = ErrorCode.None
            };
        }

        return ValueTask.FromResult<IReadOnlyDictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>>(result);
    }

    public ValueTask<IReadOnlyDictionary<string, ShareGroupDescription>> DescribeShareGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var result = groupIds.ToDictionary(
            groupId => groupId,
            groupId => new ShareGroupDescription
            {
                GroupId = groupId,
                GroupState = "Stable",
                Members = []
            },
            StringComparer.Ordinal);

        return ValueTask.FromResult<IReadOnlyDictionary<string, ShareGroupDescription>>(result);
    }

    public ValueTask<IReadOnlyList<GroupListing>> ListShareGroupsAsync(
        ListShareGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return ListConsumerGroupsAsync(null, cancellationToken);
    }

    public ValueTask<IReadOnlyList<ShareGroupOffsetDescription>> DescribeShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var groupOffsets = _cluster.GetGroupOffsets(groupId);
        var targetPartitions = partitions?.ToArray() ?? groupOffsets.Keys.ToArray();
        var result = targetPartitions
            .Select(partition =>
            {
                var offset = groupOffsets.GetValueOrDefault(partition);
                var high = _cluster.GetWatermarks(partition).High;
                return new ShareGroupOffsetDescription
                {
                    TopicPartition = partition,
                    StartOffset = offset,
                    LeaderEpoch = 0,
                    Lag = Math.Max(0, high - offset),
                    ErrorCode = ErrorCode.None
                };
            })
            .ToArray();

        return ValueTask.FromResult<IReadOnlyList<ShareGroupOffsetDescription>>(result);
    }

    public ValueTask AlterShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<ShareGroupOffsetAlteration> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        _cluster.CommitOffsets(
            groupId,
            offsets.Select(offset => new TopicPartitionOffset(
                offset.TopicPartition.Topic,
                offset.TopicPartition.Partition,
                offset.StartOffset)));

        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<string> topics,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(topics);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var topicSet = topics.ToHashSet(StringComparer.Ordinal);
        var partitions = _cluster.GetGroupOffsets(groupId)
            .Keys
            .Where(partition => topicSet.Contains(partition.Topic))
            .ToArray();

        _cluster.DeleteGroupOffsets(groupId, partitions);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void ApplyClientQuotaAlteration(ClientQuotaAlteration alteration)
    {
        if (!_clientQuotas.TryGetValue(alteration.Entity, out var quotas))
        {
            if (alteration.Operations.All(operation => operation.Remove))
                return;

            quotas = new Dictionary<string, double>(StringComparer.Ordinal);
            _clientQuotas[CloneClientQuotaEntity(alteration.Entity)] = quotas;
        }

        foreach (var operation in alteration.Operations)
        {
            if (operation.Remove)
            {
                quotas.Remove(operation.Key);
            }
            else
            {
                quotas[operation.Key] = operation.Value;
            }
        }

        if (quotas.Count == 0)
            _clientQuotas.Remove(alteration.Entity);
    }

    private static bool MatchesClientQuotaFilter(ClientQuotaEntity entity, ClientQuotaFilter filter)
    {
        if (filter.Strict && entity.Components.Count != filter.Components.Count)
            return false;

        return filter.Components.All(component => MatchesClientQuotaFilterComponent(entity, component));
    }

    private static bool MatchesClientQuotaFilterComponent(ClientQuotaEntity entity, ClientQuotaFilterComponent filterComponent) =>
        entity.Components.Any(component =>
            component.EntityType == filterComponent.EntityType &&
            filterComponent.MatchType switch
            {
                ClientQuotaMatchType.Exact => component.Name == filterComponent.Match,
                ClientQuotaMatchType.Default => component.Name is null,
                ClientQuotaMatchType.AnySpecified => component.Name is not null,
                _ => false
            });

    private static ClientQuotaEntity CloneClientQuotaEntity(ClientQuotaEntity entity) => new()
    {
        Components = entity.Components
            .Select(component => new ClientQuotaEntityComponent
            {
                EntityType = component.EntityType,
                Name = component.Name
            })
            .ToArray()
    };

    private static void ValidateTopicPartition(TopicPartition topicPartition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicPartition.Topic);
        ArgumentOutOfRangeException.ThrowIfNegative(topicPartition.Partition);
    }

    private static TimeSpan ValidateDelegationTokenDuration(
        TimeSpan? value,
        string parameterName,
        TimeSpan defaultValue)
    {
        if (value is null)
            return defaultValue;

        if (value.Value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(parameterName, "Duration cannot be negative.");

        return value.Value;
    }

    private DateTimeOffset UpdateDelegationTokenExpiry(string key, TimeSpan period)
    {
        lock (_delegationTokenGate)
        {
            if (!_delegationTokens.TryGetValue(key, out var token))
                throw new InvalidOperationException("Delegation token was not found.");

            var expiry = DateTimeOffset.UtcNow + period;
            if (expiry > token.MaxTimestamp)
                expiry = token.MaxTimestamp;

            _delegationTokens[key] = CloneDelegationToken(token, expiry);
            return expiry;
        }
    }

    private static string DelegationTokenKey(byte[] hmac) => Convert.ToBase64String(hmac);

    private static DelegationToken CloneDelegationToken(
        DelegationToken token,
        DateTimeOffset? expiryTimestamp = null)
    {
        return new DelegationToken
        {
            Owner = token.Owner,
            TokenRequester = token.TokenRequester,
            IssueTimestamp = token.IssueTimestamp,
            ExpiryTimestamp = expiryTimestamp ?? token.ExpiryTimestamp,
            MaxTimestamp = token.MaxTimestamp,
            TokenId = token.TokenId,
            Hmac = token.Hmac.ToArray(),
            Renewers = token.Renewers.ToArray()
        };
    }
}
