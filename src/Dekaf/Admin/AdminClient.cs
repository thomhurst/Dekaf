using System.Security.Cryptography;
using System.Text;
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

    public AdminClient(AdminClientOptions options, ILoggerFactory? loggerFactory = null, MetadataOptions? metadataOptions = null)
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
            options: metadataOptions,
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

        var groupIdList = groupIds.ToList();

        // Find coordinator for each group and batch groups by coordinator
        var groupsByCoordinator = new Dictionary<int, List<string>>();
        foreach (var groupId in groupIdList)
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            if (!groupsByCoordinator.TryGetValue(coordinatorId, out var groups))
            {
                groups = [];
                groupsByCoordinator[coordinatorId] = groups;
            }
            groups.Add(groupId);
        }

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DescribeGroups,
            DescribeGroupsRequest.LowestSupportedVersion,
            DescribeGroupsRequest.HighestSupportedVersion);

        var result = new Dictionary<string, GroupDescription>();

        // Send requests per coordinator
        foreach (var (coordinatorId, groups) in groupsByCoordinator)
        {
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            var request = new DescribeGroupsRequest
            {
                Groups = groups
            };

            var response = await connection.SendAsync<DescribeGroupsRequest, DescribeGroupsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            foreach (var group in response.Groups)
            {
                if (group.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new Errors.GroupException(group.ErrorCode,
                        $"DescribeConsumerGroups failed for group '{group.GroupId}': {group.ErrorCode}")
                    {
                        GroupId = group.GroupId
                    };
                }

                result[group.GroupId] = new GroupDescription
                {
                    GroupId = group.GroupId,
                    ProtocolType = group.ProtocolType,
                    ProtocolData = group.ProtocolData,
                    State = group.GroupState,
                    CoordinatorId = coordinatorId,
                    Members = group.Members.Select(m => new MemberDescription
                    {
                        MemberId = m.MemberId,
                        GroupInstanceId = m.GroupInstanceId,
                        ClientId = m.ClientId,
                        ClientHost = m.ClientHost,
                        Assignment = ParseMemberAssignment(m.MemberAssignment)
                    }).ToList()
                };
            }
        }

        return result;
    }

    public async ValueTask<IReadOnlyList<GroupListing>> ListConsumerGroupsAsync(
        ListConsumerGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new ListConsumerGroupsOptions();
        var brokers = _metadataManager.Metadata.GetBrokers();
        if (brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers available");
        }

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.ListGroups,
            ListGroupsRequest.LowestSupportedVersion,
            ListGroupsRequest.HighestSupportedVersion);

        var request = new ListGroupsRequest
        {
            StatesFilter = apiVersion >= 4 ? opts.States : null
        };

        // Query all brokers since each only knows about groups it coordinates
        var seenGroupIds = new HashSet<string>();
        var result = new List<GroupListing>();

        foreach (var broker in brokers)
        {
            var connection = await _connectionPool.GetConnectionAsync(broker.NodeId, cancellationToken).ConfigureAwait(false);

            var response = await connection.SendAsync<ListGroupsRequest, ListGroupsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(response.ErrorCode,
                    $"ListConsumerGroups failed on broker {broker.NodeId}: {response.ErrorCode}");
            }

            foreach (var group in response.Groups)
            {
                if (!seenGroupIds.Add(group.GroupId))
                    continue;

                // Client-side state filtering if broker doesn't support v4+
                if (apiVersion < 4 && opts.States is { Count: > 0 } && group.GroupState is not null)
                {
                    if (!opts.States.Contains(group.GroupState, StringComparer.OrdinalIgnoreCase))
                        continue;
                }

                result.Add(new GroupListing
                {
                    GroupId = group.GroupId,
                    ProtocolType = group.ProtocolType,
                    State = group.GroupState
                });
            }
        }

        return result;
    }

    public async ValueTask DeleteConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var groupIdList = groupIds.ToList();

        // Find coordinator for each group and batch by coordinator
        var groupsByCoordinator = new Dictionary<int, List<string>>();
        foreach (var groupId in groupIdList)
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            if (!groupsByCoordinator.TryGetValue(coordinatorId, out var groups))
            {
                groups = [];
                groupsByCoordinator[coordinatorId] = groups;
            }
            groups.Add(groupId);
        }

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DeleteGroups,
            DeleteGroupsRequest.LowestSupportedVersion,
            DeleteGroupsRequest.HighestSupportedVersion);

        foreach (var (coordinatorId, groups) in groupsByCoordinator)
        {
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            var request = new DeleteGroupsRequest
            {
                GroupsNames = groups
            };

            var response = await connection.SendAsync<DeleteGroupsRequest, DeleteGroupsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            foreach (var groupResult in response.Results)
            {
                if (groupResult.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new Errors.GroupException(groupResult.ErrorCode,
                        $"DeleteConsumerGroups failed for group '{groupResult.GroupId}': {groupResult.ErrorCode}")
                    {
                        GroupId = groupResult.GroupId
                    };
                }
            }
        }
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

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.OffsetFetch,
            OffsetFetchRequest.LowestSupportedVersion,
            OffsetFetchRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<OffsetFetchRequest, OffsetFetchResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<TopicPartition, long>();

        // v8+ uses Groups array; v0-v7 uses flat Topics
        IReadOnlyList<OffsetFetchResponseTopic>? topics = response.Topics;
        if (topics is null && response.Groups is { Count: > 0 })
        {
            var group = response.Groups[0];
            if (group.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new Errors.GroupException(group.ErrorCode,
                    $"ListConsumerGroupOffsets failed for group '{groupId}': {group.ErrorCode}")
                {
                    GroupId = groupId
                };
            }
            topics = group.Topics;
        }

        if (topics is not null)
        {
            foreach (var topic in topics)
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

        // Cap at v7 for admin offset commits — v8+ requires valid MemberId/GenerationId
        // for group membership validation, which admin operations don't have.
        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.OffsetCommit,
            OffsetCommitRequest.LowestSupportedVersion,
            Math.Min(OffsetCommitRequest.HighestSupportedVersion, (short)7));

        var response = await connection.SendAsync<OffsetCommitRequest, OffsetCommitResponse>(
            request,
            apiVersion,
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

        // Group offsets by partition leader
        var partitionsByLeader = new Dictionary<int, List<(TopicPartition Tp, long Offset)>>();

        foreach (var (tp, offset) in offsets)
        {
            var leaderNode = _metadataManager.Metadata.GetPartitionLeader(tp.Topic, tp.Partition);
            if (leaderNode is null)
            {
                throw new KafkaException(Protocol.ErrorCode.LeaderNotAvailable,
                    $"No leader available for {tp.Topic}-{tp.Partition}");
            }

            var leaderId = leaderNode.NodeId;
            if (!partitionsByLeader.TryGetValue(leaderId, out var leaderOffsets))
            {
                leaderOffsets = [];
                partitionsByLeader[leaderId] = leaderOffsets;
            }
            leaderOffsets.Add((tp, offset));
        }

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DeleteRecords,
            DeleteRecordsRequest.LowestSupportedVersion,
            DeleteRecordsRequest.HighestSupportedVersion);

        var result = new Dictionary<TopicPartition, long>();

        foreach (var (leaderId, leaderOffsets) in partitionsByLeader)
        {
            var connection = await _connectionPool.GetConnectionAsync(leaderId, cancellationToken).ConfigureAwait(false);

            // Build topics array from offsets grouped by topic
            var topics = leaderOffsets
                .GroupBy(o => o.Tp.Topic)
                .Select(g => new DeleteRecordsRequestTopic
                {
                    Name = g.Key,
                    Partitions = g.Select(o => new DeleteRecordsRequestPartition
                    {
                        PartitionIndex = o.Tp.Partition,
                        Offset = o.Offset
                    }).ToList()
                }).ToList();

            var request = new DeleteRecordsRequest
            {
                Topics = topics
            };

            var response = await connection.SendAsync<DeleteRecordsRequest, DeleteRecordsResponse>(
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
                            $"DeleteRecords failed for {tp.Topic}-{tp.Partition}: {partition.ErrorCode}");
                    }

                    result[tp] = partition.LowWatermark;
                }
            }
        }

        return result;
    }

    public async ValueTask CreatePartitionsAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var topics = newPartitionCounts.Select(kvp => new CreatePartitionsTopic
        {
            Name = kvp.Key,
            Count = kvp.Value
        }).ToList();

        var request = new CreatePartitionsRequest
        {
            Topics = topics
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.CreatePartitions,
            CreatePartitionsRequest.LowestSupportedVersion,
            CreatePartitionsRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<CreatePartitionsRequest, CreatePartitionsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        foreach (var topicResult in response.Results)
        {
            if (topicResult.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(topicResult.ErrorCode,
                    $"CreatePartitions failed for topic '{topicResult.Name}': {topicResult.ErrorMessage ?? topicResult.ErrorCode.ToString()}");
            }
        }
    }

    public async ValueTask<IReadOnlyDictionary<string, IReadOnlyList<ScramCredentialInfo>>> DescribeUserScramCredentialsAsync(
        IEnumerable<string>? users = null,
        DescribeUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var usersList = users?.Select(u => new UserName { Name = u }).ToList();

        var request = new DescribeUserScramCredentialsRequest
        {
            Users = usersList
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DescribeUserScramCredentials,
            DescribeUserScramCredentialsRequest.LowestSupportedVersion,
            DescribeUserScramCredentialsRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<DescribeUserScramCredentialsRequest, DescribeUserScramCredentialsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check top-level error
        if (response.ErrorCode != Protocol.ErrorCode.None)
        {
            throw new KafkaException(response.ErrorCode,
                $"DescribeUserScramCredentials failed: {response.ErrorMessage ?? response.ErrorCode.ToString()}");
        }

        var result = new Dictionary<string, IReadOnlyList<ScramCredentialInfo>>();

        foreach (var userResult in response.Results)
        {
            if (userResult.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(userResult.ErrorCode,
                    $"DescribeUserScramCredentials failed for user '{userResult.User}': {userResult.ErrorMessage ?? userResult.ErrorCode.ToString()}");
            }

            var credentials = userResult.CredentialInfos
                .Select(c => new ScramCredentialInfo
                {
                    Mechanism = (ScramMechanism)c.Mechanism,
                    Iterations = c.Iterations
                })
                .ToList();

            result[userResult.User] = credentials;
        }

        return result;
    }

    public async ValueTask AlterUserScramCredentialsAsync(
        IEnumerable<UserScramCredentialAlteration> alterations,
        AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var deletions = new List<ScramCredentialDeletion>();
        var upsertions = new List<ScramCredentialUpsertion>();

        foreach (var alteration in alterations)
        {
            switch (alteration)
            {
                case UserScramCredentialDeletion deletion:
                    deletions.Add(new ScramCredentialDeletion
                    {
                        Name = deletion.User,
                        Mechanism = (byte)deletion.Mechanism
                    });
                    break;

                case UserScramCredentialUpsertion upsertion:
                    var salt = upsertion.Salt ?? RandomNumberGenerator.GetBytes(32);
                    var saltedPassword = ComputeSaltedPassword(
                        upsertion.Password,
                        salt,
                        upsertion.Iterations,
                        upsertion.Mechanism);

                    upsertions.Add(new ScramCredentialUpsertion
                    {
                        Name = upsertion.User,
                        Mechanism = (byte)upsertion.Mechanism,
                        Iterations = upsertion.Iterations,
                        Salt = salt,
                        SaltedPassword = saltedPassword
                    });
                    break;
            }
        }

        var request = new AlterUserScramCredentialsRequest
        {
            Deletions = deletions,
            Upsertions = upsertions
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.AlterUserScramCredentials,
            AlterUserScramCredentialsRequest.LowestSupportedVersion,
            AlterUserScramCredentialsRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<AlterUserScramCredentialsRequest, AlterUserScramCredentialsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for errors
        foreach (var result in response.Results)
        {
            if (result.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(result.ErrorCode,
                    $"AlterUserScramCredentials failed for user '{result.User}': {result.ErrorMessage ?? result.ErrorCode.ToString()}");
            }
        }
    }

    private static byte[] ComputeSaltedPassword(string password, byte[] salt, int iterations, ScramMechanism mechanism)
    {
        var hashAlgorithm = mechanism == ScramMechanism.ScramSha256
            ? HashAlgorithmName.SHA256
            : HashAlgorithmName.SHA512;

        var hashSize = mechanism == ScramMechanism.ScramSha256 ? 32 : 64;

        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            hashAlgorithm,
            hashSize);
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

    public async ValueTask CreateAclsAsync(
        IEnumerable<AclBinding> aclBindings,
        CreateAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var bindings = aclBindings.ToList();
        if (bindings.Count == 0)
            return;

        var creations = bindings.Select(b => new AclCreation
        {
            ResourceType = (sbyte)b.Pattern.Type,
            ResourceName = b.Pattern.Name,
            ResourcePatternType = (sbyte)b.Pattern.PatternType,
            Principal = b.Entry.Principal,
            Host = b.Entry.Host,
            Operation = (sbyte)b.Entry.Operation,
            PermissionType = (sbyte)b.Entry.Permission
        }).ToList();

        var request = new CreateAclsRequest
        {
            Creations = creations
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.CreateAcls,
            CreateAclsRequest.LowestSupportedVersion,
            CreateAclsRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<CreateAclsRequest, CreateAclsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for errors
        for (var i = 0; i < response.Results.Count; i++)
        {
            var result = response.Results[i];
            if (result.ErrorCode != Protocol.ErrorCode.None)
            {
                var binding = bindings[i];
                throw new KafkaException(result.ErrorCode,
                    $"Failed to create ACL for {binding.Pattern.Type}:{binding.Pattern.Name}: {result.ErrorMessage ?? result.ErrorCode.ToString()}");
            }
        }
    }

    public async ValueTask<IReadOnlyList<AclBinding>> DeleteAclsAsync(
        IEnumerable<AclBindingFilter> filters,
        DeleteAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var filterList = filters.ToList();
        if (filterList.Count == 0)
            return [];

        var deleteFilters = filterList.Select(f => new DeleteAclsFilter
        {
            ResourceTypeFilter = (sbyte)f.ResourceType,
            ResourceNameFilter = f.ResourceName,
            PatternTypeFilter = (sbyte)f.PatternType,
            PrincipalFilter = f.Principal,
            HostFilter = f.Host,
            Operation = (sbyte)f.Operation,
            PermissionType = (sbyte)f.Permission
        }).ToList();

        var request = new DeleteAclsRequest
        {
            Filters = deleteFilters
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DeleteAcls,
            DeleteAclsRequest.LowestSupportedVersion,
            DeleteAclsRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<DeleteAclsRequest, DeleteAclsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        var deletedBindings = new List<AclBinding>();

        // Check for errors and collect deleted ACLs
        foreach (var filterResult in response.FilterResults)
        {
            if (filterResult.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(filterResult.ErrorCode,
                    $"Failed to delete ACLs: {filterResult.ErrorMessage ?? filterResult.ErrorCode.ToString()}");
            }

            foreach (var matchingAcl in filterResult.MatchingAcls)
            {
                if (matchingAcl.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new KafkaException(matchingAcl.ErrorCode,
                        $"Failed to delete ACL for {matchingAcl.ResourceName}: {matchingAcl.ErrorMessage ?? matchingAcl.ErrorCode.ToString()}");
                }

                deletedBindings.Add(new AclBinding
                {
                    Pattern = new ResourcePattern
                    {
                        Type = (ResourceType)matchingAcl.ResourceType,
                        Name = matchingAcl.ResourceName,
                        PatternType = (PatternType)matchingAcl.PatternType
                    },
                    Entry = new AccessControlEntry
                    {
                        Principal = matchingAcl.Principal,
                        Host = matchingAcl.Host,
                        Operation = (AclOperation)matchingAcl.Operation,
                        Permission = (AclPermissionType)matchingAcl.PermissionType
                    }
                });
            }
        }

        return deletedBindings;
    }

    public async ValueTask<IReadOnlyList<AclBinding>> DescribeAclsAsync(
        AclBindingFilter filter,
        DescribeAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

        var request = new DescribeAclsRequest
        {
            ResourceTypeFilter = (sbyte)filter.ResourceType,
            ResourceNameFilter = filter.ResourceName,
            PatternTypeFilter = (sbyte)filter.PatternType,
            PrincipalFilter = filter.Principal,
            HostFilter = filter.Host,
            Operation = (sbyte)filter.Operation,
            PermissionType = (sbyte)filter.Permission
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DescribeAcls,
            DescribeAclsRequest.LowestSupportedVersion,
            DescribeAclsRequest.HighestSupportedVersion);

        var response = await controller.SendAsync<DescribeAclsRequest, DescribeAclsResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        if (response.ErrorCode != Protocol.ErrorCode.None)
        {
            throw new KafkaException(response.ErrorCode,
                $"Failed to describe ACLs: {response.ErrorMessage ?? response.ErrorCode.ToString()}");
        }

        var bindings = new List<AclBinding>();

        foreach (var resource in response.Resources)
        {
            foreach (var acl in resource.Acls)
            {
                bindings.Add(new AclBinding
                {
                    Pattern = new ResourcePattern
                    {
                        Type = (ResourceType)resource.ResourceType,
                        Name = resource.ResourceName,
                        PatternType = (PatternType)resource.PatternType
                    },
                    Entry = new AccessControlEntry
                    {
                        Principal = acl.Principal,
                        Host = acl.Host,
                        Operation = (AclOperation)acl.Operation,
                        Permission = (AclPermissionType)acl.PermissionType
                    }
                });
            }
        }

        return bindings;
    }

    public async ValueTask DeleteConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition> partitions,
        DeleteConsumerGroupOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        ArgumentNullException.ThrowIfNull(partitions);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
        var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

        // Group partitions by topic
        var topicPartitions = partitions
            .GroupBy(p => p.Topic)
            .Select(g => new OffsetDeleteRequestTopic
            {
                Name = g.Key,
                Partitions = g.Select(p => new OffsetDeleteRequestPartition
                {
                    PartitionIndex = p.Partition
                }).ToList()
            })
            .ToList();

        var request = new OffsetDeleteRequest
        {
            GroupId = groupId,
            Topics = topicPartitions
        };

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.OffsetDelete,
            OffsetDeleteRequest.LowestSupportedVersion,
            OffsetDeleteRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<OffsetDeleteRequest, OffsetDeleteResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // Check for top-level error
        if (response.ErrorCode != Protocol.ErrorCode.None)
        {
            throw new Errors.GroupException(response.ErrorCode,
                $"DeleteConsumerGroupOffsets failed for group '{groupId}': {response.ErrorCode}")
            {
                GroupId = groupId
            };
        }

        // Check for per-partition errors
        foreach (var topic in response.Topics)
        {
            foreach (var partition in topic.Partitions)
            {
                if (partition.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new Errors.GroupException(partition.ErrorCode,
                        $"DeleteConsumerGroupOffsets failed for {topic.Name}-{partition.PartitionIndex}: {partition.ErrorCode}")
                    {
                        GroupId = groupId
                    };
                }
            }
        }
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>> ListOffsetsAsync(
        IEnumerable<TopicPartitionOffsetSpec> specs,
        ListOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new ListOffsetsOptions();
        var specList = specs.ToList();

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

    public async ValueTask<IReadOnlyDictionary<TopicPartition, ElectLeadersResultInfo>> ElectLeadersAsync(
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
        var results = new Dictionary<TopicPartition, ElectLeadersResultInfo>();
        foreach (var topic in response.ReplicaElectionResults)
        {
            foreach (var partition in topic.PartitionResult)
            {
                var tp = new TopicPartition(topic.Topic, partition.PartitionId);
                results[tp] = new ElectLeadersResultInfo
                {
                    TopicPartition = tp,
                    ErrorCode = partition.ErrorCode,
                    ErrorMessage = partition.ErrorMessage
                };
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

        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.FindCoordinator,
            FindCoordinatorRequest.LowestSupportedVersion,
            FindCoordinatorRequest.HighestSupportedVersion);

        var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
            request,
            apiVersion,
            cancellationToken).ConfigureAwait(false);

        // v4+ uses Coordinators array; v0-v3 uses top-level fields
        if (apiVersion >= 4 && response.Coordinators is { Count: > 0 })
        {
            var coordinator = response.Coordinators[0];
            if (coordinator.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new Errors.GroupException(coordinator.ErrorCode, $"FindCoordinator failed: {coordinator.ErrorCode}");
            }

            _connectionPool.RegisterBroker(coordinator.NodeId, coordinator.Host, coordinator.Port);
            return coordinator.NodeId;
        }

        if (response.ErrorCode != Protocol.ErrorCode.None)
        {
            throw new Errors.GroupException(response.ErrorCode, $"FindCoordinator failed: {response.ErrorCode}");
        }

        _connectionPool.RegisterBroker(response.NodeId, response.Host!, response.Port);
        return response.NodeId;
    }

    private static IReadOnlyList<TopicPartition>? ParseMemberAssignment(byte[]? assignmentBytes)
    {
        if (assignmentBytes is null || assignmentBytes.Length == 0)
            return null;

        try
        {
            // Consumer protocol assignment format:
            // Version: int16
            // TopicPartitions: [TopicName: string, Partitions: [int32]]
            // UserData: bytes (optional)
            var reader = new Protocol.KafkaProtocolReader(assignmentBytes);

            _ = reader.ReadInt16(); // version

            var topicCount = reader.ReadInt32();
            var assignments = new List<TopicPartition>();

            for (var i = 0; i < topicCount; i++)
            {
                var topic = reader.ReadString() ?? string.Empty;
                var partitionCount = reader.ReadInt32();
                for (var j = 0; j < partitionCount; j++)
                {
                    var partition = reader.ReadInt32();
                    assignments.Add(new TopicPartition(topic, partition));
                }
            }

            return assignments;
        }
        catch
        {
            // If we can't parse the assignment bytes, return null rather than failing
            return null;
        }
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

    /// <summary>
    /// Strategy for recovering cluster metadata when all known brokers become unavailable.
    /// Default is <see cref="MetadataRecoveryStrategy.Rebootstrap"/>.
    /// </summary>
    public MetadataRecoveryStrategy MetadataRecoveryStrategy { get; init; } = MetadataRecoveryStrategy.Rebootstrap;

    /// <summary>
    /// How long in milliseconds to wait before triggering a rebootstrap when all known brokers
    /// are unavailable. Only applies when <see cref="MetadataRecoveryStrategy"/> is
    /// <see cref="MetadataRecoveryStrategy.Rebootstrap"/>.
    /// Default is 300000 (5 minutes).
    /// </summary>
    public int MetadataRecoveryRebootstrapTriggerMs { get; init; } = 300000;
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
    private MetadataRecoveryStrategy _metadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap;
    private int _metadataRecoveryRebootstrapTriggerMs = 300000;
    private TimeSpan? _metadataMaxAge;

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

    /// <summary>
    /// Sets the metadata recovery strategy for when all known brokers become unavailable.
    /// </summary>
    /// <param name="strategy">The recovery strategy to use.</param>
    public AdminClientBuilder WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy)
    {
        _metadataRecoveryStrategy = strategy;
        return this;
    }

    /// <summary>
    /// Sets how long to wait before triggering a rebootstrap when all known
    /// brokers are unavailable.
    /// </summary>
    /// <param name="trigger">The trigger delay. Default is 5 minutes.</param>
    public AdminClientBuilder WithMetadataRecoveryRebootstrapTrigger(TimeSpan trigger)
    {
        _metadataRecoveryRebootstrapTriggerMs = (int)trigger.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum age of metadata before it is refreshed.
    /// This controls how frequently the client refreshes its view of the cluster topology.
    /// Equivalent to Kafka's <c>metadata.max.age.ms</c> configuration.
    /// Default is 15 minutes.
    /// </summary>
    /// <param name="interval">The maximum age of metadata. Must be positive.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public AdminClientBuilder WithMetadataMaxAge(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Metadata max age must be positive");

        _metadataMaxAge = interval;
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
            SaslPassword = _saslPassword,
            MetadataRecoveryStrategy = _metadataRecoveryStrategy,
            MetadataRecoveryRebootstrapTriggerMs = _metadataRecoveryRebootstrapTriggerMs
        };

        var metadataOptions = _metadataMaxAge.HasValue
            ? new MetadataOptions { MetadataRefreshInterval = _metadataMaxAge.Value }
            : null;

        return new AdminClient(options, _loggerFactory, metadataOptions);
    }
}
