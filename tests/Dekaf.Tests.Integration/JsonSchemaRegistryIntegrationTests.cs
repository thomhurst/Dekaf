using System.Text.Json;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for JSON Schema Registry serialization.
/// </summary>
[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class JsonSchemaRegistryIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    private const string TestOrderSchema = """
        {
            "type": "object",
            "properties": {
                "OrderId": { "type": "integer" },
                "CustomerName": { "type": "string" },
                "Amount": { "type": "number" }
            },
            "required": ["OrderId", "CustomerName", "Amount"]
        }
        """;

    private sealed class TestOrder
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    [Test]
    public async Task JsonSchemaRegistry_ProduceAndConsume_RoundTrips()
    {
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        var order = new TestOrder
        {
            OrderId = 1001,
            CustomerName = "Alice",
            Amount = 99.99m
        };

        await using var producer = await Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseJsonSchemaRegistry(registryClient, TestOrderSchema)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "order-1001",
            Value = order
        });

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(registryClient)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        TestOrder? consumed = null;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed = msg.Value;
            break;
        }

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.OrderId).IsEqualTo(1001);
        await Assert.That(consumed.CustomerName).IsEqualTo("Alice");
        await Assert.That(consumed.Amount).IsEqualTo(99.99m);
    }

    [Test]
    public async Task JsonSchemaRegistry_MultipleMessages_AllRoundTrip()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        const int messageCount = 10;

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var producer = await Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseJsonSchemaRegistry(registryClient, TestOrderSchema)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
            {
                Topic = topic,
                Key = $"order-{i}",
                Value = new TestOrder
                {
                    OrderId = i,
                    CustomerName = $"Customer {i}",
                    Amount = (i + 1) * 25.50m
                }
            });
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-multi-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(registryClient)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumedOrders = new List<TestOrder>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumedOrders.Add(msg.Value!);
            if (consumedOrders.Count >= messageCount) break;
        }

        await Assert.That(consumedOrders).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            var order = consumedOrders.First(o => o.OrderId == i);
            await Assert.That(order.CustomerName).IsEqualTo($"Customer {i}");
        }
    }

    [Test]
    public async Task JsonSchemaRegistry_RegistersSchemaInRegistry()
    {
        var topic = $"json-sr-schema-{Guid.NewGuid():N}";
        await testInfra.CreateTopicAsync(topic);

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var producer = await Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseJsonSchemaRegistry(registryClient, TestOrderSchema)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "test",
            Value = new TestOrder { OrderId = 1, CustomerName = "Test", Amount = 10.0m }
        });

        var subjects = await registryClient.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains($"{topic}-value");
    }

    [Test]
    public async Task JsonSchemaRegistry_CustomJsonOptions_RespectedDuringSerialization()
    {
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var order = new TestOrder
        {
            OrderId = 2002,
            CustomerName = "Bob",
            Amount = 150.00m
        };

        await using var producer = await Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseJsonSchemaRegistry(registryClient, TestOrderSchema, jsonOptions)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "order-2002",
            Value = order
        });

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-options-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(registryClient, jsonOptions)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        TestOrder? consumed = null;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed = msg.Value;
            break;
        }

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.OrderId).IsEqualTo(2002);
        await Assert.That(consumed.CustomerName).IsEqualTo("Bob");
    }
}
