using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dekaf.Consumer;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.Outbox;
using Dekaf.Outbox.DynamoDB;
using Dekaf.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dekaf.Tests.Integration;

/// <summary>
/// End to end: messages committed to DynamoDB Local with the business write are published to
/// a real broker by the relay and removed once acknowledged, and a stopping relay hands its
/// buckets to its peer.
/// </summary>
[Category("MessagingPatterns")]
public sealed class OutboxDynamoDbRelayTests(KafkaTestContainer kafka) : KafkaIntegrationTest(kafka)
{
    private const int BucketCount = 4;

    [ClassDataSource<DynamoDbLocalContainer>(Shared = SharedType.PerTestSession)]
    public required DynamoDbLocalContainer DynamoDb { get; init; }

    [Test]
    public async Task Relay_PublishesCommittedMessagesInKeyOrder_AndDrainsTheTable()
    {
        var topic = $"outbox-dynamodb-{Guid.NewGuid():N}";
        await KafkaContainer.CreateTopicAsync(topic, partitions: 3);
        using var client = DynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client, BucketCount);
        await using var pod = BuildPod(client, options, "relay-a", TimeSpan.FromSeconds(30));
        var writer = pod.GetRequiredService<IDynamoDbOutboxWriter>();
        var store = pod.GetRequiredService<IOutboxStore>();
        var services = pod.GetServices<IHostedService>().ToArray();
        foreach (var service in services)
            await service.StartAsync(CancellationToken.None);
        try
        {
            string[] keys = ["order-1", "order-2", "order-3", "order-4", "order-5"];
            for (var sequence = 0; sequence < 20; sequence++)
            {
                foreach (var key in keys)
                {
                    var message = OutboxMessage.Create(topic, key, sequence.ToString("D2"), Serializers.String,
                        Serializers.String, bucketCount: BucketCount);
                    // The business write and the message commit together.
                    await client.TransactWriteItemsAsync(new TransactWriteItemsRequest
                    {
                        TransactItems =
                        [
                            BusinessPut(options, $"{key}#{sequence}"),
                            await writer.CreateTransactWriteItemAsync(message)
                        ]
                    });
                    writer.NotifyCommitted(message);
                }
            }

            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId($"outbox-dynamodb-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync();
            consumer.Subscribe(topic);
            var records = await ConsumeMessagesAsync(consumer, keys.Length * 20);

            await Assert.That(records.Count).IsEqualTo(keys.Length * 20);
            var expected = string.Join(',', Enumerable.Range(0, 20).Select(sequence => sequence.ToString("D2")));
            foreach (var key in keys)
            {
                // The exact sequence: sorted and counted alone would let a duplicate stand
                // in for a missing value.
                var values = records.Where(record => record.Key == key).Select(record => record.Value);
                await Assert.That(string.Join(',', values)).IsEqualTo(expected);
            }

            await WaitForDrainedAsync(store);
        }
        finally
        {
            await StopAsync(services);
        }
    }

    [Test]
    public async Task StoppingRelay_HandsItsBucketsToThePeer_LongBeforeTheLeaseExpires()
    {
        var topic = $"outbox-dynamodb-handover-{Guid.NewGuid():N}";
        await KafkaContainer.CreateTopicAsync(topic, partitions: 1);
        using var client = DynamoDb.CreateClient();
        var options = await DynamoDbLocalContainer.CreateTableAsync(client, BucketCount);
        // A lease this long cannot expire during the test: only a release explains a takeover.
        var leaseDuration = TimeSpan.FromMinutes(10);
        await using var stopping = BuildPod(client, options, "relay-a", leaseDuration);
        await using var surviving = BuildPod(client, options, "relay-b", leaseDuration);
        var stoppingServices = stopping.GetServices<IHostedService>().ToArray();
        var survivingServices = surviving.GetServices<IHostedService>().ToArray();
        foreach (var service in stoppingServices.Concat(survivingServices))
            await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForOwnersAsync(client, options, owners =>
                owners.Count == BucketCount && owners.Count(owner => owner == "relay-a") == BucketCount / 2);

            await StopAsync(stoppingServices);
            await WaitForOwnersAsync(client, options, owners =>
                owners.Count == BucketCount && owners.All(owner => owner == "relay-b"));

            // One message per bucket, including the two the stopped relay owned.
            var writer = surviving.GetRequiredService<IDynamoDbOutboxWriter>();
            for (var bucket = 0; bucket < BucketCount; bucket++)
            {
                await writer.EnqueueAsync(new OutboxMessage
                {
                    MessageId = Guid.NewGuid(),
                    Bucket = bucket,
                    Topic = topic,
                    Value = Serialize($"bucket-{bucket}"),
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }

            await using var consumer = await Kafka.CreateConsumer<string, string>()
                .WithBootstrapServers(KafkaContainer.BootstrapServers)
                .WithGroupId($"outbox-dynamodb-handover-{Guid.NewGuid():N}")
                .WithAutoOffsetReset(AutoOffsetReset.Earliest).BuildAsync();
            consumer.Subscribe(topic);
            var records = await ConsumeMessagesAsync(consumer, BucketCount);

            await Assert.That(string.Join(',', records.Select(record => record.Value).Order(StringComparer.Ordinal)))
                .IsEqualTo("bucket-0,bucket-1,bucket-2,bucket-3");
            await WaitForDrainedAsync(surviving.GetRequiredService<IOutboxStore>());
        }
        finally
        {
            await StopAsync(survivingServices);
            // Already stopped on the success path; stopping twice is a no-op.
            await StopAsync(stoppingServices);
        }
    }

    private ServiceProvider BuildPod(
        IAmazonDynamoDB client, DynamoDbOutboxOptions options, string relayId, TimeSpan leaseDuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(client);
        // Registered the way the documentation shows it, inside AddDekaf.
        services.AddDekaf(dekaf => dekaf
            .AddDynamoDbOutboxStore(options)
            .AddOutboxRelay(
                producer => producer.WithBootstrapServers(KafkaContainer.BootstrapServers),
                new OutboxRelayOptions
                {
                    BucketCount = options.BucketCount,
                    RelayId = relayId,
                    LeaseDuration = leaseDuration,
                    LeaseRenewInterval = TimeSpan.FromMilliseconds(500),
                    PollInterval = TimeSpan.FromMilliseconds(200)
                }));
        return services.BuildServiceProvider();
    }

    private static async Task StopAsync(IHostedService[] services)
    {
        for (var index = services.Length - 1; index >= 0; index--)
            await services[index].StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));
    }

    private static async Task WaitForDrainedAsync(IOutboxStore store)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var buckets = Enumerable.Range(0, BucketCount).ToArray();
        while ((await store.GetBucketsWithPendingAsync(buckets, deadline.Token)).Count > 0)
            await Task.Delay(100, deadline.Token);
    }

    /// <summary>Polls the owner of every leased bucket until <paramref name="condition"/> holds.</summary>
    private static async Task WaitForOwnersAsync(
        IAmazonDynamoDB client, DynamoDbOutboxOptions options, Func<List<string>, bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (true)
        {
            var response = await client.QueryAsync(new QueryRequest
            {
                TableName = options.TableName,
                KeyConditionExpression = "PK = :pk AND begins_with(SK, :lease)",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":pk"] = new() { S = "OUTBOX#COORDINATION" },
                    [":lease"] = new() { S = "LEASE#" }
                },
                ConsistentRead = true
            }, deadline.Token);
            var owners = (response.Items ?? [])
                .Where(item => item.ContainsKey("Owner"))
                .Select(item => item["Owner"].S)
                .ToList();
            if (condition(owners))
                return;
            await Task.Delay(100, deadline.Token);
        }
    }

    private static byte[] Serialize(string value) => System.Text.Encoding.UTF8.GetBytes(value);

    private static TransactWriteItem BusinessPut(DynamoDbOutboxOptions options, string id) => new()
    {
        Put = new Put
        {
            TableName = options.TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "ORDER#" + id },
                ["SK"] = new() { S = "ORDER" }
            }
        }
    };
}
