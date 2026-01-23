using Dekaf.Errors;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol.Messages;
using Dekaf.Security.Sasl;
using Microsoft.Extensions.Logging;

namespace Dekaf.Admin;

/// <summary>
/// Kafka administrative client implementation.
/// </summary>
public sealed class AdminClient : IAdminClient
{
    private readonly AdminClientOptions _options;
    private readonly IConnectionPool _connectionPool;
    private readonly MetadataManager _metadataManager;
    private readonly ILogger<AdminClient>? _logger;
    private volatile bool _disposed;

    public AdminClient(AdminClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        _options = options;
        _logger = loggerFactory?.CreateLogger<AdminClient>();

        _connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword
            },
            loggerFactory);

        _metadataManager = new MetadataManager(
            _connectionPool,
            options.BootstrapServers,
            logger: loggerFactory?.CreateLogger<MetadataManager>());
    }

    public ClusterMetadata Metadata => _metadataManager.Metadata;

    public async ValueTask CreateTopicsAsync(
        IEnumerable<NewTopic> topics,
        CreateTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new CreateTopicsOptions();

        // Convert NewTopic to CreateTopicData
        var topicData = topics.Select(t => new CreateTopicData
        {
            Name = t.Name,
            NumPartitions = t.NumPartitions,
            ReplicationFactor = t.ReplicationFactor,
            Assignments = t.ReplicaAssignments?.Select(a => new CreateTopicAssignment
            {
                PartitionIndex = a.Key,
                BrokerIds = a.Value.ToList()
            }).ToList(),
            Configs = t.Configs?.Select(c => new CreateTopicConfig
            {
                Name = c.Key,
                Value = c.Value
            }).ToList()
        }).ToList();

        var request = new CreateTopicsRequest
        {
            Topics = topicData,
            TimeoutMs = opts.TimeoutMs,
            ValidateOnly = opts.ValidateOnly
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.CreateTopics,
            CreateTopicsRequest.LowestSupportedVersion,
            CreateTopicsRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for errors
        foreach (var topic in response.Topics)
        {
            if (topic.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(topic.ErrorCode,
                    $"Failed to create topic '{topic.Name}': {topic.ErrorMessage ?? topic.ErrorCode.ToString()}");
            }
        }
    }

    public async ValueTask DeleteTopicsAsync(
        IEnumerable<string> topicNames,
        DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new DeleteTopicsOptions();
        var names = topicNames.ToList();

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DeleteTopics,
            DeleteTopicsRequest.LowestSupportedVersion,
            DeleteTopicsRequest.HighestSupportedVersion);

        DeleteTopicsRequest request;
        if (apiVersion >= 6)
        {
            // v6+: Use Topics array with name/id
            request = new DeleteTopicsRequest
            {
                Topics = names.Select(n => new DeleteTopicState { Name = n }).ToList(),
                TimeoutMs = opts.TimeoutMs
            };
        }
        else
        {
            // v0-v5: Use TopicNames array
            request = new DeleteTopicsRequest
            {
                TopicNames = names,
                TimeoutMs = opts.TimeoutMs
            };
        }

        var response = await controller.SendAsync<DeleteTopicsRequest, DeleteTopicsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for errors
        foreach (var topic in response.Responses)
        {
            if (topic.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(topic.ErrorCode,
                    $"Failed to delete topic '{topic.Name}': {topic.ErrorMessage ?? topic.ErrorCode.ToString()}");
            }
        }
    }

    public async ValueTask<IReadOnlyList<TopicListing>> ListTopicsAsync(
        ListTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var topics = _metadataManager.Metadata.GetTopics();

        return topics
            .Where(t => options?.ListInternal ?? false || !t.IsInternal)
            .Select(t => new TopicListing
            {
                Name = t.Name,
                TopicId = t.TopicId,
                IsInternal = t.IsInternal
            })
            .ToList();
    }

    public async ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicsAsync(
        IEnumerable<string> topicNames,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Refresh metadata for specific topics
        await _metadataManager.RefreshMetadataAsync(topicNames, cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<string, TopicDescription>();

        foreach (var name in topicNames)
        {
            var topic = _metadataManager.Metadata.GetTopic(name);
            if (topic is not null)
            {
                result[name] = new TopicDescription
                {
                    Name = topic.Name,
                    TopicId = topic.TopicId,
                    IsInternal = topic.IsInternal,
                    Partitions = topic.Partitions
                };
            }
        }

        return result;
    }

    public async ValueTask<ClusterDescription> DescribeClusterAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return new ClusterDescription
        {
            ClusterId = _metadataManager.Metadata.ClusterId,
            ControllerId = _metadataManager.Metadata.ControllerId,
            Nodes = _metadataManager.Metadata.GetBrokers()
        };
    }

    public async ValueTask<IReadOnlyDictionary<string, GroupDescription>> DescribeConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // TODO: Implement DescribeGroups request
        throw new NotImplementedException("DescribeConsumerGroups not yet implemented");
    }

    public async ValueTask<IReadOnlyList<GroupListing>> ListConsumerGroupsAsync(
        ListConsumerGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // TODO: Implement ListGroups request
        throw new NotImplementedException("ListConsumerGroups not yet implemented");
    }

    public async ValueTask DeleteConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // TODO: Implement DeleteGroups request
        throw new NotImplementedException("DeleteConsumerGroups not yet implemented");
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> ListConsumerGroupOffsetsAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Find group coordinator
        var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
        var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

        var request = new OffsetFetchRequest
        {
            GroupId = groupId,
            Topics = null // Fetch all
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
                    if (partition.CommittedOffset >= 0)
                    {
                        result[new TopicPartition(topic.Name, partition.PartitionIndex)] = partition.CommittedOffset;
                    }
                }
            }
        }

        return result;
    }

    public async ValueTask AlterConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
        var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

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
            GroupId = groupId,
            GenerationIdOrMemberEpoch = -1,
            MemberId = string.Empty,
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
                if (partition.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new Errors.GroupException(partition.ErrorCode,
                        $"AlterConsumerGroupOffsets failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}");
                }
            }
        }
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> DeleteRecordsAsync(
        IReadOnlyDictionary<TopicPartition, long> offsets,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // TODO: Implement DeleteRecords request
        throw new NotImplementedException("DeleteRecords not yet implemented");
    }

    public async ValueTask CreatePartitionsAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // TODO: Implement CreatePartitions request
        throw new NotImplementedException("CreatePartitions not yet implemented");
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>> ListOffsetsAsync(
        IEnumerable<TopicPartitionOffsetSpec> specs,
        ListOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new ListOffsetsOptions();
        var specList = specs.ToList();

        // Group specs by topic for the request
        var topicGroups = specList
            .GroupBy(s => s.TopicPartition.Topic)
            .Select(g => new ListOffsetsRequestTopic
            {
                Name = g.Key,
                Partitions = g.Select(s => new ListOffsetsRequestPartition
                {
                    PartitionIndex = s.TopicPartition.Partition,
                    Timestamp = GetTimestampForSpec(s)
                }).ToList()
            })
            .ToList();

        // Get partition leaders from metadata and group specs by leader
        var partitionsByLeader = new Dictionary<int, List<ListOffsetsRequestTopic>>();

        foreach (var spec in specList)
        {
            var leaderNode = _metadataManager.Metadata.GetPartitionLeader(spec.TopicPartition.Topic, spec.TopicPartition.Partition);
            if (leaderNode is null)
            {
                throw new KafkaException(Protocol.ErrorCode.LeaderNotAvailable,
                    $"No leader available for {spec.TopicPartition.Topic}-{spec.TopicPartition.Partition}");
            }

            var leaderId = leaderNode.NodeId;

            if (!partitionsByLeader.TryGetValue(leaderId, out var leaderTopics))
            {
                leaderTopics = [];
                partitionsByLeader[leaderId] = leaderTopics;
            }

            var topicEntry = leaderTopics.FirstOrDefault(t => t.Name == spec.TopicPartition.Topic);
            if (topicEntry is null)
            {
                topicEntry = new ListOffsetsRequestTopic
                {
                    Name = spec.TopicPartition.Topic,
                    Partitions = new List<ListOffsetsRequestPartition>()
                };
                leaderTopics.Add(topicEntry);
            }

            ((List<ListOffsetsRequestPartition>)topicEntry.Partitions).Add(new ListOffsetsRequestPartition
            {
                PartitionIndex = spec.TopicPartition.Partition,
                Timestamp = GetTimestampForSpec(spec)
            });
        }

        var result = new Dictionary<TopicPartition, ListOffsetsResultInfo>();

        // Send requests to each leader
        foreach (var (leaderId, topics) in partitionsByLeader)
        {
            var connection = await _connectionPool.GetConnectionAsync(leaderId, cancellationToken).ConfigureAwait(false);

            var request = new ListOffsetsRequest
            {
                ReplicaId = -1, // Consumer request
                IsolationLevel = opts.IsolationLevel,
                Topics = topics
            };

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.ListOffsets,
                ListOffsetsRequest.LowestSupportedVersion,
                ListOffsetsRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<ListOffsetsRequest, ListOffsetsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            foreach (var topic in response.Topics)
            {
                foreach (var partition in topic.Partitions)
                {
                    var tp = new TopicPartition(topic.Name, partition.PartitionIndex);

                    if (partition.ErrorCode != Protocol.ErrorCode.None)
                    {
                        throw new KafkaException(partition.ErrorCode,
                            $"ListOffsets failed for {tp.Topic}-{tp.Partition}: {partition.ErrorCode}");
                    }

                    result[tp] = new ListOffsetsResultInfo
                    {
                        Offset = partition.Offset,
                        Timestamp = partition.Timestamp,
                        LeaderEpoch = partition.LeaderEpoch >= 0 ? partition.LeaderEpoch : null
                    };
                }
            }
        }

        return result;
    }

    private static long GetTimestampForSpec(TopicPartitionOffsetSpec spec)
    {
        return spec.Spec switch
        {
            OffsetSpec.Earliest => -2,
            OffsetSpec.Latest => -1,
            OffsetSpec.MaxTimestamp => -3,
            OffsetSpec.Timestamp => spec.Timestamp ?? throw new ArgumentException("Timestamp must be specified when using OffsetSpec.Timestamp"),
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Spec, "Unknown OffsetSpec")
        };
    }

    public async ValueTask<IReadOnlyList<ElectLeadersResultInfo>> ElectLeadersAsync(
        ElectionType electionType,
        IEnumerable<TopicPartition>? partitions = null,
        ElectLeadersOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new ElectLeadersOptions();

        // Build topic partitions array if specified
        IReadOnlyList<ElectLeadersRequestTopic>? topicPartitions = null;
        if (partitions is not null)
        {
            var partitionList = partitions.ToList();
            if (partitionList.Count > 0)
            {
                topicPartitions = partitionList
                    .GroupBy(p => p.Topic)
                    .Select(g => new ElectLeadersRequestTopic
                    {
                        Topic = g.Key,
                        Partitions = g.Select(p => p.Partition).ToList()
                    })
                    .ToList();
            }
        }

        var request = new ElectLeadersRequest
        {
            ElectionType = (byte)electionType,
            TopicPartitions = topicPartitions,
            TimeoutMs = opts.TimeoutMs
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.ElectLeaders,
            ElectLeadersRequest.LowestSupportedVersion,
            ElectLeadersRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<ElectLeadersRequest, ElectLeadersResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check top-level error
        if (response.ErrorCode != Protocol.ErrorCode.None)
        {
            throw new KafkaException(response.ErrorCode, $"ElectLeaders failed: {response.ErrorCode}");
        }

        // Build results
        var results = new List<ElectLeadersResultInfo>();
        foreach (var topic in response.ReplicaElectionResults)
        {
            foreach (var partition in topic.PartitionResult)
            {
                results.Add(new ElectLeadersResultInfo
                {
                    TopicPartition = new TopicPartition(topic.Topic, partition.PartitionId),
                    ErrorCode = partition.ErrorCode,
                    ErrorMessage = partition.ErrorMessage
                });
            }
        }

        return results;
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<IKafkaConnection> GetControllerAsync(CancellationToken cancellationToken)
    {
        var controllerId = _metadataManager.Metadata.ControllerId;
        if (controllerId < 0)
        {
            throw new InvalidOperationException("No controller available");
        }

        return await _connectionPool.GetConnectionAsync(controllerId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> FindGroupCoordinatorAsync(string groupId, CancellationToken cancellationToken)
    {
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken).ConfigureAwait(false);

        var request = new FindCoordinatorRequest
        {
            Key = groupId,
            KeyType = CoordinatorType.Group
        };

        var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            request,
            FindCoordinatorRequest.HighestSupportedVersion,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != Protocol.ErrorCode.None)
        {
            throw new Errors.GroupException(response.ErrorCode, $"FindCoordinator failed: {response.ErrorCode}");
        }

        _connectionPool.RegisterBroker(response.NodeId, response.Host!, response.Port);
        return response.NodeId;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        await _metadataManager.DisposeAsync().ConfigureAwait(false);
        await _connectionPool.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Options for the admin client.
/// </summary>
public sealed class AdminClientOptions
{
    public required IReadOnlyList<string> BootstrapServers { get; init; }
    public string? ClientId { get; init; } = "dekaf-admin";
    public int RequestTimeoutMs { get; init; } = 30000;
    public bool UseTls { get; init; }
    public SaslMechanism SaslMechanism { get; init; } = SaslMechanism.None;
    public string? SaslUsername { get; init; }
    public string? SaslPassword { get; init; }
}

/// <summary>
/// Builder for creating admin clients.
/// </summary>
public sealed class AdminClientBuilder
{
    private readonly List<string> _bootstrapServers = [];
    private string? _clientId;
    private bool _useTls;
    private SaslMechanism _saslMechanism = SaslMechanism.None;
    private string? _saslUsername;
    private string? _saslPassword;
    private Microsoft.Extensions.Logging.ILoggerFactory? _loggerFactory;

    public AdminClientBuilder WithBootstrapServers(string servers)
    {
        _bootstrapServers.Clear();
        _bootstrapServers.AddRange(servers.Split(',').Select(s => s.Trim()));
        return this;
    }

    public AdminClientBuilder WithClientId(string clientId)
    {
        _clientId = clientId;
        return this;
    }

    public AdminClientBuilder UseTls()
    {
        _useTls = true;
        return this;
    }

    public AdminClientBuilder WithSaslPlain(string username, string password)
    {
        _saslMechanism = SaslMechanism.Plain;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public AdminClientBuilder WithSaslScramSha256(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha256;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public AdminClientBuilder WithSaslScramSha512(string username, string password)
    {
        _saslMechanism = SaslMechanism.ScramSha512;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public AdminClientBuilder WithLoggerFactory(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    public IAdminClient Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified");

        var options = new AdminClientOptions
        {
            BootstrapServers = _bootstrapServers,
            ClientId = _clientId,
            UseTls = _useTls,
            SaslMechanism = _saslMechanism,
            SaslUsername = _saslUsername,
            SaslPassword = _saslPassword
        };

        return new AdminClient(options, _loggerFactory);
    }
}
