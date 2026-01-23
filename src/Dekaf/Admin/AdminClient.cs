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

    public async ValueTask<IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>>> DescribeConfigsAsync(
        IEnumerable<ConfigResource> resources,
        DescribeConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new DescribeConfigsOptions();
        var resourceList = resources.ToList();

        // Any broker can handle DescribeConfigs
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken).ConfigureAwait(false);

        var request = new DescribeConfigsRequest
        {
            Resources = resourceList.Select(r => new DescribeConfigsResource
            {
                ResourceType = (sbyte)r.Type,
                ResourceName = r.Name,
                ConfigurationKeys = null // Fetch all configs
            }).ToList(),
            IncludeSynonyms = opts.IncludeSynonyms,
            IncludeDocumentation = opts.IncludeDocumentation
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DescribeConfigs,
            DescribeConfigsRequest.LowestSupportedVersion,
            DescribeConfigsRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<DescribeConfigsRequest, DescribeConfigsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<ConfigResource, IReadOnlyList<ConfigEntry>>();

        foreach (var resourceResult in response.Results)
        {
            if (resourceResult.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(resourceResult.ErrorCode,
                    $"Failed to describe configs for {(ConfigResourceType)resourceResult.ResourceType}:{resourceResult.ResourceName}: " +
                    $"{resourceResult.ErrorMessage ?? resourceResult.ErrorCode.ToString()}");
            }

            var configResource = new ConfigResource
            {
                Type = (ConfigResourceType)resourceResult.ResourceType,
                Name = resourceResult.ResourceName
            };

            var entries = resourceResult.Configs.Select(c => new ConfigEntry
            {
                Name = c.Name,
                Value = c.Value,
                IsReadOnly = c.ReadOnly,
                IsDefault = c.ConfigSource == (sbyte)ConfigSource.DefaultConfig || c.IsDefault,
                IsSensitive = c.IsSensitive,
                Source = (ConfigSource)c.ConfigSource,
                Synonyms = c.Synonyms?.Select(s => new ConfigSynonym
                {
                    Name = s.Name,
                    Value = s.Value,
                    Source = (ConfigSource)s.Source
                }).ToList(),
                Documentation = c.Documentation
            }).ToList();

            result[configResource] = entries;
        }

        return result;
    }

    public async ValueTask AlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs,
        AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new AlterConfigsOptions();

        // Any broker can handle AlterConfigs
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken).ConfigureAwait(false);

        var request = new AlterConfigsRequest
        {
            Resources = configs.Select(kvp => new AlterConfigsResource
            {
                ResourceType = (sbyte)kvp.Key.Type,
                ResourceName = kvp.Key.Name,
                Configs = kvp.Value.Select(e => new AlterableConfig
                {
                    Name = e.Name,
                    Value = e.Value
                }).ToList()
            }).ToList(),
            ValidateOnly = opts.ValidateOnly
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.AlterConfigs,
            AlterConfigsRequest.LowestSupportedVersion,
            AlterConfigsRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<AlterConfigsRequest, AlterConfigsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for errors
        foreach (var resourceResponse in response.Responses)
        {
            if (resourceResponse.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(resourceResponse.ErrorCode,
                    $"Failed to alter configs for {(ConfigResourceType)resourceResponse.ResourceType}:{resourceResponse.ResourceName}: " +
                    $"{resourceResponse.ErrorMessage ?? resourceResponse.ErrorCode.ToString()}");
            }
        }
    }

    public async ValueTask IncrementalAlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs,
        IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new IncrementalAlterConfigsOptions();

        // Any broker can handle IncrementalAlterConfigs
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken).ConfigureAwait(false);

        var request = new IncrementalAlterConfigsRequest
        {
            Resources = configs.Select(kvp => new IncrementalAlterConfigsResource
            {
                ResourceType = (sbyte)kvp.Key.Type,
                ResourceName = kvp.Key.Name,
                Configs = kvp.Value.Select(a => new IncrementalAlterableConfig
                {
                    Name = a.Name,
                    ConfigOperation = (sbyte)a.Operation,
                    Value = a.Value
                }).ToList()
            }).ToList(),
            ValidateOnly = opts.ValidateOnly
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.IncrementalAlterConfigs,
            IncrementalAlterConfigsRequest.LowestSupportedVersion,
            IncrementalAlterConfigsRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<IncrementalAlterConfigsRequest, IncrementalAlterConfigsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for errors
        foreach (var resourceResponse in response.Responses)
        {
            if (resourceResponse.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(resourceResponse.ErrorCode,
                    $"Failed to incrementally alter configs for {(ConfigResourceType)resourceResponse.ResourceType}:{resourceResponse.ResourceName}: " +
                    $"{resourceResponse.ErrorMessage ?? resourceResponse.ErrorCode.ToString()}");
            }
        }
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
