using System.Text.Json;
using System.Text.Json.Serialization;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Json;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for JSON Schema Registry serialization.
/// </summary>
[Category("Serialization")]
[Timeout(120_000)]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class JsonSchemaRegistryIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task JsonWriterOptions_Produce_PreservesConfiguredPayload(bool schemaRegistry, CancellationToken cancellationToken)
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig { Url = testInfra.RegistryUrl });
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var typeInfo = new JsonSchemaRegistryIntegrationJsonContext(options).TestOrder;
        var order = new TestOrder { OrderId = 42, CustomerName = "<Alice> é", Amount = 12.50m };
        var builder = Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory());
        if (schemaRegistry)
            builder.UseJsonSchemaRegistry(registryClient, CamelCaseTestOrderSchema, typeInfo);
        else
            builder.WithValueSerializer(new Dekaf.Serialization.Json.JsonSerializer<TestOrder>(typeInfo));
        await using var producer = await builder.BuildAsync(cancellationToken);
        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "options",
            Value = order
        }, cancellationToken);

        await using var consumer = await Kafka.CreateConsumer<string, byte[]>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-writer-options-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);
        byte[]? consumed = null;
        await foreach (var message in consumer.ConsumeAsync(cancellationToken))
        {
            consumed = message.Value;
            break;
        }

        await Assert.That(consumed).IsNotNull();
        var json = System.Text.Encoding.UTF8.GetString(consumed.AsSpan(schemaRegistry ? 5 : 0));
        await Assert.That(json).IsEqualTo(JsonSerializer.Serialize(order, typeInfo));
    }

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

    private const string CamelCaseTestOrderSchema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "orderId": { "type": "integer" },
                "customerName": { "type": "string", "minLength": 1 },
                "amount": { "type": "number", "minimum": 0 }
            },
            "required": ["orderId", "customerName", "amount"]
        }
        """;

    [Test]
    [SkipWhenNativeAot("Reflection-based JSON schema registry serialization is not supported under NativeAOT; use the JsonTypeInfo overload.")]
    public async Task JsonSchemaRegistry_ProduceAndConsume_RoundTrips(CancellationToken cancellationToken)
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "order-1001",
            Value = order
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(registryClient)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(cancellationToken);

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
    public async Task JsonSchemaRegistry_ProduceAndConsume_WithJsonTypeInfo_RoundTrips(CancellationToken cancellationToken)
    {
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        var order = new TestOrder
        {
            OrderId = 3003,
            CustomerName = "Clara",
            Amount = 42.50m
        };

        await using var producer = await Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseJsonSchemaRegistry(
                registryClient,
                TestOrderSchema,
                JsonSchemaRegistryIntegrationJsonContext.Default.TestOrder)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "order-3003",
            Value = order
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-aot-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(
                registryClient,
                JsonSchemaRegistryIntegrationJsonContext.Default.TestOrder)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(cancellationToken);

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        TestOrder? consumed = null;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed = msg.Value;
            break;
        }

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.OrderId).IsEqualTo(3003);
        await Assert.That(consumed.CustomerName).IsEqualTo("Clara");
        await Assert.That(consumed.Amount).IsEqualTo(42.50m);
    }

    [Test]
    public async Task JsonSchemaValidation_RegisteredSchema_RoundTripsAndRejectsInvalidWrite(CancellationToken cancellationToken)
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var validation = new JsonSchemaValidationOptions
        {
            ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registryClient)
        };
        await using var producer = await Kafka.CreateProducer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseJsonSchemaRegistry(
                registryClient,
                CamelCaseTestOrderSchema,
                JsonSchemaRegistryIntegrationJsonContext.Default.TestOrder,
                validation)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "valid",
            Value = new TestOrder { OrderId = 4004, CustomerName = "Dana", Amount = 5.25m }
        }, cancellationToken);

        var exception = await Assert.ThrowsAsync<JsonSchemaValidationException>(
            () => producer.ProduceAsync(new ProducerMessage<string, TestOrder>
            {
                Topic = topic,
                Key = "invalid",
                Value = new TestOrder { OrderId = 4005, CustomerName = "", Amount = -1m }
            }).AsTask());
        await Assert.That(exception!.SchemaId).IsGreaterThan(0);

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-validation-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(
                registryClient,
                JsonSchemaRegistryIntegrationJsonContext.Default.TestOrder,
                validation)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);
        consumer.Subscribe(topic);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        TestOrder? consumed = null;

        await foreach (var message in consumer.ConsumeAsync(cts.Token))
        {
            consumed = message.Value;
            break;
        }

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.OrderId).IsEqualTo(4004);
    }

    [Test]
    [SkipWhenNativeAot("Reflection-based JSON schema registry serialization is not supported under NativeAOT; use the JsonTypeInfo overload.")]
    public async Task JsonSchemaRegistry_MultipleMessages_AllRoundTrip(CancellationToken cancellationToken)
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

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
            }, CancellationToken.None);
        }

        await producer.FlushWithTimeoutAsync();

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-multi-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(registryClient)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(cancellationToken);

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
    [SkipWhenNativeAot("Reflection-based JSON schema registry serialization is not supported under NativeAOT; use the JsonTypeInfo overload.")]
    public async Task JsonSchemaRegistry_RegistersSchemaInRegistry(CancellationToken cancellationToken)
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "test",
            Value = new TestOrder { OrderId = 1, CustomerName = "Test", Amount = 10.0m }
        }, CancellationToken.None);

        var subjects = await registryClient.GetAllSubjectsAsync(cancellationToken);
        await Assert.That(subjects).Contains($"{topic}-value");
    }

    [Test]
    [SkipWhenNativeAot("Reflection-based JSON schema registry serialization is not supported under NativeAOT; use the JsonTypeInfo overload.")]
    public async Task JsonSchemaRegistry_CustomJsonOptions_RespectedDuringSerialization(CancellationToken cancellationToken)
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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync(cancellationToken);

        await producer.ProduceAsync(new ProducerMessage<string, TestOrder>
        {
            Topic = topic,
            Key = "order-2002",
            Value = order
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, TestOrder>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"json-sr-options-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseJsonSchemaRegistry(registryClient, jsonOptions)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync(cancellationToken);

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

internal sealed class TestOrder
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TestOrder))]
internal sealed partial class JsonSchemaRegistryIntegrationJsonContext : JsonSerializerContext;
