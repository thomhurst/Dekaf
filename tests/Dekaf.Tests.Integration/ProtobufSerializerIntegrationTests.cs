using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Tests.Integration.Protos;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for Protobuf serializer with Kafka and Schema Registry.
/// </summary>
[Category("Serialization")]
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class ProtobufSerializerIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task ProtobufSerializer_ProduceAndConsume_RoundTrips()
    {
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        var person = new TestPerson
        {
            Id = 42,
            Name = "Integration Test",
            Email = "test@example.com"
        };

        await using var producer = await Kafka.CreateProducer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseProtobufSchemaRegistry(registryClient)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestPerson>
        {
            Topic = topic,
            Key = "proto-key",
            Value = person
        });

        await using var consumer = await Kafka.CreateConsumer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"proto-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseProtobufSchemaRegistry(registryClient)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        TestPerson? consumed = null;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed = msg.Value;
            break;
        }

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Id).IsEqualTo(42);
        await Assert.That(consumed.Name).IsEqualTo("Integration Test");
        await Assert.That(consumed.Email).IsEqualTo("test@example.com");
    }

    [Test]
    public async Task ProtobufSerializer_MultipleMessages_AllRoundTrip()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        const int messageCount = 10;

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var producer = await Kafka.CreateProducer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseProtobufSchemaRegistry(registryClient)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            await producer.ProduceAsync(new ProducerMessage<string, TestPerson>
            {
                Topic = topic,
                Key = $"person-{i}",
                Value = new TestPerson
                {
                    Id = i,
                    Name = $"Person {i}",
                    Email = $"person{i}@example.com"
                }
            });
        }

        await producer.FlushAsync();

        await using var consumer = await Kafka.CreateConsumer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"proto-multi-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseProtobufSchemaRegistry(registryClient)
            .BuildAsync();

        consumer.Subscribe(topic);

        var consumedPersons = new List<TestPerson>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumedPersons.Add(msg.Value!);
            if (consumedPersons.Count >= messageCount) break;
        }

        await Assert.That(consumedPersons).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            var person = consumedPersons.First(p => p.Id == i);
            await Assert.That(person.Name).IsEqualTo($"Person {i}");
            await Assert.That(person.Email).IsEqualTo($"person{i}@example.com");
        }
    }

    [Test]
    public async Task ProtobufSerializer_RegistersSchemaInRegistry()
    {
        var topic = $"proto-schema-test-{Guid.NewGuid():N}";
        await testInfra.CreateTopicAsync(topic);

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var producer = await Kafka.CreateProducer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseProtobufSchemaRegistry(registryClient)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestPerson>
        {
            Topic = topic,
            Key = "test",
            Value = new TestPerson { Id = 1, Name = "Schema Test", Email = "schema@test.com" }
        });

        var subjects = await registryClient.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains($"{topic}-value");
    }

    [Test]
    public async Task ProtobufSerializer_DefaultFields_HandledCorrectly()
    {
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        // Create a person with default (empty) fields
        var person = new TestPerson
        {
            Id = 0,
            Name = "",
            Email = ""
        };

        await using var producer = await Kafka.CreateProducer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseProtobufSchemaRegistry(registryClient)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestPerson>
        {
            Topic = topic,
            Key = "default-fields",
            Value = person
        });

        await using var consumer = await Kafka.CreateConsumer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"proto-default-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseProtobufSchemaRegistry(registryClient)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        TestPerson? consumed = null;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumed = msg.Value;
            break;
        }

        await Assert.That(consumed).IsNotNull();
        await Assert.That(consumed!.Id).IsEqualTo(0);
        await Assert.That(consumed.Name).IsEqualTo("");
        await Assert.That(consumed.Email).IsEqualTo("");
    }
}
