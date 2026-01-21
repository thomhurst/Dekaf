using Dekaf.Consumer;
using Dekaf.Metadata;
using Dekaf.Networking;
using Dekaf.Producer;
using Dekaf.Protocol;
using Dekaf.Protocol.Messages;
using Dekaf.Serialization;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for protocol version negotiation and compatibility.
/// </summary>
[ClassDataSource<KafkaTestContainer>(Shared = SharedType.PerTestSession)]
public class ProtocolVersionTests(KafkaTestContainer kafka)
{
    [Test]
    public async Task ApiVersions_SuccessfullyNegotiates()
    {
        // This test verifies that ApiVersions request works and returns supported API versions
        // Arrange
        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var pool = new ConnectionPool("test-client", connectionOptions, null);
        var parts = kafka.BootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        try
        {
            var connection = await pool.GetConnectionAsync(host, port, CancellationToken.None);

            // Act - send ApiVersions v0 (most compatible)
            var request = new ApiVersionsRequest();
            var response = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                request,
                0,
                CancellationToken.None);

            // Assert
            await Assert.That(response.ErrorCode).IsEqualTo(ErrorCode.None);
            await Assert.That(response.ApiKeys.Count).IsGreaterThan(0);

            // Verify key APIs are present
            var apiKeyIds = response.ApiKeys.Select(k => k.ApiKey).ToHashSet();
            await Assert.That(apiKeyIds.Contains(ApiKey.Produce)).IsTrue();
            await Assert.That(apiKeyIds.Contains(ApiKey.Fetch)).IsTrue();
            await Assert.That(apiKeyIds.Contains(ApiKey.Metadata)).IsTrue();
            await Assert.That(apiKeyIds.Contains(ApiKey.FindCoordinator)).IsTrue();
            await Assert.That(apiKeyIds.Contains(ApiKey.JoinGroup)).IsTrue();
            await Assert.That(apiKeyIds.Contains(ApiKey.SyncGroup)).IsTrue();
            await Assert.That(apiKeyIds.Contains(ApiKey.Heartbeat)).IsTrue();
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task Metadata_SupportsExpectedVersions()
    {
        // Arrange
        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var pool = new ConnectionPool("test-client", connectionOptions, null);
        var parts = kafka.BootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        try
        {
            var connection = await pool.GetConnectionAsync(host, port, CancellationToken.None);

            // Get API versions
            var apiRequest = new ApiVersionsRequest();
            var apiResponse = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                apiRequest,
                0,
                CancellationToken.None);

            var metadataApi = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.Metadata);

            // Assert - Metadata should support at least v0-v9 (modern Kafka)
            await Assert.That((int)metadataApi.MinVersion).IsLessThanOrEqualTo(0);
            await Assert.That((int)metadataApi.MaxVersion).IsGreaterThanOrEqualTo(9);
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task Produce_SupportsExpectedVersions()
    {
        // Arrange
        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var pool = new ConnectionPool("test-client", connectionOptions, null);
        var parts = kafka.BootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        try
        {
            var connection = await pool.GetConnectionAsync(host, port, CancellationToken.None);

            // Get API versions
            var apiRequest = new ApiVersionsRequest();
            var apiResponse = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                apiRequest,
                0,
                CancellationToken.None);

            var produceApi = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.Produce);

            // Assert - Produce should support RecordBatch v2 format (v3+)
            await Assert.That((int)produceApi.MaxVersion).IsGreaterThanOrEqualTo(3);
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task Fetch_SupportsExpectedVersions()
    {
        // Arrange
        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var pool = new ConnectionPool("test-client", connectionOptions, null);
        var parts = kafka.BootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        try
        {
            var connection = await pool.GetConnectionAsync(host, port, CancellationToken.None);

            // Get API versions
            var apiRequest = new ApiVersionsRequest();
            var apiResponse = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                apiRequest,
                0,
                CancellationToken.None);

            var fetchApi = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.Fetch);

            // Assert - Fetch should support v4+ for isolation level support
            await Assert.That((int)fetchApi.MaxVersion).IsGreaterThanOrEqualTo(4);
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task GroupProtocol_SupportsExpectedVersions()
    {
        // Arrange
        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var pool = new ConnectionPool("test-client", connectionOptions, null);
        var parts = kafka.BootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        try
        {
            var connection = await pool.GetConnectionAsync(host, port, CancellationToken.None);

            // Get API versions
            var apiRequest = new ApiVersionsRequest();
            var apiResponse = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                apiRequest,
                0,
                CancellationToken.None);

            var joinGroup = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.JoinGroup);
            var syncGroup = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.SyncGroup);
            var heartbeat = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.Heartbeat);
            var leaveGroup = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.LeaveGroup);

            // Assert - should support modern group protocol
            await Assert.That((int)joinGroup.MaxVersion).IsGreaterThanOrEqualTo(5);
            await Assert.That((int)syncGroup.MaxVersion).IsGreaterThanOrEqualTo(3);
            await Assert.That((int)heartbeat.MaxVersion).IsGreaterThanOrEqualTo(3);
            await Assert.That((int)leaveGroup.MaxVersion).IsGreaterThanOrEqualTo(3);
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task Producer_NegotiatesCompatibleVersion_AndProduces()
    {
        // This test verifies that the producer correctly negotiates API versions
        // and can produce messages using the negotiated version
        var topic = await kafka.CreateTestTopicAsync();

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-version")
            .Build();

        // Act - producing should negotiate version automatically
        var metadata = await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        // Assert
        await Assert.That(metadata.Topic).IsEqualTo(topic);
        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Consumer_NegotiatesCompatibleVersion_AndConsumes()
    {
        // This test verifies that the consumer correctly negotiates API versions
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer")
            .Build();

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value"
        });

        // Act - consumer should negotiate versions automatically
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-version")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo("value");
    }

    [Test]
    public async Task FlexibleProtocol_WorksWithTaggedFields()
    {
        // Tests that flexible protocol versions (with tagged fields) work correctly
        // Flexible versions use COMPACT_ types and support tagged fields
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-flexible")
            .Build();

        // Produce with headers (uses flexible protocol features)
        var headers = new Headers
        {
            { "header1", "value1"u8.ToArray() }
        };

        await producer.ProduceAsync(new ProducerMessage<string, string>
        {
            Topic = topic,
            Key = "key",
            Value = "value",
            Headers = headers
        });

        // Consume
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-flexible")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await consumer.ConsumeOneAsync(TimeSpan.FromSeconds(30), cts.Token);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Headers).IsNotNull();
        await Assert.That(result.Headers!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RecordBatchV2_CorrectlyEncodesAndDecodes()
    {
        // Tests that RecordBatch v2 format (used in Kafka 0.11+) works correctly
        var topic = await kafka.CreateTestTopicAsync();
        var groupId = $"test-group-{Guid.NewGuid():N}";

        await using var producer = Dekaf.CreateProducer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-producer-batch")
            .Build();

        // Produce multiple messages (will be batched)
        var messagesToProduce = 5;
        for (var i = 0; i < messagesToProduce; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, string>
            {
                Topic = topic,
                Key = $"key-{i}",
                Value = $"value-{i}"
            });
        }

        // Consume all
        await using var consumer = Dekaf.CreateConsumer<string, string>()
            .WithBootstrapServers(kafka.BootstrapServers)
            .WithClientId("test-consumer-batch")
            .WithGroupId(groupId)
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .Build();

        consumer.Subscribe(topic);

        var messages = new List<ConsumeResult<string, string>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            messages.Add(msg);
            if (messages.Count >= messagesToProduce) break;
        }

        // Assert
        await Assert.That(messages).Count().IsEqualTo(messagesToProduce);
        for (var i = 0; i < messagesToProduce; i++)
        {
            await Assert.That(messages[i].Key).IsEqualTo($"key-{i}");
            await Assert.That(messages[i].Value).IsEqualTo($"value-{i}");
        }
    }

    [Test]
    public async Task FindCoordinator_SupportsV4Format()
    {
        // Tests that FindCoordinator v4 format (with multiple coordinators) works
        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var pool = new ConnectionPool("test-client", connectionOptions, null);
        var parts = kafka.BootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        try
        {
            var connection = await pool.GetConnectionAsync(host, port, CancellationToken.None);

            // Get API versions first
            var apiRequest = new ApiVersionsRequest();
            var apiResponse = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                apiRequest,
                0,
                CancellationToken.None);

            var findCoordApi = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.FindCoordinator);

            // Only test if v4 is supported
            if (findCoordApi.MaxVersion >= 4)
            {
                var request = new FindCoordinatorRequest
                {
                    Key = "test-group",
                    KeyType = CoordinatorType.Group
                };

                var response = await connection.SendAsync<FindCoordinatorRequest, FindCoordinatorResponse>(
                    request,
                    4,
                    CancellationToken.None);

                // v4 response has Coordinators array
                await Assert.That(response.Coordinators).IsNotNull();
                await Assert.That(response.Coordinators!.Count).IsGreaterThan(0);
            }
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }

    [Test]
    public async Task Metadata_SupportsV12FlexibleFormat()
    {
        // Tests that Metadata v12 (flexible) format works correctly
        var topic = await kafka.CreateTestTopicAsync();

        var connectionOptions = new ConnectionOptions
        {
            RequestTimeout = TimeSpan.FromSeconds(30)
        };

        var pool = new ConnectionPool("test-client", connectionOptions, null);
        var parts = kafka.BootstrapServers.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);

        try
        {
            var connection = await pool.GetConnectionAsync(host, port, CancellationToken.None);

            // Get API versions first
            var apiRequest = new ApiVersionsRequest();
            var apiResponse = await connection.SendAsync<ApiVersionsRequest, ApiVersionsResponse>(
                apiRequest,
                0,
                CancellationToken.None);

            var metadataApi = apiResponse.ApiKeys.FirstOrDefault(k => k.ApiKey == ApiKey.Metadata);

            // Test with v12 if supported
            var version = Math.Min((short)12, metadataApi.MaxVersion);
            if (version >= 9) // v9+ is flexible
            {
                var request = MetadataRequest.ForTopics(topic);

                var response = await connection.SendAsync<MetadataRequest, MetadataResponse>(
                    request,
                    version,
                    CancellationToken.None);

                await Assert.That(response.Brokers.Count).IsGreaterThan(0);
                await Assert.That(response.Topics.Count).IsGreaterThan(0);

                var topicInfo = response.Topics.FirstOrDefault(t => t.Name == topic);
                await Assert.That(topicInfo).IsNotNull();
            }
        }
        finally
        {
            await pool.DisposeAsync();
        }
    }
}
