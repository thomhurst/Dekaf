using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Dekaf;
using Dekaf.Internal;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Retry;
using Dekaf.Protocol.Messages;
using Dekaf.Security;
using Dekaf.Security.Sasl;
using Dekaf.Telemetry;
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
    private readonly ClientTelemetryMetricCollector _telemetryMetricCollector;
    private readonly ClientTelemetryManager _telemetryManager;
    private readonly ILogger<AdminClient>? _logger;
    private readonly bool _ownsResources;
    private int _telemetryStartAttempted;
    private int _disposed;

    public AdminClient(AdminClientOptions options, ILoggerFactory? loggerFactory = null, MetadataOptions? metadataOptions = null)
    {
        _options = options;
        _logger = loggerFactory?.CreateLogger<AdminClient>();
        _ownsResources = true;
        _telemetryMetricCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        _telemetryMetricCollector.RegisterMetricsForSubscription(options.ApplicationMetrics);

        _connectionPool = new ConnectionPool(
            options.ClientId,
            new ConnectionOptions
            {
                UseTls = options.UseTls,
                TlsConfig = options.TlsConfig,
                RequestTimeout = TimeSpan.FromMilliseconds(options.RequestTimeoutMs),
                ReconnectBackoff = TimeSpan.FromMilliseconds(options.ReconnectBackoffMs),
                ReconnectBackoffMax = TimeSpan.FromMilliseconds(options.ReconnectBackoffMaxMs),
                ConnectionsMaxIdleMs = options.ConnectionsMaxIdleMs,
                SaslMechanism = options.SaslMechanism,
                SaslUsername = options.SaslUsername,
                SaslPassword = options.SaslPassword,
                GssapiConfig = options.GssapiConfig,
                OAuthBearerConfig = options.OAuthBearerConfig,
                OAuthBearerTokenProvider = options.OAuthBearerTokenProvider,
                OAuthBearerToken = options.OAuthBearerToken,
                ClientDnsLookup = options.ClientDnsLookup,
                AwsMskIamConfig = options.AwsMskIamConfig
            },
            loggerFactory);

        _metadataManager = new MetadataManager(
            _connectionPool,
            options.BootstrapServers,
            options: metadataOptions,
            logger: loggerFactory?.CreateLogger<MetadataManager>());

        _telemetryManager = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ClientTelemetryManager>(),
            _telemetryMetricCollector);
        _ownsResources = true;
    }

    internal AdminClient(
        AdminClientOptions options,
        IConnectionPool connectionPool,
        MetadataManager metadataManager,
        ILoggerFactory? loggerFactory = null,
        bool ownsResources = false)
    {
        _options = options;
        _logger = loggerFactory?.CreateLogger<AdminClient>();
        _connectionPool = connectionPool;
        _metadataManager = metadataManager;
        _telemetryMetricCollector = new ClientTelemetryMetricCollector(ClientTelemetryClientRole.Admin);
        _telemetryMetricCollector.RegisterMetricsForSubscription(options.ApplicationMetrics);
        _telemetryManager = new ClientTelemetryManager(
            _connectionPool,
            _metadataManager,
            loggerFactory?.CreateLogger<ClientTelemetryManager>(),
            _telemetryMetricCollector);
        _ownsResources = ownsResources;
    }

    public ClusterMetadata Metadata => _metadataManager.Metadata;

    /// <inheritdoc />
    public void RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(AdminClient));

        _telemetryMetricCollector.RegisterMetricForSubscription(metric);
    }

    /// <inheritdoc />
    public void UnregisterMetricFromSubscription(string name)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(AdminClient));

        _telemetryMetricCollector.UnregisterMetricFromSubscription(name);
    }

    public async ValueTask CreateTopicsAsync(
        IEnumerable<NewTopic> topics,
        CreateTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new CreateTopicsOptions();

        // Materialize before retry to avoid re-enumeration of potentially lazy sequences
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

        // Tracks whether a previous attempt may have applied the create on the broker.
        // A retriable failure after the broker accepted the create (e.g. a response
        // timeout, or waiting for leader election) makes the retry hit TopicAlreadyExists
        // for a topic this call created — treat that as success instead of failing the
        // whole operation. This is a heuristic: a topic created concurrently by another
        // client between attempts is also reported as success. Validate-only requests
        // never mutate cluster state, so they never arm the tolerance.
        var createMayHaveApplied = false;

        await WithRetryAsync(async () =>
        {
            var isRetryAttempt = createMayHaveApplied;
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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

            createMayHaveApplied = !opts.ValidateOnly;
            var response = await controller.SendAsync<CreateTopicsRequest, CreateTopicsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            // Check for errors
            var createdTopicNames = new List<string>(response.Topics.Count);
            foreach (var topic in response.Topics)
            {
                if (topic.ErrorCode != Protocol.ErrorCode.None &&
                    !(isRetryAttempt && topic.ErrorCode == Protocol.ErrorCode.TopicAlreadyExists))
                {
                    throw new KafkaException(topic.ErrorCode,
                        $"Failed to create topic '{topic.Name}': {topic.ErrorMessage ?? topic.ErrorCode.ToString()}");
                }

                createdTopicNames.Add(topic.Name);
            }

            // Wait until metadata shows all created topics with partition leaders assigned.
            // The broker acknowledges topic creation before leader election completes,
            // so a single metadata refresh may return topics with LeaderNotAvailable.
            // Poll until every partition has a leader, matching the retry pattern in
            // MetadataManager.GetTopicMetadataSlowAsync.
            if (!opts.ValidateOnly)
            {
                await WaitForTopicLeadersAsync(createdTopicNames, cancellationToken).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WaitForTopicLeadersAsync(
        List<string> topicNames,
        CancellationToken cancellationToken)
    {
        // Leader election is an async cluster operation that can take multiple metadata
        // refresh cycles, especially with multi-partition topics. Use more retries than
        // WithRetryAsync (which handles transient RPC errors) to give the cluster time.
        const int leaderWaitRetries = 5;

        for (var attempt = 0; attempt < leaderWaitRetries; attempt++)
        {
            await _metadataManager.RefreshMetadataAsync(topicNames, forceRefresh: true, cancellationToken: cancellationToken).ConfigureAwait(false);

            var allReady = true;
            foreach (var topicName in topicNames)
            {
                var topic = _metadataManager.Metadata.GetTopic(topicName);
                if (topic is null || topic.PartitionCount == 0 ||
                    topic.ErrorCode is Protocol.ErrorCode.LeaderNotAvailable or Protocol.ErrorCode.UnknownTopicOrPartition)
                {
                    allReady = false;
                    break;
                }

                // Check that every partition has a leader assigned
                foreach (var partition in topic.Partitions)
                {
                    if (partition.LeaderId < 0 ||
                        partition.ErrorCode is Protocol.ErrorCode.LeaderNotAvailable)
                    {
                        allReady = false;
                        break;
                    }
                }

                if (!allReady)
                    break;
            }

            if (allReady)
                return;

            await Task.Delay(RetryHelper.RetryDelayMs, cancellationToken).ConfigureAwait(false);
        }

        throw new KafkaException(Protocol.ErrorCode.LeaderNotAvailable,
            $"Topic(s) {string.Join(", ", topicNames)} did not have all partition leaders elected after {leaderWaitRetries * RetryHelper.RetryDelayMs}ms");
    }

    public async ValueTask DeleteTopicsAsync(
        IEnumerable<string> topicNames,
        DeleteTopicsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new DeleteTopicsOptions();
        var names = topicNames.ToList();

        await WithRetryAsync(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);

        // Refresh metadata so deleted topics are no longer visible in ListTopicsAsync
        await _metadataManager.RefreshMetadataAsync(cancellationToken).ConfigureAwait(false);
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
        await _metadataManager.RefreshMetadataAsync(topicNames, forceRefresh: true, cancellationToken: cancellationToken).ConfigureAwait(false);

        var result = new Dictionary<string, TopicDescription>();

        foreach (var name in topicNames)
        {
            var topic = _metadataManager.Metadata.GetTopic(name);
            if (topic is null)
            {
                continue;
            }

            // Report per-topic errors (e.g. TopicAuthorizationFailed when DESCRIBE is denied) on the
            // description rather than throwing, so one inaccessible topic does not fail the whole batch.
            result[name] = new TopicDescription
            {
                Name = topic.Name,
                TopicId = topic.TopicId,
                IsInternal = topic.IsInternal,
                Partitions = topic.Partitions,
                ErrorCode = topic.ErrorCode
            };
        }

        return result;
    }

    public async ValueTask<IReadOnlyDictionary<string, TopicDescription>> DescribeTopicPartitionsAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new DescribeTopicPartitionsOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(opts.ResponsePartitionLimit, 1);

        var topicList = topicNames.ToList();
        if (topicList.Count == 0)
        {
            return new Dictionary<string, TopicDescription>();
        }

        var result = new Dictionary<string, TopicDescription>(StringComparer.Ordinal);
        var partitionAccumulator = new Dictionary<string, List<PartitionInfo>>(StringComparer.Ordinal);
        var seenCursors = new HashSet<(string TopicName, int PartitionIndex)>();
        DescribeTopicPartitionsCursor? cursor = null;

        do
        {
            var page = await DescribeTopicPartitionsPageAsync(
                topicList,
                new DescribeTopicPartitionsPageOptions
                {
                    ResponsePartitionLimit = opts.ResponsePartitionLimit,
                    Cursor = cursor
                },
                cancellationToken).ConfigureAwait(false);

            MergeTopicPartitionPage(result, partitionAccumulator, page.Topics.Values);
            cursor = page.NextCursor;
            if (cursor is not null && !seenCursors.Add((cursor.TopicName, cursor.PartitionIndex)))
            {
                throw new InvalidOperationException("Broker returned a repeated DescribeTopicPartitions cursor.");
            }
        } while (cursor is not null);

        return result;
    }

    public async ValueTask<DescribeTopicPartitionsPage> DescribeTopicPartitionsPageAsync(
        IEnumerable<string> topicNames,
        DescribeTopicPartitionsPageOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var opts = options ?? new DescribeTopicPartitionsPageOptions();
        ArgumentOutOfRangeException.ThrowIfLessThan(opts.ResponsePartitionLimit, 1);

        var topicList = topicNames.ToList();
        if (topicList.Count == 0)
        {
            return new DescribeTopicPartitionsPage
            {
                Topics = new Dictionary<string, TopicDescription>()
            };
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        if (!_metadataManager.HasApiKey(Protocol.ApiKey.DescribeTopicPartitions))
        {
            throw new Errors.BrokerVersionException("Broker does not support DescribeTopicPartitions (API key 75).");
        }

        return await WithRetryAsync(async () =>
        {
            var brokers = _metadataManager.Metadata.GetBrokers();
            if (brokers.Count == 0)
            {
                throw new InvalidOperationException("No brokers available");
            }

            var connection = await _connectionPool.GetConnectionAsync(brokers[0].NodeId, cancellationToken).ConfigureAwait(false);
            var request = new DescribeTopicPartitionsRequest
            {
                Topics = topicList.Select(static name => new DescribeTopicPartitionsRequestTopic
                {
                    Name = name
                }).ToList(),
                ResponsePartitionLimit = opts.ResponsePartitionLimit,
                Cursor = opts.Cursor is null
                    ? null
                    : new DescribeTopicPartitionsRequestCursor
                    {
                        TopicName = opts.Cursor.TopicName,
                        PartitionIndex = opts.Cursor.PartitionIndex
                    }
            };

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.DescribeTopicPartitions,
                DescribeTopicPartitionsRequest.LowestSupportedVersion,
                DescribeTopicPartitionsRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<DescribeTopicPartitionsRequest, DescribeTopicPartitionsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            var topics = response.Topics
                .Select(ToTopicDescription)
                .ToDictionary(static t => t.Name, StringComparer.Ordinal);

            return new DescribeTopicPartitionsPage
            {
                ThrottleTimeMs = response.ThrottleTimeMs,
                Topics = topics,
                NextCursor = response.NextCursor is null
                    ? null
                    : new DescribeTopicPartitionsCursor
                    {
                        TopicName = response.NextCursor.TopicName,
                        PartitionIndex = response.NextCursor.PartitionIndex
                    }
            };
        }, cancellationToken).ConfigureAwait(false);
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

    private static void MergeTopicPartitionPage(
        Dictionary<string, TopicDescription> result,
        Dictionary<string, List<PartitionInfo>> partitionAccumulator,
        IEnumerable<TopicDescription> pageTopics)
    {
        foreach (var topic in pageTopics)
        {
            if (!partitionAccumulator.TryGetValue(topic.Name, out var partitions))
            {
                partitions = new List<PartitionInfo>(topic.Partitions);
                partitionAccumulator[topic.Name] = partitions;
            }
            else
            {
                partitions.AddRange(topic.Partitions);
            }

            result[topic.Name] = new TopicDescription
            {
                Name = topic.Name,
                TopicId = topic.TopicId,
                IsInternal = topic.IsInternal,
                ErrorCode = topic.ErrorCode,
                TopicAuthorizedOperations = topic.TopicAuthorizedOperations,
                Partitions = partitions
            };
        }
    }

    private static TopicDescription ToTopicDescription(DescribeTopicPartitionsResponseTopic topic) => new()
    {
        Name = topic.Name ?? string.Empty,
        TopicId = topic.TopicId,
        IsInternal = topic.IsInternal,
        ErrorCode = topic.ErrorCode,
        TopicAuthorizedOperations = topic.TopicAuthorizedOperations,
        Partitions = topic.Partitions.Select(static p => new PartitionInfo
        {
            PartitionIndex = p.PartitionIndex,
            LeaderId = p.LeaderId,
            LeaderEpoch = p.LeaderEpoch,
            ReplicaNodes = p.ReplicaNodes,
            IsrNodes = p.IsrNodes,
            EligibleLeaderReplicas = p.EligibleLeaderReplicas,
            LastKnownElr = p.LastKnownElr,
            OfflineReplicas = p.OfflineReplicas,
            ErrorCode = p.ErrorCode
        }).ToList()
    };

    public async ValueTask<IReadOnlyDictionary<string, GroupDescription>> DescribeConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var groupIdList = groupIds.ToList();

        return await WithRetryAsync<IReadOnlyDictionary<string, GroupDescription>>(async () =>
        {
            // Find coordinator for each group and batch groups by coordinator.
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

            return await DescribeConsumerGroupsWithBestAvailableApiAsync(
                groupsByCoordinator,
                cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyDictionary<string, GroupDescription>> DescribeConsumerGroupsWithBestAvailableApiAsync(
        IReadOnlyDictionary<int, List<string>> groupsByCoordinator,
        CancellationToken cancellationToken)
    {
        if (!_metadataManager.HasApiKey(Protocol.ApiKey.ConsumerGroupDescribe))
        {
            return await DescribeConsumerGroupsWithClassicApiAsync(groupsByCoordinator, cancellationToken).ConfigureAwait(false);
        }

        var (descriptions, classicFallbackGroups) = await DescribeConsumerGroupsWithKip848Async(
            groupsByCoordinator,
            cancellationToken).ConfigureAwait(false);

        if (classicFallbackGroups.Count > 0)
        {
            var classicDescriptions = await DescribeConsumerGroupsWithClassicApiAsync(
                classicFallbackGroups,
                cancellationToken).ConfigureAwait(false);

            foreach (var (groupId, description) in classicDescriptions)
                descriptions[groupId] = description;
        }

        return descriptions;
    }

    private async ValueTask<(Dictionary<string, GroupDescription> Descriptions, Dictionary<int, List<string>> ClassicFallbackGroups)> DescribeConsumerGroupsWithKip848Async(
        IReadOnlyDictionary<int, List<string>> groupsByCoordinator,
        CancellationToken cancellationToken)
    {
        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.ConsumerGroupDescribe,
            ConsumerGroupDescribeRequest.LowestSupportedVersion,
            ConsumerGroupDescribeRequest.HighestSupportedVersion);

        var result = new Dictionary<string, GroupDescription>();
        var classicFallbackGroups = new Dictionary<int, List<string>>();

        foreach (var (coordinatorId, groups) in groupsByCoordinator)
        {
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            var request = new ConsumerGroupDescribeRequest
            {
                GroupIds = groups,
                IncludeAuthorizedOperations = true
            };

            var response = await connection.SendAsync<ConsumerGroupDescribeRequest, ConsumerGroupDescribeResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            foreach (var group in response.Groups)
            {
                if (ShouldFallbackToClassicConsumerGroupApi(group.ErrorCode))
                {
                    if (!classicFallbackGroups.TryGetValue(coordinatorId, out var fallbackGroups))
                    {
                        fallbackGroups = [];
                        classicFallbackGroups[coordinatorId] = fallbackGroups;
                    }

                    fallbackGroups.Add(group.GroupId);
                    continue;
                }

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
                    ProtocolType = "consumer",
                    // Legacy shape: classic DescribeGroups exposes the assignor in ProtocolData.
                    ProtocolData = group.AssignorName,
                    State = group.GroupState,
                    CoordinatorId = coordinatorId,
                    GroupEpoch = group.GroupEpoch,
                    AssignmentEpoch = group.AssignmentEpoch,
                    AssignorName = group.AssignorName,
                    AuthorizedOperations = group.AuthorizedOperations,
                    Members = group.Members.Select(m => new MemberDescription
                    {
                        MemberId = m.MemberId,
                        GroupInstanceId = m.InstanceId,
                        RackId = m.RackId,
                        MemberEpoch = m.MemberEpoch,
                        ClientId = m.ClientId,
                        ClientHost = m.ClientHost,
                        SubscribedTopicNames = m.SubscribedTopicNames,
                        SubscribedTopicRegex = m.SubscribedTopicRegex,
                        Assignment = FlattenAssignment(m.Assignment),
                        TargetAssignment = FlattenAssignment(m.TargetAssignment),
                        MemberType = m.MemberType
                    }).ToList()
                };
            }
        }

        return (result, classicFallbackGroups);
    }

    private static bool ShouldFallbackToClassicConsumerGroupApi(Protocol.ErrorCode errorCode) =>
        errorCode is Protocol.ErrorCode.GroupIdNotFound or Protocol.ErrorCode.UnsupportedVersion;

    private async ValueTask<IReadOnlyDictionary<string, GroupDescription>> DescribeConsumerGroupsWithClassicApiAsync(
        IReadOnlyDictionary<int, List<string>> groupsByCoordinator,
        CancellationToken cancellationToken)
    {
        var apiVersion = _metadataManager.GetNegotiatedApiVersion(
            Protocol.ApiKey.DescribeGroups,
            DescribeGroupsRequest.LowestSupportedVersion,
            DescribeGroupsRequest.HighestSupportedVersion);

        var result = new Dictionary<string, GroupDescription>();

        foreach (var (coordinatorId, groups) in groupsByCoordinator)
        {
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            var request = new DescribeGroupsRequest
            {
                Groups = groups,
                IncludeAuthorizedOperations = true
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
                    AuthorizedOperations = group.AuthorizedOperations,
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

        return await WithRetryAsync<IReadOnlyList<GroupListing>>(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteConsumerGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var groupIdList = groupIds.ToList();

        await WithRetryAsync(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> ListConsumerGroupOffsetsAsync(
        string groupId,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return await WithRetryAsync<IReadOnlyDictionary<TopicPartition, long>>(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AlterConsumerGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartitionOffset> offsets,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var topicOffsets = offsets.GroupBy(o => o.Topic).Select(g => new OffsetCommitRequestTopic
        {
            Name = g.Key,
            Partitions = g.Select(o => new OffsetCommitRequestPartition
            {
                PartitionIndex = o.Partition,
                CommittedOffset = o.Offset
            }).ToList()
        }).ToList();

        await WithRetryAsync(async () =>
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, long>> DeleteRecordsAsync(
        IReadOnlyDictionary<TopicPartition, long> offsets,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return await WithRetryAsync<IReadOnlyDictionary<TopicPartition, long>>(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CreatePartitionsAsync(
        IReadOnlyDictionary<string, int> newPartitionCounts,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var topics = newPartitionCounts.Select(kvp => new CreatePartitionsTopic
        {
            Name = kvp.Key,
            Count = kvp.Value
        }).ToList();

        await WithRetryAsync(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AlterPartitionReassignmentsAsync(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments,
        AlterPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reassignments);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new AlterPartitionReassignmentsOptions();
        var topics = BuildAlterPartitionReassignmentTopics(reassignments);
        if (topics.Count == 0)
        {
            return;
        }

        // Materialize before retry so an ambiguous retriable failure resends the exact
        // same target/cancel operations. Some already-applied retries surface as
        // ReassignmentInProgress or NoReassignmentInProgress; those are tolerated only
        // after a previous attempt may have reached the controller.
        var alterMayHaveApplied = false;

        await WithRetryAsync(async () =>
        {
            var isRetryAttempt = alterMayHaveApplied;
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

            var request = new AlterPartitionReassignmentsRequest
            {
                TimeoutMs = opts.TimeoutMs,
                AllowReplicationFactorChange = opts.AllowReplicationFactorChange,
                Topics = topics
            };

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.AlterPartitionReassignments,
                AlterPartitionReassignmentsRequest.LowestSupportedVersion,
                AlterPartitionReassignmentsRequest.HighestSupportedVersion);

            alterMayHaveApplied = true;
            var response = await controller.SendAsync<AlterPartitionReassignmentsRequest, AlterPartitionReassignmentsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode != Protocol.ErrorCode.None)
            {
                if (IsToleratedReassignmentRetry(isRetryAttempt, response.ErrorCode))
                {
                    return;
                }

                throw new KafkaException(response.ErrorCode,
                    $"AlterPartitionReassignments failed: {response.ErrorMessage ?? response.ErrorCode.ToString()}");
            }

            foreach (var topic in response.Responses)
            {
                foreach (var partition in topic.Partitions)
                {
                    if (partition.ErrorCode == Protocol.ErrorCode.None ||
                        IsToleratedReassignmentRetry(isRetryAttempt, partition.ErrorCode))
                    {
                        continue;
                    }

                    throw new KafkaException(partition.ErrorCode,
                        $"AlterPartitionReassignments failed for {topic.Name}-{partition.PartitionIndex}: " +
                        $"{partition.ErrorMessage ?? partition.ErrorCode.ToString()}");
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, PartitionReassignment>> ListPartitionReassignmentsAsync(
        IEnumerable<TopicPartition>? partitions = null,
        ListPartitionReassignmentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new ListPartitionReassignmentsOptions();
        var topics = partitions is null ? null : BuildListPartitionReassignmentTopics(partitions);

        return await WithRetryAsync<IReadOnlyDictionary<TopicPartition, PartitionReassignment>>(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

            var request = new ListPartitionReassignmentsRequest
            {
                TimeoutMs = opts.TimeoutMs,
                Topics = topics
            };

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.ListPartitionReassignments,
                ListPartitionReassignmentsRequest.LowestSupportedVersion,
                ListPartitionReassignmentsRequest.HighestSupportedVersion);

            var response = await controller.SendAsync<ListPartitionReassignmentsRequest, ListPartitionReassignmentsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new KafkaException(response.ErrorCode,
                    $"ListPartitionReassignments failed: {response.ErrorMessage ?? response.ErrorCode.ToString()}");
            }

            var result = new Dictionary<TopicPartition, PartitionReassignment>();
            foreach (var topic in response.Topics)
            {
                foreach (var partition in topic.Partitions)
                {
                    result[new TopicPartition(topic.Name, partition.PartitionIndex)] = new PartitionReassignment
                    {
                        Replicas = partition.Replicas,
                        AddingReplicas = partition.AddingReplicas,
                        RemovingReplicas = partition.RemovingReplicas
                    };
                }
            }

            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static List<AlterPartitionReassignmentsRequestTopic> BuildAlterPartitionReassignmentTopics(
        IReadOnlyDictionary<TopicPartition, Optional<NewPartitionReassignment>> reassignments) =>
        reassignments
            .Select(kvp =>
            {
                ValidateTopicPartition(kvp.Key);
                var targetReplicas = kvp.Value.HasValue ? kvp.Value.Value.TargetReplicas : null;
                return new
                {
                    TopicPartition = kvp.Key,
                    Replicas = targetReplicas is { Count: > 0 } ? targetReplicas.ToList() : null
                };
            })
            .GroupBy(item => item.TopicPartition.Topic)
            .Select(group => new AlterPartitionReassignmentsRequestTopic
            {
                Name = group.Key,
                Partitions = group
                    .OrderBy(item => item.TopicPartition.Partition)
                    .Select(item => new AlterPartitionReassignmentsRequestPartition
                    {
                        PartitionIndex = item.TopicPartition.Partition,
                        Replicas = item.Replicas
                    })
                    .ToList()
            })
            .ToList();

    private static List<ListPartitionReassignmentsRequestTopic> BuildListPartitionReassignmentTopics(
        IEnumerable<TopicPartition> partitions) =>
        partitions
            .Select(partition =>
            {
                ValidateTopicPartition(partition);
                return partition;
            })
            .GroupBy(partition => partition.Topic)
            .Select(group => new ListPartitionReassignmentsRequestTopic
            {
                Name = group.Key,
                PartitionIndexes = group.Select(partition => partition.Partition).Order().ToList()
            })
            .ToList();

    private static void ValidateTopicPartition(TopicPartition topicPartition)
    {
        ArgumentException.ThrowIfNullOrEmpty(topicPartition.Topic);
        if (topicPartition.Partition < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topicPartition), topicPartition.Partition, "Partition index must not be negative.");
        }
    }

    private static bool IsToleratedReassignmentRetry(bool isRetryAttempt, Protocol.ErrorCode errorCode) =>
        isRetryAttempt &&
        (errorCode == Protocol.ErrorCode.ReassignmentInProgress ||
         errorCode == Protocol.ErrorCode.NoReassignmentInProgress);

    public async ValueTask<IReadOnlyDictionary<string, IReadOnlyList<ScramCredentialInfo>>> DescribeUserScramCredentialsAsync(
        IEnumerable<string>? users = null,
        DescribeUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var usersList = users?.Select(u => new UserName { Name = u }).ToList();

        return await WithRetryAsync<IReadOnlyDictionary<string, IReadOnlyList<ScramCredentialInfo>>>(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AlterUserScramCredentialsAsync(
        IEnumerable<UserScramCredentialAlteration> alterations,
        AlterUserScramCredentialsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Materialize before retry to avoid re-enumeration of potentially lazy sequences
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

        await WithRetryAsync(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
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

        return await WithRetryAsync<IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>>>(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigEntry>> configs,
        AlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new AlterConfigsOptions();

        await WithRetryAsync(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask IncrementalAlterConfigsAsync(
        IReadOnlyDictionary<ConfigResource, IReadOnlyList<ConfigAlter>> configs,
        IncrementalAlterConfigsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new IncrementalAlterConfigsOptions();

        await WithRetryAsync(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CreateAclsAsync(
        IEnumerable<AclBinding> aclBindings,
        CreateAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

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

        await WithRetryAsync(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<AclBinding>> DeleteAclsAsync(
        IEnumerable<AclBindingFilter> filters,
        DeleteAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

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

        return await WithRetryAsync<IReadOnlyList<AclBinding>>(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<AclBinding>> DescribeAclsAsync(
        AclBindingFilter filter,
        DescribeAclsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return await WithRetryAsync<IReadOnlyList<AclBinding>>(async () =>
        {
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
        }, cancellationToken).ConfigureAwait(false);
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

        // Group partitions by topic — materialize before retry to avoid re-enumeration
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

        await WithRetryAsync(async () =>
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>> ListOffsetsAsync(
        IEnumerable<TopicPartitionOffsetSpec> specs,
        ListOffsetsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new ListOffsetsOptions();
        var specList = specs.ToList();

        return await WithRetryAsync(async () =>
        {
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

            return (IReadOnlyDictionary<TopicPartition, ListOffsetsResultInfo>)result;
        }, cancellationToken).ConfigureAwait(false);
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

        var opts = options ?? new ElectLeadersOptions();

        // Materialize before retry to avoid re-enumeration of potentially lazy sequences
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

        return await WithRetryAsync<IReadOnlyDictionary<TopicPartition, ElectLeadersResultInfo>>(async () =>
        {
            var controller = await GetControllerAsync(cancellationToken).ConfigureAwait(false);

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<int, IReadOnlyDictionary<string, LogDirDescription>>> DescribeLogDirsAsync(
        IEnumerable<int> brokerIds,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brokerIds);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var brokerIdList = brokerIds.Distinct().ToArray();
        foreach (var brokerId in brokerIdList)
        {
            if (brokerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(brokerIds), brokerId, "Broker ID must be non-negative.");
            }
        }

        var topicFilters = BuildDescribeLogDirsTopics(partitions);
        if (brokerIdList.Length == 0)
        {
            return new Dictionary<int, IReadOnlyDictionary<string, LogDirDescription>>();
        }

        return await WithRetryAsync<IReadOnlyDictionary<int, IReadOnlyDictionary<string, LogDirDescription>>>(async () =>
        {
            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.DescribeLogDirs,
                DescribeLogDirsRequest.LowestSupportedVersion,
                DescribeLogDirsRequest.HighestSupportedVersion);

            // Fan out to all requested brokers in parallel.
            var brokerResults = await Task.WhenAll(brokerIdList.Select(async brokerId =>
            {
                var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);
                var response = await connection.SendAsync<DescribeLogDirsRequest, DescribeLogDirsResponse>(
                    new DescribeLogDirsRequest { Topics = topicFilters },
                    apiVersion,
                    cancellationToken).ConfigureAwait(false);

                if (response.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new KafkaException(response.ErrorCode,
                        $"DescribeLogDirs failed for broker {brokerId}: {response.ErrorCode}");
                }

                var brokerResult = new Dictionary<string, LogDirDescription>();
                foreach (var dir in response.Results)
                {
                    var replicas = new Dictionary<TopicPartition, ReplicaLogDirInfo>();
                    foreach (var topic in dir.Topics)
                    {
                        foreach (var partition in topic.Partitions)
                        {
                            var topicPartition = new TopicPartition(topic.Name, partition.PartitionIndex);
                            replicas[topicPartition] = new ReplicaLogDirInfo
                            {
                                Size = partition.PartitionSize,
                                OffsetLag = partition.OffsetLag,
                                IsFuture = partition.IsFutureKey
                            };
                        }
                    }

                    brokerResult[dir.LogDir] = new LogDirDescription
                    {
                        ErrorCode = dir.ErrorCode,
                        ReplicaInfos = replicas,
                        TotalBytes = dir.TotalBytes,
                        UsableBytes = dir.UsableBytes,
                        IsCordoned = dir.IsCordoned
                    };
                }

                return (BrokerId: brokerId, Result: brokerResult);
            })).ConfigureAwait(false);

            var results = new Dictionary<int, IReadOnlyDictionary<string, LogDirDescription>>(brokerResults.Length);
            foreach (var brokerResult in brokerResults)
            {
                results[brokerResult.BrokerId] = brokerResult.Result;
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyDictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>> AlterReplicaLogDirsAsync(
        IReadOnlyDictionary<TopicPartitionReplica, string> replicaAssignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replicaAssignments);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var assignments = new List<ReplicaLogDirAssignment>(replicaAssignments.Count);
        foreach (var (replica, logDir) in replicaAssignments)
        {
            ValidateTopicPartitionReplica(replica, nameof(replicaAssignments));
            if (string.IsNullOrWhiteSpace(logDir))
            {
                throw new ArgumentException("Log directory path must be specified.", nameof(replicaAssignments));
            }

            assignments.Add(new ReplicaLogDirAssignment(replica, logDir));
        }

        if (assignments.Count == 0)
        {
            return new Dictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>();
        }

        var assignmentsByBroker = assignments.GroupBy(static a => a.Replica.BrokerId).ToArray();

        return await WithRetryAsync<IReadOnlyDictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>>(async () =>
        {
            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.AlterReplicaLogDirs,
                AlterReplicaLogDirsRequest.LowestSupportedVersion,
                AlterReplicaLogDirsRequest.HighestSupportedVersion);

            // Fan out to all target brokers in parallel.
            var brokerResults = await Task.WhenAll(assignmentsByBroker.Select(async brokerAssignments =>
            {
                var brokerId = brokerAssignments.Key;
                var connection = await _connectionPool.GetConnectionAsync(brokerId, cancellationToken).ConfigureAwait(false);
                var request = new AlterReplicaLogDirsRequest
                {
                    Dirs = BuildAlterReplicaLogDirsRequestDirs(brokerAssignments)
                };

                var response = await connection.SendAsync<AlterReplicaLogDirsRequest, AlterReplicaLogDirsResponse>(
                    request,
                    apiVersion,
                    cancellationToken).ConfigureAwait(false);

                var brokerResult = new Dictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>();
                foreach (var topic in response.Results)
                {
                    foreach (var partition in topic.Partitions)
                    {
                        var replica = new TopicPartitionReplica(topic.TopicName, partition.PartitionIndex, brokerId);
                        brokerResult[replica] = new AlterReplicaLogDirResultInfo
                        {
                            TopicPartitionReplica = replica,
                            ErrorCode = partition.ErrorCode
                        };
                    }
                }

                return brokerResult;
            })).ConfigureAwait(false);

            var results = new Dictionary<TopicPartitionReplica, AlterReplicaLogDirResultInfo>(replicaAssignments.Count);
            foreach (var brokerResult in brokerResults)
            {
                foreach (var (replica, result) in brokerResult)
                {
                    results[replica] = result;
                }
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<DescribeLogDirsRequestTopic>? BuildDescribeLogDirsTopics(IEnumerable<TopicPartition>? partitions)
    {
        if (partitions is null)
        {
            return null;
        }

        var topics = new Dictionary<string, List<int>>();
        foreach (var topicPartition in partitions)
        {
            ValidateTopicPartition(topicPartition, nameof(partitions));
            if (!topics.TryGetValue(topicPartition.Topic, out var topicPartitions))
            {
                topicPartitions = [];
                topics[topicPartition.Topic] = topicPartitions;
            }

            if (!topicPartitions.Contains(topicPartition.Partition))
            {
                topicPartitions.Add(topicPartition.Partition);
            }
        }

        return topics
            .Select(static kvp => new DescribeLogDirsRequestTopic
            {
                Topic = kvp.Key,
                Partitions = kvp.Value
            })
            .ToList();
    }

    private static IReadOnlyList<AlterReplicaLogDirsRequestDir> BuildAlterReplicaLogDirsRequestDirs(
        IEnumerable<ReplicaLogDirAssignment> assignments)
    {
        var dirs = new Dictionary<string, Dictionary<string, List<int>>>();
        foreach (var assignment in assignments)
        {
            if (!dirs.TryGetValue(assignment.LogDir, out var topics))
            {
                topics = [];
                dirs[assignment.LogDir] = topics;
            }

            if (!topics.TryGetValue(assignment.Replica.Topic, out var partitions))
            {
                partitions = [];
                topics[assignment.Replica.Topic] = partitions;
            }

            partitions.Add(assignment.Replica.Partition);
        }

        return dirs
            .Select(static dir => new AlterReplicaLogDirsRequestDir
            {
                Path = dir.Key,
                Topics = dir.Value
                    .Select(static topic => new AlterReplicaLogDirsRequestTopic
                    {
                        Name = topic.Key,
                        Partitions = topic.Value
                    })
                    .ToList()
            })
            .ToList();
    }

    private static void ValidateTopicPartition(TopicPartition topicPartition, string paramName)
    {
        if (string.IsNullOrWhiteSpace(topicPartition.Topic))
        {
            throw new ArgumentException("Topic name must be specified.", paramName);
        }

        if (topicPartition.Partition < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, topicPartition.Partition, "Partition must be non-negative.");
        }
    }

    private static void ValidateTopicPartitionReplica(TopicPartitionReplica replica, string paramName)
    {
        ValidateTopicPartition(replica.TopicPartition, paramName);
        if (replica.BrokerId < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, replica.BrokerId, "Broker ID must be non-negative.");
        }
    }

    private readonly record struct ReplicaLogDirAssignment(TopicPartitionReplica Replica, string LogDir);

    public async ValueTask<IReadOnlyDictionary<string, ShareGroupDescription>> DescribeShareGroupsAsync(
        IEnumerable<string> groupIds,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var groupIdList = groupIds.ToList();

        return await WithRetryAsync<IReadOnlyDictionary<string, ShareGroupDescription>>(async () =>
        {
            // Find coordinators for all groups in parallel
            var coordinatorTasks = groupIdList
                .Select(async g => (GroupId: g, CoordinatorId: await FindGroupCoordinatorAsync(g, cancellationToken).ConfigureAwait(false)));
            var coordinatorResults = await Task.WhenAll(coordinatorTasks).ConfigureAwait(false);

            var groupsByCoordinator = new Dictionary<int, List<string>>();
            foreach (var (groupId, coordinatorId) in coordinatorResults)
            {
                if (!groupsByCoordinator.TryGetValue(coordinatorId, out var groups))
                {
                    groups = [];
                    groupsByCoordinator[coordinatorId] = groups;
                }
                groups.Add(groupId);
            }

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.ShareGroupDescribe,
                ShareGroupDescribeRequest.LowestSupportedVersion,
                ShareGroupDescribeRequest.HighestSupportedVersion);

            // Fan out ShareGroupDescribe requests to all coordinators in parallel
            var describeResponses = await Task.WhenAll(groupsByCoordinator.Select(async kvp =>
            {
                var connection = await _connectionPool.GetConnectionAsync(kvp.Key, cancellationToken).ConfigureAwait(false);

                var request = new ShareGroupDescribeRequest
                {
                    GroupIds = kvp.Value,
                    IncludeAuthorizedOperations = true
                };

                return await connection.SendAsync<ShareGroupDescribeRequest, ShareGroupDescribeResponse>(
                    request,
                    apiVersion,
                    cancellationToken).ConfigureAwait(false);
            })).ConfigureAwait(false);

            var result = new Dictionary<string, ShareGroupDescription>();

            foreach (var response in describeResponses)
            {
                foreach (var group in response.Groups)
                {
                    if (group.ErrorCode != Protocol.ErrorCode.None)
                    {
                        throw new Errors.GroupException(group.ErrorCode,
                            $"DescribeShareGroups failed for group '{group.GroupId}': {group.ErrorCode}")
                        {
                            GroupId = group.GroupId
                        };
                    }

                    result[group.GroupId] = new ShareGroupDescription
                    {
                        GroupId = group.GroupId,
                        GroupState = group.GroupState,
                        GroupEpoch = group.GroupEpoch,
                        AssignmentEpoch = group.AssignmentEpoch,
                        AssignorName = group.AssignorName,
                        AuthorizedOperations = group.AuthorizedOperations,
                        Members = group.Members.Select(m => new ShareGroupMemberDescription
                        {
                            MemberId = m.MemberId,
                            RackId = m.RackId,
                            MemberEpoch = m.MemberEpoch,
                            ClientId = m.ClientId,
                            ClientHost = m.ClientHost,
                            SubscribedTopicNames = m.SubscribedTopicNames,
                            Assignment = m.Assignment?.TopicPartitions
                                ?.SelectMany(tp => tp.Partitions.Select(p =>
                                    new TopicPartition(tp.TopicName ?? string.Empty, p)))
                                .ToList()
                        }).ToList()
                    };
                }
            }

            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<GroupListing>> ListShareGroupsAsync(
        ListShareGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var opts = options ?? new ListShareGroupsOptions();

        return await WithRetryAsync<IReadOnlyList<GroupListing>>(async () =>
        {
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
                StatesFilter = apiVersion >= 4 ? opts.States : null,
                TypesFilter = apiVersion >= 5 ? ["share"] : null
            };

            // Fan out to all brokers in parallel
            var responses = await Task.WhenAll(brokers.Select(async broker =>
            {
                var connection = await _connectionPool.GetConnectionAsync(broker.NodeId, cancellationToken).ConfigureAwait(false);

                var response = await connection.SendAsync<ListGroupsRequest, ListGroupsResponse>(
                    request,
                    apiVersion,
                    cancellationToken).ConfigureAwait(false);

                if (response.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new KafkaException(response.ErrorCode,
                        $"ListShareGroups failed on broker {broker.NodeId}: {response.ErrorCode}");
                }

                return response;
            })).ConfigureAwait(false);

            // Merge and deduplicate results
            var seenGroupIds = new HashSet<string>();
            var result = new List<GroupListing>();

            foreach (var response in responses)
            {
                foreach (var group in response.Groups)
                {
                    // Filter to share groups only (client-side for pre-v5 brokers)
                    if (apiVersion < 5 && !string.Equals(group.GroupType, "share", StringComparison.OrdinalIgnoreCase))
                        continue;

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
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ShareGroupOffsetDescription>> DescribeShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<TopicPartition>? partitions = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var partitionList = partitions?.ToList();

        return await WithRetryAsync<IReadOnlyList<ShareGroupOffsetDescription>>(async () =>
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            // Build topics filter from partitions list, or null for all
            IReadOnlyList<DescribeShareGroupOffsetsRequestTopic>? requestTopics = null;
            if (partitionList is { Count: > 0 })
            {
                requestTopics = partitionList
                    .GroupBy(tp => tp.Topic)
                    .Select(g => new DescribeShareGroupOffsetsRequestTopic
                    {
                        TopicName = g.Key,
                        Partitions = g.Select(tp => tp.Partition).ToList()
                    })
                    .ToList();
            }

            var request = new DescribeShareGroupOffsetsRequest
            {
                Groups =
                [
                    new DescribeShareGroupOffsetsRequestGroup
                    {
                        GroupId = groupId,
                        Topics = requestTopics
                    }
                ]
            };

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.DescribeShareGroupOffsets,
                DescribeShareGroupOffsetsRequest.LowestSupportedVersion,
                DescribeShareGroupOffsetsRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<DescribeShareGroupOffsetsRequest, DescribeShareGroupOffsetsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            var result = new List<ShareGroupOffsetDescription>();

            foreach (var group in response.Groups)
            {
                if (group.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new Errors.GroupException(group.ErrorCode,
                        $"DescribeShareGroupOffsets failed for group '{group.GroupId}': {group.ErrorCode}")
                    {
                        GroupId = group.GroupId
                    };
                }

                foreach (var topic in group.Topics)
                {
                    foreach (var partition in topic.Partitions)
                    {
                        result.Add(new ShareGroupOffsetDescription
                        {
                            TopicPartition = new TopicPartition(topic.TopicName, partition.PartitionIndex),
                            StartOffset = partition.StartOffset,
                            LeaderEpoch = partition.LeaderEpoch,
                            Lag = partition.Lag,
                            ErrorCode = partition.ErrorCode,
                            ErrorMessage = partition.ErrorMessage
                        });
                    }
                }
            }

            return result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AlterShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<ShareGroupOffsetAlteration> offsets,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Hoisted outside WithRetryAsync intentionally: derived purely from the caller's
        // immutable input, safe to reuse across retries.
        var topicData = offsets
            .GroupBy(o => o.TopicPartition.Topic)
            .Select(g => new AlterShareGroupOffsetsRequestTopic
            {
                TopicName = g.Key,
                Partitions = g.Select(o => new AlterShareGroupOffsetsRequestPartition
                {
                    PartitionIndex = o.TopicPartition.Partition,
                    StartOffset = o.StartOffset
                }).ToList()
            })
            .ToList();

        await WithRetryAsync(async () =>
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            var request = new AlterShareGroupOffsetsRequest
            {
                GroupId = groupId,
                Topics = topicData
            };

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.AlterShareGroupOffsets,
                AlterShareGroupOffsetsRequest.LowestSupportedVersion,
                AlterShareGroupOffsetsRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<AlterShareGroupOffsetsRequest, AlterShareGroupOffsetsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new Errors.GroupException(response.ErrorCode,
                    $"AlterShareGroupOffsets failed for group '{groupId}': {response.ErrorCode}")
                {
                    GroupId = groupId
                };
            }

            foreach (var topic in response.Responses)
            {
                foreach (var partition in topic.Partitions)
                {
                    if (partition.ErrorCode != Protocol.ErrorCode.None)
                    {
                        throw new Errors.GroupException(partition.ErrorCode,
                            $"AlterShareGroupOffsets failed for group '{groupId}', " +
                            $"topic '{topic.TopicName}', partition {partition.PartitionIndex}: {partition.ErrorCode}")
                        {
                            GroupId = groupId
                        };
                    }
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteShareGroupOffsetsAsync(
        string groupId,
        IEnumerable<string> topics,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var topicData = topics
            .Select(t => new DeleteShareGroupOffsetsRequestTopic { TopicName = t })
            .ToList();

        await WithRetryAsync(async () =>
        {
            var coordinatorId = await FindGroupCoordinatorAsync(groupId, cancellationToken).ConfigureAwait(false);
            var connection = await _connectionPool.GetConnectionAsync(coordinatorId, cancellationToken).ConfigureAwait(false);

            var request = new DeleteShareGroupOffsetsRequest
            {
                GroupId = groupId,
                Topics = topicData
            };

            var apiVersion = _metadataManager.GetNegotiatedApiVersion(
                Protocol.ApiKey.DeleteShareGroupOffsets,
                DeleteShareGroupOffsetsRequest.LowestSupportedVersion,
                DeleteShareGroupOffsetsRequest.HighestSupportedVersion);

            var response = await connection.SendAsync<DeleteShareGroupOffsetsRequest, DeleteShareGroupOffsetsResponse>(
                request,
                apiVersion,
                cancellationToken).ConfigureAwait(false);

            if (response.ErrorCode != Protocol.ErrorCode.None)
            {
                throw new Errors.GroupException(response.ErrorCode,
                    $"DeleteShareGroupOffsets failed for group '{groupId}': {response.ErrorCode}")
                {
                    GroupId = groupId
                };
            }

            foreach (var topic in response.Responses)
            {
                if (topic.ErrorCode != Protocol.ErrorCode.None)
                {
                    throw new Errors.GroupException(topic.ErrorCode,
                        $"DeleteShareGroupOffsets failed for group '{groupId}', topic '{topic.TopicName}': {topic.ErrorCode}")
                    {
                        GroupId = groupId
                    };
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_metadataManager.Metadata.LastRefreshed == default)
        {
            await _metadataManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Volatile.Read(ref _telemetryStartAttempted) == 0)
        {
            await StartTelemetryOnceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask StartTelemetryOnceAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _telemetryStartAttempted, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await _telemetryManager.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Volatile.Write(ref _telemetryStartAttempted, 0);
            throw;
        }
    }

    private ValueTask WithRetryAsync(Func<ValueTask> operation, CancellationToken cancellationToken)
        => RetryHelper.WithRetryAsync(operation, _metadataManager, cancellationToken);

    private ValueTask<T> WithRetryAsync<T>(Func<ValueTask<T>> operation, CancellationToken cancellationToken)
        => RetryHelper.WithRetryAsync(operation, _metadataManager, cancellationToken);

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

        if (response.Coordinators.Count == 0)
        {
            throw new Errors.GroupException(Protocol.ErrorCode.CoordinatorNotAvailable,
                "FindCoordinator returned an empty Coordinators array");
        }

        var coordinator = response.Coordinators[0];
        if (coordinator.ErrorCode != Protocol.ErrorCode.None)
        {
            throw new Errors.GroupException(coordinator.ErrorCode, $"FindCoordinator failed: {coordinator.ErrorCode}");
        }

        _connectionPool.RegisterBroker(coordinator.NodeId, coordinator.Host, coordinator.Port);
        return coordinator.NodeId;
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

    private static IReadOnlyList<TopicPartition>? FlattenAssignment(ConsumerGroupDescribeAssignment assignment)
    {
        if (assignment.TopicPartitions.Count == 0)
            return null;

        return assignment.TopicPartitions
            .SelectMany(topic => topic.Partitions.Select(partition => new TopicPartition(topic.TopicName, partition)))
            .ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var telemetryStopMs = Math.Max(1, Math.Min(_options.RequestTimeoutMs, 5000));
        await _telemetryManager.StopAsync(TimeSpan.FromMilliseconds(telemetryStopMs)).ConfigureAwait(false);
        await _telemetryManager.DisposeAsync().ConfigureAwait(false);

        if (_ownsResources)
        {
            await _metadataManager.DisposeAsync().ConfigureAwait(false);
            await _connectionPool.DisposeAsync().ConfigureAwait(false);
        }
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
    public int ReconnectBackoffMs { get; init; } = 50;
    public int ReconnectBackoffMaxMs { get; init; } = 1000;

    /// <summary>
    /// Maximum idle time in milliseconds before unused broker connections are closed.
    /// Default is 9 minutes, below Kafka's default broker-side 10 minute idle timeout.
    /// Set to -1 to disable client-side idle connection reaping.
    /// </summary>
    public int ConnectionsMaxIdleMs { get; init; } = ConnectionOptions.DefaultConnectionsMaxIdleMs;

    public bool UseTls { get; init; }
    public TlsConfig? TlsConfig { get; init; }
    public SaslMechanism SaslMechanism { get; init; } = SaslMechanism.None;
    public string? SaslUsername { get; init; }
    public string? SaslPassword { get; init; }

    /// <summary>
    /// GSSAPI (Kerberos) configuration. Required when <see cref="SaslMechanism"/> is
    /// <see cref="SaslMechanism.Gssapi"/>.
    /// </summary>
    public GssapiConfig? GssapiConfig { get; init; }

    /// <summary>
    /// OAuth 2.0 / OIDC configuration used to fetch bearer tokens from a token endpoint.
    /// Used when <see cref="SaslMechanism"/> is <see cref="SaslMechanism.OAuthBearer"/>.
    /// </summary>
    public OAuthBearerConfig? OAuthBearerConfig { get; init; }

    /// <summary>
    /// A callback that supplies OAuth bearer tokens on demand.
    /// Takes precedence over <see cref="OAuthBearerConfig"/> if both are specified.
    /// </summary>
    public Func<CancellationToken, ValueTask<OAuthBearerToken>>? OAuthBearerTokenProvider { get; init; }

    /// <summary>
    /// A pre-obtained OAuth bearer token. Use this for static tokens; for dynamic
    /// retrieval use <see cref="OAuthBearerTokenProvider"/> instead.
    /// </summary>
    public OAuthBearerToken? OAuthBearerToken { get; init; }

    /// <summary>
    /// AWS_MSK_IAM configuration.
    /// </summary>
    public AwsMskIamConfig? AwsMskIamConfig { get; init; }

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

    /// <summary>
    /// Controls how broker hostnames are resolved before connecting.
    /// </summary>
    public ClientDnsLookup ClientDnsLookup { get; init; } = ClientDnsLookup.UseAllDnsIps;

    /// <summary>
    /// Application metrics registered for broker telemetry subscriptions.
    /// </summary>
    public IReadOnlyList<ApplicationTelemetryMetric> ApplicationMetrics { get; init; } = [];
}

/// <summary>
/// Builder for creating admin clients.
/// </summary>
public sealed class AdminClientBuilder
{
    private readonly KafkaClientInfrastructure? _clientInfrastructure;
    private IReadOnlyList<string> _bootstrapServers = [];
    private string? _clientId;
    private int _requestTimeoutMs = 30000;
    private bool _useTls;
    private TlsConfig? _tlsConfig;
    private SaslMechanism _saslMechanism = SaslMechanism.None;
    private string? _saslUsername;
    private string? _saslPassword;
    private GssapiConfig? _gssapiConfig;
    private OAuthBearerConfig? _oauthConfig;
    private Func<CancellationToken, ValueTask<OAuthBearerToken>>? _oauthTokenProvider;
    private int _reconnectBackoffMs = 50;
    private int _reconnectBackoffMaxMs = 1000;
    private int _connectionsMaxIdleMs = ConnectionOptions.DefaultConnectionsMaxIdleMs;
    private AwsMskIamConfig? _awsMskIamConfig;
    private ILoggerFactory? _loggerFactory;
    private MetadataRecoveryStrategy _metadataRecoveryStrategy = MetadataRecoveryStrategy.Rebootstrap;
    private int _metadataRecoveryRebootstrapTriggerMs = 300000;
    private ClientDnsLookup _clientDnsLookup = ClientDnsLookup.UseAllDnsIps;
    private TimeSpan? _metadataMaxAge;
    private readonly Dictionary<string, ApplicationTelemetryMetric> _applicationMetrics = new(StringComparer.Ordinal);

    public AdminClientBuilder()
    {
    }

    internal AdminClientBuilder(KafkaClientInfrastructure clientInfrastructure)
    {
        _clientInfrastructure = clientInfrastructure;
        _bootstrapServers = clientInfrastructure.BootstrapServers;
        _loggerFactory = clientInfrastructure.LoggerFactory;
    }

    public AdminClientBuilder WithBootstrapServers(string servers)
    {
        ThrowIfClientOwnedBootstrap();
        _bootstrapServers = BootstrapServerList.FromCommaSeparated(servers);
        return this;
    }

    public AdminClientBuilder WithBootstrapServers(params string[] servers)
    {
        ThrowIfClientOwnedBootstrap();
        _bootstrapServers = BootstrapServerList.FromValues(servers);
        return this;
    }

    public AdminClientBuilder WithClientId(string clientId)
    {
        _clientId = clientId;
        return this;
    }

    public AdminClientBuilder UseTls()
    {
        ThrowIfClientOwnedConnectionSettings();
        _useTls = true;
        return this;
    }

    /// <summary>
    /// Configures TLS with custom settings.
    /// </summary>
    /// <param name="config">The TLS configuration.</param>
    public AdminClientBuilder UseTls(TlsConfig config)
    {
        ThrowIfClientOwnedConnectionSettings();
        _useTls = true;
        _tlsConfig = config;
        return this;
    }

    /// <summary>
    /// Configures mutual TLS (mTLS) authentication using certificate files.
    /// </summary>
    /// <param name="caCertPath">Path to the CA certificate file (PEM format).</param>
    /// <param name="clientCertPath">Path to the client certificate file (PEM format).</param>
    /// <param name="clientKeyPath">Path to the client private key file (PEM format).</param>
    /// <param name="keyPassword">Optional password for the private key.</param>
    public AdminClientBuilder UseMutualTls(
        string caCertPath,
        string clientCertPath,
        string clientKeyPath,
        string? keyPassword = null)
    {
        ThrowIfClientOwnedConnectionSettings();
        _useTls = true;
        _tlsConfig = TlsConfig.CreateMutualTls(caCertPath, clientCertPath, clientKeyPath, keyPassword);
        return this;
    }

    /// <summary>
    /// Configures mutual TLS (mTLS) authentication using in-memory certificates.
    /// </summary>
    /// <param name="clientCertificate">The client certificate with private key.</param>
    /// <param name="caCertificate">Optional CA certificate for server validation.</param>
    public AdminClientBuilder UseMutualTls(
        X509Certificate2 clientCertificate,
        X509Certificate2? caCertificate = null)
    {
        ThrowIfClientOwnedConnectionSettings();
        _useTls = true;
        _tlsConfig = TlsConfig.CreateMutualTls(clientCertificate, caCertificate);
        return this;
    }

    public AdminClientBuilder WithSaslPlain(string username, string password)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = SaslMechanism.Plain;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public AdminClientBuilder WithSaslScramSha256(string username, string password)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = SaslMechanism.ScramSha256;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    public AdminClientBuilder WithSaslScramSha512(string username, string password)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = SaslMechanism.ScramSha512;
        _saslUsername = username;
        _saslPassword = password;
        return this;
    }

    /// <summary>
    /// Configures SASL/GSSAPI (Kerberos) authentication.
    /// </summary>
    /// <param name="config">The GSSAPI configuration.</param>
    public AdminClientBuilder WithGssapi(GssapiConfig config)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = SaslMechanism.Gssapi;
        _gssapiConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Configures SASL/OAUTHBEARER authentication using OAuth 2.0 client credentials.
    /// </summary>
    /// <param name="config">The OAuth configuration describing the token endpoint and client.</param>
    public AdminClientBuilder WithOAuthBearer(OAuthBearerConfig config)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = config ?? throw new ArgumentNullException(nameof(config));
        _oauthTokenProvider = null;
        return this;
    }

    /// <summary>
    /// Configures SASL/OAUTHBEARER authentication using OAuth 2.0 JWT-bearer assertion flow.
    /// </summary>
    /// <param name="options">The JWT-bearer OAuth options.</param>
    public AdminClientBuilder WithOAuthBearerJwtBearer(OAuthBearerJwtBearerOptions options)
    {
        ThrowIfClientOwnedConnectionSettings();
        ArgumentNullException.ThrowIfNull(options);
        var oauthConfig = options.ToOAuthBearerConfig();
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthConfig = oauthConfig;
        _oauthTokenProvider = null;
        return this;
    }

    /// <summary>
    /// Configures SASL/OAUTHBEARER authentication using OAuth 2.0 JWT-bearer assertion flow.
    /// </summary>
    /// <param name="configure">Callback that configures the JWT-bearer OAuth options.</param>
    public AdminClientBuilder WithOAuthBearerJwtBearer(Action<OAuthBearerJwtBearerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new OAuthBearerJwtBearerOptions();
        configure(options);
        return WithOAuthBearerJwtBearer(options);
    }

    /// <summary>
    /// Configures SASL/OAUTHBEARER authentication using a custom token provider.
    /// </summary>
    /// <param name="tokenProvider">A callback that returns an OAuth bearer token on demand.</param>
    public AdminClientBuilder WithOAuthBearer(Func<CancellationToken, ValueTask<OAuthBearerToken>> tokenProvider)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = SaslMechanism.OAuthBearer;
        _oauthTokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _oauthConfig = null;
        return this;
    }

    /// <summary>
    /// Configures Amazon MSK IAM authentication using the AWS_MSK_IAM SASL mechanism.
    /// </summary>
    /// <param name="config">Optional AWS_MSK_IAM configuration. Defaults to the AWS credential chain and broker-derived region.</param>
    public AdminClientBuilder WithAwsMskIam(AwsMskIamConfig? config = null)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = SaslMechanism.AwsMskIam;
        _awsMskIamConfig = config ?? new AwsMskIamConfig();
        return this;
    }

    public AdminClientBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Registers an application metric for broker telemetry subscriptions on built admin clients.
    /// Duplicate names replace the previous metric.
    /// </summary>
    /// <param name="metric">The application metric to register.</param>
    public AdminClientBuilder RegisterMetricForSubscription(ApplicationTelemetryMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        _applicationMetrics[metric.Name] = metric;
        return this;
    }

    /// <summary>
    /// Removes an application metric registration from this builder.
    /// Missing names are ignored.
    /// </summary>
    /// <param name="name">The application metric name.</param>
    public AdminClientBuilder UnregisterMetricFromSubscription(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _applicationMetrics.Remove(name);
        return this;
    }

    /// <summary>
    /// Sets the metadata recovery strategy for when all known brokers become unavailable.
    /// </summary>
    /// <param name="strategy">The recovery strategy to use.</param>
    public AdminClientBuilder WithMetadataRecoveryStrategy(MetadataRecoveryStrategy strategy)
    {
        ThrowIfClientOwnedConnectionSettings();
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
        ThrowIfClientOwnedConnectionSettings();
        _metadataRecoveryRebootstrapTriggerMs = (int)trigger.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets how broker DNS names are resolved before connecting.
    /// </summary>
    public AdminClientBuilder WithClientDnsLookup(ClientDnsLookup lookup)
    {
        ThrowIfClientOwnedConnectionSettings();
        _clientDnsLookup = lookup;
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
        ThrowIfClientOwnedConnectionSettings();
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Metadata max age must be positive");

        _metadataMaxAge = interval;
        return this;
    }

    internal AdminClientBuilder WithRequestTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Request timeout must be positive");
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeout.TotalMilliseconds, int.MaxValue, nameof(timeout));

        _requestTimeoutMs = (int)timeout.TotalMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the initial delay before reconnecting to a broker after a connection failure.
    /// Equivalent to Kafka's <c>reconnect.backoff.ms</c>. Set to zero to disable the delay.
    /// </summary>
    /// <param name="backoff">The reconnect backoff. Cannot be negative.</param>
    public AdminClientBuilder WithReconnectBackoff(TimeSpan backoff)
    {
        ThrowIfClientOwnedConnectionSettings();
        _reconnectBackoffMs = ReconnectBackoffValidation.ToMilliseconds(
            backoff,
            nameof(backoff),
            "Reconnect backoff cannot be negative");
        return this;
    }

    /// <summary>
    /// Sets the maximum delay before reconnecting to a broker after repeated connection failures.
    /// Equivalent to Kafka's <c>reconnect.backoff.max.ms</c>.
    /// </summary>
    /// <param name="backoff">The maximum reconnect backoff. Cannot be negative.</param>
    public AdminClientBuilder WithReconnectBackoffMax(TimeSpan backoff)
    {
        ThrowIfClientOwnedConnectionSettings();
        _reconnectBackoffMaxMs = ReconnectBackoffValidation.ToMilliseconds(
            backoff,
            nameof(backoff),
            "Maximum reconnect backoff cannot be negative");
        return this;
    }

    /// <summary>
    /// Sets the maximum idle time before unused broker connections are closed.
    /// Equivalent to Kafka's <c>connections.max.idle.ms</c>. Use <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    /// <param name="idle">The maximum idle time. Must be non-negative, or <see cref="Timeout.InfiniteTimeSpan"/>.</param>
    public AdminClientBuilder WithConnectionsMaxIdle(TimeSpan idle)
    {
        ThrowIfClientOwnedConnectionSettings();
        _connectionsMaxIdleMs = ConnectionOptions.ToConnectionsMaxIdleMs(idle, nameof(idle));
        return this;
    }

    internal AdminClientBuilder WithSaslOptions(
        SaslMechanism mechanism,
        string? username,
        string? password,
        GssapiConfig? gssapiConfig,
        OAuthBearerConfig? oauthConfig,
        AwsMskIamConfig? awsMskIamConfig = null)
    {
        ThrowIfClientOwnedConnectionSettings();
        _saslMechanism = mechanism;
        _saslUsername = username;
        _saslPassword = password;
        _gssapiConfig = gssapiConfig;
        _oauthConfig = oauthConfig;
        _awsMskIamConfig = awsMskIamConfig ?? (mechanism == SaslMechanism.AwsMskIam ? new AwsMskIamConfig() : null);
        _oauthTokenProvider = null;
        return this;
    }

    public IAdminClient Build()
    {
        if (_bootstrapServers.Count == 0)
            throw new InvalidOperationException("Bootstrap servers must be specified");
        ReconnectBackoffValidation.ValidateMilliseconds(_reconnectBackoffMs, _reconnectBackoffMaxMs);

        GssapiConfig.ValidateForBuild(_saslMechanism, _gssapiConfig);

        var options = new AdminClientOptions
        {
            BootstrapServers = _bootstrapServers,
            ClientId = _clientId,
            RequestTimeoutMs = _requestTimeoutMs,
            ReconnectBackoffMs = _reconnectBackoffMs,
            ReconnectBackoffMaxMs = _reconnectBackoffMaxMs,
            ConnectionsMaxIdleMs = _connectionsMaxIdleMs,
            UseTls = _useTls,
            TlsConfig = _tlsConfig,
            SaslMechanism = _saslMechanism,
            SaslUsername = _saslUsername,
            SaslPassword = _saslPassword,
            GssapiConfig = _gssapiConfig,
            OAuthBearerConfig = _oauthConfig,
            OAuthBearerTokenProvider = _oauthTokenProvider,
            AwsMskIamConfig = _awsMskIamConfig,
            MetadataRecoveryStrategy = _metadataRecoveryStrategy,
            MetadataRecoveryRebootstrapTriggerMs = _metadataRecoveryRebootstrapTriggerMs,
            ClientDnsLookup = _clientDnsLookup,
            ApplicationMetrics = _applicationMetrics.Count > 0 ? _applicationMetrics.Values.ToArray() : []
        };

        var metadataOptions = new MetadataOptions
        {
            MetadataRefreshInterval = _metadataMaxAge ?? TimeSpan.FromMinutes(15),
            MetadataRecoveryStrategy = _metadataRecoveryStrategy,
            MetadataRecoveryRebootstrapTriggerMs = _metadataRecoveryRebootstrapTriggerMs,
            ClientDnsLookup = _clientDnsLookup
        };

        return _clientInfrastructure is null
            ? new AdminClient(options, _loggerFactory, metadataOptions)
            : new AdminClient(
                options,
                _clientInfrastructure.ConnectionPool,
                _clientInfrastructure.MetadataManager,
                _loggerFactory);
    }

    private void ThrowIfClientOwnedBootstrap()
    {
        if (_clientInfrastructure is not null)
            throw new InvalidOperationException("Bootstrap servers are owned by KafkaClient. Configure them on Kafka.Connect(...).");
    }

    private void ThrowIfClientOwnedConnectionSettings()
    {
        if (_clientInfrastructure is not null)
            throw new InvalidOperationException("Connection settings are owned by KafkaClient. Configure them on Kafka.Connect(...).");
    }
}
