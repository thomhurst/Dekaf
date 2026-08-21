using Dekaf.Admin;
using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Protocol;
using Dekaf.Telemetry;

namespace Dekaf.Testing;

/// <summary>
/// In-memory <see cref="IAdminClient"/> for common topic and group-offset test operations.
/// </summary>
public sealed class InMemoryAdminClient :
    IAdminClient,
    IReplicaLogDirAdminClient,
    ITopicIdAdminClient,
    ITransactionRemediationAdminClient,
    IShareGroupDeletionAdminClient
{
    private static readonly TimeSpan DefaultDelegationTokenLifetime = TimeSpan.FromHours(24);

    private readonly InMemoryKafkaCluster _cluster;
    private readonly Dictionary<ClientQuotaEntity, Dictionary<string, double>> _clientQuotas = new();
    private readonly object _delegationTokenGate = new();
    private readonly Dictionary<string, DelegationToken> _delegationTokens = new(StringComparer.Ordinal);
    private readonly object _featureGate = new();
    private readonly Dictionary<string, FeatureVersionRange> _supportedFeatures;
    private readonly Dictionary<string, FeatureVersionRange> _finalizedFeatures = new(StringComparer.Ordinal);
    private long _finalizedFeaturesEpoch = -1;
    private bool _disposed;

    public InMemoryAdminClient(InMemoryKafkaCluster cluster)
    {
        _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
        _supportedFeatures = new Dictionary<string, FeatureVersionRange>(
            cluster.Options.SupportedFeatures,
            StringComparer.Ordinal);
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

    public async ValueTask<IReadOnlyList<ClientMetricsResourceListing>> ListClientMetricsResourcesAsync(
        ListClientMetricsResourcesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        return Array.Empty<ClientMetricsResourceListing>();
    }

    public async ValueTask<IReadOnlyList<ConfigResourceListing>> ListConfigResourcesAsync(
        ListConfigResourcesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        return Array.Empty<ConfigResourceListing>();
    }

    public async ValueTask CreateTopicsAsync(
        IEnumerable<NewTopic> topics,
        CreateTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topics);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var topicList = topics.ToArray();
        if (topicList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < topicList.Length; index++)
        {
            var topic = topicList[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(topic.Name);
            ArgumentOutOfRangeException.ThrowIfLessThan(topic.NumPartitions, 1);
            await ApplyAdminFaultAsync(
                cancellationToken,
                topic: topic.Name).ConfigureAwait(false);

            if (options?.ValidateOnly != true)
                _cluster.CreateTopic(topic.Name, topic.NumPartitions, topic.Configs);
        }
    }

    public async ValueTask DeleteTopicsAsync(
        IEnumerable<string> topicNames,
        DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicNames);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var names = topicNames.ToArray();
        for (var index = 0; index < names.Length; index++)
        {
            var topicName = names[index];
            await ApplyAdminFaultAsync(
                cancellationToken,
                topic: topicName).ConfigureAwait(false);
            _cluster.DeleteTopic(topicName);
        }
    }

    public async ValueTask DeleteTopicsAsync(
        IEnumerable<Guid> topicIds,
        DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var ids = topicIds.Distinct().ToArray();
        foreach (var topicId in ids)
            if (topicId == Guid.Empty)
                throw new ArgumentException("Topic IDs cannot contain the empty UUID.", nameof(topicIds));

        foreach (var topicId in ids)
        {
            await ApplyAdminFaultAsync(
                cancellationToken,
                topic: _cluster.GetTopicName(topicId)).ConfigureAwait(false);
            if (!_cluster.DeleteTopic(topicId))
                throw new KafkaException(ErrorCode.UnknownTopicId, $"Topic ID '{topicId}' does not exist.");
        }
    }

    public async ValueTask<IReadOnlyList<TopicListing>> ListTopicsAsync(
        ListTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        return _cluster.TopicListings(options?.ListInternal == true);
    }

    public async ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicsAsync(
        IEnumerable<string> topicNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicNames);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var names = topicNames.ToArray();
        for (var index = 0; index < names.Length; index++)
            await ApplyAdminFaultAsync(cancellationToken, topic: names[index]).ConfigureAwait(false);
        return _cluster.DescribeTopics(names);
    }

    public async ValueTask<IReadOnlyDictionary<Guid, TopicDescription>> DescribeTopicsAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var ids = topicIds.Distinct().ToArray();
        for (var index = 0; index < ids.Length; index++)
        {
            await ApplyAdminFaultAsync(
                cancellationToken,
                topic: _cluster.GetTopicName(ids[index])).ConfigureAwait(false);
        }
        return _cluster.DescribeTopics(ids);
    }

    public ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicPartitionsAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return DescribeTopicsAsync(topicNames, cancellationToken);
    }

    public async ValueTask<DescribeTopicPartitionsPage> DescribeTopicPartitionsPageAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsPageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topicNames);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var names = topicNames.ToArray();
        for (var index = 0; index < names.Length; index++)
            await ApplyAdminFaultAsync(cancellationToken, topic: names[index]).ConfigureAwait(false);
        return new DescribeTopicPartitionsPage
        {
            Topics = _cluster.DescribeTopics(names),
            NextCursor = null
        };
    }

    public async ValueTask<ClusterDescription> DescribeClusterAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        return new ClusterDescription
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
        };
    }

    public async ValueTask<FeatureMetadata> DescribeFeaturesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        lock (_featureGate)
        {
            return new FeatureMetadata
            {
                FinalizedFeaturesEpoch = _finalizedFeaturesEpoch,
                SupportedFeatures = new Dictionary<string, FeatureVersionRange>(
                    _supportedFeatures,
                    StringComparer.Ordinal),
                FinalizedFeatures = new Dictionary<string, FeatureVersionRange>(
                    _finalizedFeatures,
                    StringComparer.Ordinal)
            };
        }
    }

    public async ValueTask<IReadOnlyDictionary<string, FeatureUpdateResultInfo>> UpdateFeaturesAsync(
        IReadOnlyDictionary<string, FeatureUpdate> updates,
        UpdateFeaturesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var updateList = updates.ToArray();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        if (options?.ValidateOnly != true)
        {
            lock (_featureGate)
            {
                foreach (var update in updateList)
                {
                    _finalizedFeatures[update.Key] = new FeatureVersionRange(
                        update.Value.MaxVersionLevel,
                        update.Value.MaxVersionLevel);
                }

                _finalizedFeaturesEpoch++;
            }
        }

        return updateList.ToDictionary(
            static update => update.Key,
            static _ => new FeatureUpdateResultInfo(),
            StringComparer.Ordinal);
    }

    public async ValueTask<IReadOnlyDictionary<string, GroupDescription>> DescribeConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var groups = groupIds.ToArray();
        for (var index = 0; index < groups.Length; index++)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groups[index]).ConfigureAwait(false);
        var result = groups.ToDictionary(
            groupId => groupId,
            groupId => new GroupDescription
            {
                GroupId = groupId,
                ProtocolType = "consumer",
                State = "Stable",
                Members = []
            },
            StringComparer.Ordinal);

        return result;
    }

    public async ValueTask<IReadOnlyList<GroupListing>> ListConsumerGroupsAsync(
        ListConsumerGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<GroupListing> result = _cluster.ListGroups()
            .Select(groupId => new GroupListing
            {
                GroupId = groupId,
                ProtocolType = "consumer",
                State = "Stable"
            })
            .ToArray();

        return result;
    }

    public async ValueTask DeleteConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var groups = groupIds.ToArray();
        for (var index = 0; index < groups.Length; index++)
        {
            var groupId = groups[index];
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
            _cluster.DeleteGroup(groupId);
        }
    }

    public async ValueTask<RemoveMembersFromConsumerGroupResult> RemoveMembersFromConsumerGroupAsync(
        string groupId,
        IEnumerable<ConsumerGroupMemberToRemove> members,
        RemoveMembersFromConsumerGroupOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(members);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var memberList = members.ToArray();
        await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);

        var results = memberList.Select(member => new ConsumerGroupMemberRemovalResult
        {
            GroupInstanceId = member.GroupInstanceId,
            MemberId = string.Empty,
            ErrorCode = Protocol.ErrorCode.None
        }).ToArray();

        return new RemoveMembersFromConsumerGroupResult
        {
            GroupId = groupId,
            Members = results
        };
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> ListConsumerGroupOffsetsAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
        return _cluster.GetGroupOffsets(groupId);
    }

    public async ValueTask AlterConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var offsetList = offsets.ToArray();
        if (offsetList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
        for (var index = 0; index < offsetList.Length; index++)
        {
            var offset = offsetList[index];
            await ApplyAdminFaultAsync(
                cancellationToken,
                offset.Topic,
                offset.Partition,
                groupId).ConfigureAwait(false);
            _cluster.CommitOffsets(groupId, [offset]);
        }
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> DeleteRecordsAsync(
        IReadOnlyDictionary<TopicPartition, long> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var offsetSnapshot = new Dictionary<TopicPartition, long>(offsets);
        var result = new Dictionary<TopicPartition, long>(offsetSnapshot.Count);
        foreach (var (partition, offset) in offsetSnapshot)
        {
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition).ConfigureAwait(false);
            var deleted = _cluster.DeleteRecords(
                new Dictionary<TopicPartition, long>(1) { [partition] = offset });
            result[partition] = deleted[partition];
        }

        return result;
    }

    public async ValueTask CreatePartitionsAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(newPartitionCounts);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var partitionCounts = newPartitionCounts.ToArray();
        foreach (var (topicName, partitionCount) in partitionCounts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentOutOfRangeException.ThrowIfLessThan(partitionCount, 1);
            await ApplyAdminFaultAsync(cancellationToken, topic: topicName).ConfigureAwait(false);
            _cluster.CreatePartitions(topicName, partitionCount);
        }
    }

    public async ValueTask AlterPartitionReassignmentsAsync(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reassignments);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var reassignmentList = reassignments.ToArray();
        foreach (var (topicPartition, reassignment) in reassignmentList)
        {
            ValidateTopicPartition(topicPartition);
            if (reassignment.HasValue)
            {
                foreach (var replica in reassignment.Value.TargetReplicas)
                    ArgumentOutOfRangeException.ThrowIfNegative(replica);
            }

            await ApplyAdminFaultAsync(
                cancellationToken,
                topicPartition.Topic,
                topicPartition.Partition).ConfigureAwait(false);
        }
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, PartitionReassignment>> ListPartitionReassignmentsAsync(
        IEnumerable<TopicPartition>? partitions = null,
        ListPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var partitionList = partitions?.ToArray();
        if (partitionList is not null)
        {
            foreach (var partition in partitionList)
            {
                ValidateTopicPartition(partition);
                await ApplyAdminFaultAsync(
                    cancellationToken,
                    partition.Topic,
                    partition.Partition).ConfigureAwait(false);
            }
        }
        else
        {
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        }

        return new Dictionary<TopicPartition, PartitionReassignment>();
    }

    public async ValueTask<IReadOnlyDictionary<string, IReadOnlyList<ScramCredentialInfo>>> DescribeUserScramCredentialsAsync(
        IEnumerable<string>? users = null,
        DescribeUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var userList = users?.ToArray() ?? [];
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        var result = userList.ToDictionary(
            user => user,
            _ => (IReadOnlyList<ScramCredentialInfo>)Array.Empty<ScramCredentialInfo>(),
            StringComparer.Ordinal);

        return result;
    }

    public async ValueTask AlterUserScramCredentialsAsync(
        IEnumerable<UserScramCredentialAlteration> alterations,
        AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alterations);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        _ = alterations.Count();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<DelegationToken> CreateDelegationTokenAsync(
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
        var renewerList = renewers?.ToArray() ?? [];
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

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
            Renewers = renewerList
        };

        lock (_delegationTokenGate)
            _delegationTokens[DelegationTokenKey(hmac)] = CloneDelegationToken(token);

        return token;
    }

    public async ValueTask<DateTimeOffset> RenewDelegationTokenAsync(
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
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        var key = DelegationTokenKey(hmac);

        return UpdateDelegationTokenExpiry(key, period);
    }

    public async ValueTask<DateTimeOffset> ExpireDelegationTokenAsync(
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
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        var key = DelegationTokenKey(hmac);

        return UpdateDelegationTokenExpiry(key, period);
    }

    public async ValueTask<IReadOnlyList<DelegationToken>> DescribeDelegationTokensAsync(
        IEnumerable<DelegationTokenPrincipal>? owners = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var ownerFilter = owners?.ToHashSet();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        lock (_delegationTokenGate)
        {
            IReadOnlyList<DelegationToken> tokens = _delegationTokens.Values
                .Where(token => ownerFilter is null || ownerFilter.Contains(token.Owner))
                .Select(token => CloneDelegationToken(token))
                .ToArray();

            return tokens;
        }
    }

    public async ValueTask<IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>>> DescribeConfigsAsync(
        IEnumerable<ConfigResource> resources,
        DescribeConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resources);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var resourceList = resources.ToArray();
        for (var index = 0; index < resourceList.Length; index++)
            await ApplyAdminResourceFaultAsync(resourceList[index], cancellationToken).ConfigureAwait(false);
        var result = resourceList.ToDictionary(
            resource => resource,
            _ => (IReadOnlyList<ConfigEntry>)Array.Empty<ConfigEntry>());

        return result;
    }

    public async ValueTask AlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs,
        AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var resources = configs.Keys.ToArray();
        foreach (var resource in resources)
            await ApplyAdminResourceFaultAsync(resource, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask IncrementalAlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs,
        IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var resources = configs.Keys.ToArray();
        foreach (var resource in resources)
            await ApplyAdminResourceFaultAsync(resource, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CreateAclsAsync(
        IEnumerable<AclBinding> aclBindings,
        CreateAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aclBindings);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (aclBindings.Any())
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<AclBinding>> DeleteAclsAsync(
        IEnumerable<AclBindingFilter> filters,
        DeleteAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filters);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        if (filters.Any())
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        return Array.Empty<AclBinding>();
    }

    public async ValueTask<IReadOnlyList<AclBinding>> DescribeAclsAsync(
        AclBindingFilter filter,
        DescribeAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        return Array.Empty<AclBinding>();
    }

    public async ValueTask DeleteConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition> partitions,
        DeleteConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(partitions);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var partitionList = partitions.ToArray();
        if (partitionList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
        for (var index = 0; index < partitionList.Length; index++)
        {
            var partition = partitionList[index];
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition,
                groupId).ConfigureAwait(false);
            _cluster.DeleteGroupOffsets(groupId, [partition]);
        }
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>> ListOffsetsAsync(
        IEnumerable<TopicPartitionOffsetSpec> specs,
        ListOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var specList = specs.ToArray();
        for (var index = 0; index < specList.Length; index++)
        {
            var partition = specList[index].TopicPartition;
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition).ConfigureAwait(false);
        }

        var result = specList.ToDictionary(
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

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, ElectLeadersResultInfo>> ElectLeadersAsync(
        ElectionType electionType,
        IEnumerable<TopicPartition>? partitions = null,
        ElectLeadersOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var partitionList = partitions?.ToArray() ?? [];
        if (partitionList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        for (var index = 0; index < partitionList.Length; index++)
        {
            var partition = partitionList[index];
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition).ConfigureAwait(false);
        }

        var result = partitionList.ToDictionary(
            partition => partition,
            partition => new ElectLeadersResultInfo
            {
                TopicPartition = partition,
                ErrorCode = ErrorCode.None
            });

        return result;
    }

    public async ValueTask<MetadataQuorumDescription> DescribeMetadataQuorumAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        return new MetadataQuorumDescription
        {
            LeaderId = 0,
            LeaderEpoch = 0,
            HighWatermark = 0,
            CurrentVoters =
            [
                new QuorumReplicaState
                {
                    ReplicaId = 0,
                    LogEndOffset = 0
                }
            ],
            Observers = [],
            Nodes =
            [
                new QuorumNode
                {
                    NodeId = 0,
                    Listeners =
                    [
                        new RaftVoterEndpoint
                        {
                            Name = "PLAINTEXT",
                            Host = "in-memory",
                            Port = 0
                        }
                    ]
                }
            ]
        };
    }

    public async ValueTask AddRaftVoterAsync(
        int voterId,
        Guid voterDirectoryId,
        IEnumerable<RaftVoterEndpoint> endpoints,
        AddRaftVoterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(voterId);
        if (voterDirectoryId == Guid.Empty)
        {
            throw new ArgumentException("Voter directory ID must not be empty.", nameof(voterDirectoryId));
        }

        ValidateRaftVoterEndpoints(endpoints);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveRaftVoterAsync(
        int voterId,
        Guid voterDirectoryId,
        RemoveRaftVoterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(voterId);
        if (voterDirectoryId == Guid.Empty)
        {
            throw new ArgumentException("Voter directory ID must not be empty.", nameof(voterDirectoryId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask UnregisterBrokerAsync(int brokerId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(brokerId);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<ClientQuotaEntity, IReadOnlyDictionary<string, double>>> DescribeClientQuotasAsync(
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
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<ClientQuotaEntity, IReadOnlyDictionary<string, double>> result;
        lock (_clientQuotas)
        {
            result = _clientQuotas
                .Where(quota => MatchesClientQuotaFilter(quota.Key, filter))
                .ToDictionary(
                    quota => CloneClientQuotaEntity(quota.Key),
                    quota => (IReadOnlyDictionary<string, double>)new Dictionary<string, double>(quota.Value, StringComparer.Ordinal));
        }

        return result;
    }

    public async ValueTask AlterClientQuotasAsync(
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

        if (materialized.Length == 0)
            return;

        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        if (options?.ValidateOnly == true)
            return;

        lock (_clientQuotas)
        {
            foreach (var alteration in materialized)
                ApplyClientQuotaAlteration(alteration);
        }

    }

    public async ValueTask<ListTransactionsResult> ListTransactionsAsync(
        ListTransactionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        return new ListTransactionsResult
        {
            UnknownStateFilters = [],
            Transactions = []
        };
    }

    public async ValueTask<IReadOnlyDictionary<string, TransactionDescription>> DescribeTransactionsAsync(
        IEnumerable<string> transactionalIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionalIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var ids = transactionalIds.ToArray();
        if (ids.Length != 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        var result = ids.ToDictionary(
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

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, DescribeProducersResultInfo>> DescribeProducersAsync(
        IEnumerable<TopicPartition> partitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var partitionList = partitions.ToArray();
        for (var index = 0; index < partitionList.Length; index++)
        {
            var partition = partitionList[index];
            ValidateTopicPartition(partition);
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition).ConfigureAwait(false);
        }

        var result = partitionList.ToDictionary(
                partition => partition,
                partition => new DescribeProducersResultInfo
                {
                    TopicPartition = partition,
                    ErrorCode = ErrorCode.None,
                    ActiveProducers = []
                });

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<string, FenceProducersResultInfo>> FenceProducersAsync(
        IEnumerable<string> transactionalIds,
        FenceProducersOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transactionalIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        var ids = transactionalIds.ToArray();
        if (ids.Length != 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        var result = ids.ToDictionary(
            transactionalId => transactionalId,
            transactionalId => new FenceProducersResultInfo
            {
                TransactionalId = transactionalId,
                ErrorCode = ErrorCode.TransactionalIdNotFound
            },
            StringComparer.Ordinal);

        return result;
    }

    public async ValueTask<ForceTerminateTransactionResultInfo> ForceTerminateTransactionAsync(
        string transactionalId,
        ForceTerminateTransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionalId);
        if (options?.TimeoutMs is { } timeoutMs)
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        return new ForceTerminateTransactionResultInfo
        {
            TransactionalId = transactionalId,
            ErrorCode = ErrorCode.TransactionalIdNotFound
        };
    }

    public async ValueTask<AbortTransactionResultInfo> AbortTransactionAsync(
        AbortTransactionSpec transaction,
        AbortTransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        ValidateTopicPartition(transaction.TopicPartition);
        await ApplyAdminFaultAsync(
            cancellationToken,
            transaction.TopicPartition.Topic,
            transaction.TopicPartition.Partition).ConfigureAwait(false);

        return new AbortTransactionResultInfo
        {
            TopicPartition = transaction.TopicPartition,
            ProducerId = transaction.ProducerId,
            ProducerEpoch = transaction.ProducerEpoch,
            CoordinatorEpoch = transaction.CoordinatorEpoch,
            ErrorCode = ErrorCode.None
        };
    }

    public async ValueTask<IReadOnlyDictionary<int, IReadOnlyDictionary<string, LogDirDescription>>> DescribeLogDirsAsync(
        IEnumerable<int> brokerIds,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brokerIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var targetPartitions = partitions?.Distinct().ToArray() ??
            _cluster.ListTopics().SelectMany(_cluster.GetTopicPartitions).ToArray();
        var targetBrokerIds = brokerIds.Distinct().Order().ToArray();
        if (targetBrokerIds.Length == 0)
            return new Dictionary<int, IReadOnlyDictionary<string, LogDirDescription>>();
        for (var index = 0; index < targetBrokerIds.Length; index++)
            ArgumentOutOfRangeException.ThrowIfNegative(targetBrokerIds[index]);

        foreach (var partition in targetPartitions)
        {
            ValidateTopicPartition(partition);
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition).ConfigureAwait(false);
        }
        if (targetPartitions.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<int, IReadOnlyDictionary<string, LogDirDescription>>();
        foreach (var brokerId in targetBrokerIds)
        {
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

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>> AlterReplicaLogDirsAsync(
        IReadOnlyDictionary<TopicPartitionReplica, string> replicaAssignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replicaAssignments);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var assignments = replicaAssignments.ToArray();
        var result = new Dictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>(assignments.Length);
        foreach (var (replica, logDir) in assignments)
        {
            ValidateTopicPartition(replica.TopicPartition);
            ArgumentOutOfRangeException.ThrowIfNegative(replica.BrokerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(logDir);
            await ApplyAdminFaultAsync(
                cancellationToken,
                replica.TopicPartition.Topic,
                replica.TopicPartition.Partition).ConfigureAwait(false);

            result[replica] = new AlterReplicaLogDirResultInfo
            {
                TopicPartitionReplica = replica,
                ErrorCode = ErrorCode.None
            };
        }

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartitionReplica, DescribeReplicaLogDirResultInfo>> DescribeReplicaLogDirsAsync(
        IEnumerable<TopicPartitionReplica> replicas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replicas);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var replicaList = replicas.ToArray();
        var result = new Dictionary<TopicPartitionReplica, DescribeReplicaLogDirResultInfo>(replicaList.Length);
        foreach (var replica in replicaList)
        {
            ValidateTopicPartition(replica.TopicPartition);
            ArgumentOutOfRangeException.ThrowIfNegative(replica.BrokerId);
            await ApplyAdminFaultAsync(
                cancellationToken,
                replica.TopicPartition.Topic,
                replica.TopicPartition.Partition).ConfigureAwait(false);

            var exists = replica.BrokerId == 0 && _cluster.ContainsTopicPartition(replica.TopicPartition);
            result[replica] = new DescribeReplicaLogDirResultInfo
            {
                TopicPartitionReplica = replica,
                CurrentReplicaLogDir = exists ? "in-memory" : null,
                CurrentReplicaOffsetLag = exists ? 0 : -1,
                FutureReplicaOffsetLag = -1,
                ErrorCode = ErrorCode.None
            };
        }

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<string, StreamsGroupDescription>> DescribeStreamsGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var groups = groupIds.ToArray();
        for (var index = 0; index < groups.Length; index++)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groups[index]).ConfigureAwait(false);
        var result = groups.ToDictionary(
            groupId => groupId,
            groupId => new StreamsGroupDescription
            {
                GroupId = groupId,
                GroupState = "Stable",
                Members = []
            },
            StringComparer.Ordinal);

        return result;
    }

    public async ValueTask<IReadOnlyList<GroupListing>> ListStreamsGroupsAsync(
        ListStreamsGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<GroupListing> result = _cluster.ListGroups()
            .Select(groupId => new GroupListing
            {
                GroupId = groupId,
                ProtocolType = "streams",
                State = "Stable"
            })
            .ToArray();

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<string, ShareGroupDescription>> DescribeShareGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var groups = groupIds.ToArray();
        for (var index = 0; index < groups.Length; index++)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groups[index]).ConfigureAwait(false);
        var listedGroups = _cluster.ListShareGroups().ToDictionary(
            static group => group.GroupId,
            static group => group.HasActiveMembers,
            StringComparer.Ordinal);
        var result = groups.ToDictionary(
            groupId => groupId,
            groupId => new ShareGroupDescription
            {
                GroupId = groupId,
                GroupState = listedGroups.TryGetValue(groupId, out var hasActiveMembers) && !hasActiveMembers
                    ? "Empty"
                    : "Stable",
                Members = []
            },
            StringComparer.Ordinal);

        return result;
    }

    public async ValueTask<IReadOnlyList<GroupListing>> ListShareGroupsAsync(
        ListShareGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);

        var groups = _cluster.ListShareGroups();
        var result = new List<GroupListing>(groups.Count);
        foreach (var group in groups)
        {
            var state = group.HasActiveMembers ? "Stable" : "Empty";
            if (options?.States is { Count: > 0 } states &&
                !states.Contains(state, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new GroupListing
            {
                GroupId = group.GroupId,
                ProtocolType = "share",
                State = state
            });
        }

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<string, DeleteShareGroupResult>> DeleteShareGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var groupIdList = groupIds.ToArray();
        var uniqueGroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var groupId in groupIdList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
            if (!uniqueGroupIds.Add(groupId))
                throw new ArgumentException($"Share group ID '{groupId}' is duplicated.", nameof(groupIds));
        }

        var results = new Dictionary<string, DeleteShareGroupResult>(groupIdList.Length, StringComparer.Ordinal);
        if (groupIdList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken).ConfigureAwait(false);
        foreach (var groupId in groupIdList)
        {
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
            results[groupId] = new DeleteShareGroupResult
            {
                GroupId = groupId,
                ErrorCode = _cluster.DeleteShareGroup(groupId)
            };
        }

        return results;
    }

    public async ValueTask<IReadOnlyList<ShareGroupOffsetDescription>> DescribeShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var groupOffsets = _cluster.GetShareGroupOffsets(groupId);
        var targetPartitions = partitions?.ToArray() ?? groupOffsets.Keys.ToArray();
        if (targetPartitions.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
        for (var index = 0; index < targetPartitions.Length; index++)
        {
            var partition = targetPartitions[index];
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition,
                groupId).ConfigureAwait(false);
        }
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

        return result;
    }

    public async ValueTask AlterShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<ShareGroupOffsetAlteration> offsets,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(offsets);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var offsetList = offsets.ToArray();
        if (offsetList.Length == 0)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
        for (var index = 0; index < offsetList.Length; index++)
        {
            var offset = offsetList[index];
            var partition = offset.TopicPartition;
            await ApplyAdminFaultAsync(
                cancellationToken,
                partition.Topic,
                partition.Partition,
                groupId).ConfigureAwait(false);
            _cluster.CommitShareOffsets(
                groupId,
                [new TopicPartitionOffset(
                    partition.Topic,
                    partition.Partition,
                    offset.StartOffset)]);
        }
    }

    public async ValueTask DeleteShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<string> topics,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(topics);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        var topicSet = topics.ToHashSet(StringComparer.Ordinal);
        if (topicSet.Count == 0)
            await ApplyAdminFaultAsync(cancellationToken, groupId: groupId).ConfigureAwait(false);
        foreach (var topic in topicSet)
        {
            await ApplyAdminFaultAsync(cancellationToken, topic, groupId: groupId).ConfigureAwait(false);
            var partitions = _cluster.GetShareGroupOffsets(groupId)
                .Keys
                .Where(partition => StringComparer.Ordinal.Equals(partition.Topic, topic))
                .ToArray();
            _cluster.DeleteShareGroupOffsets(groupId, partitions);
        }
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private ValueTask ApplyAdminFaultAsync(
        CancellationToken cancellationToken,
        string? topic = null,
        int? partition = null,
        string? groupId = null) =>
        _cluster.FaultPlan.ApplyAsync(
            new KafkaFaultScope(KafkaFaultOperation.Admin, topic, partition, groupId),
            cancellationToken);

    private ValueTask ApplyAdminResourceFaultAsync(
        ConfigResource resource,
        CancellationToken cancellationToken) =>
        resource.Type switch
        {
            ConfigResourceType.Topic =>
                ApplyAdminFaultAsync(cancellationToken, topic: resource.Name),
            ConfigResourceType.Group =>
                ApplyAdminFaultAsync(cancellationToken, groupId: resource.Name),
            _ => ApplyAdminFaultAsync(cancellationToken)
        };

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

    private static void ValidateRaftVoterEndpoints(IEnumerable<RaftVoterEndpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var count = 0;
        foreach (var endpoint in endpoints)
        {
            count++;
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.Name);
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint.Host);
            if (endpoint.Port is < 0 or > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(endpoints), endpoint.Port, "Port must be between 0 and 65535.");
            }
        }

        if (count == 0)
        {
            throw new ArgumentException("At least one listener endpoint is required.", nameof(endpoints));
        }
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
                throw KafkaException.FromErrorCode(
                    ErrorCode.DelegationTokenNotFound,
                    "Delegation token was not found.");

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
