using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;
using Dekaf.Tests.Integration.Protos;
using Google.Protobuf;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for Protobuf serializer with Kafka and Schema Registry.
/// </summary>
[Category("Serialization")]
[ClassDataSource<KafkaWithAssociationSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class ProtobufSerializerIntegrationTests(KafkaWithAssociationSchemaRegistryContainer testInfra)
{
    [Test]
    public async Task ProtobufSerializer_ExplicitSchemaId_RoundTrips()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var schemaId = await registryClient.RegisterSchemaAsync($"{topic}-value", new Schema
        {
            SchemaType = SchemaType.Protobuf,
            SchemaString = TestPerson.Descriptor.File.SerializedData.ToBase64()
        });
        var person = new TestPerson { Id = 71, Name = "Explicit ID", Email = "id@example.com" };

        await using var producer = await Kafka.CreateProducer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseProtobufSchemaRegistry(registryClient, new ProtobufSerializerConfig
            {
                UseSchemaId = schemaId
            })
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(topic, "explicit", person);

        var consumed = await ConsumeOneAsync(topic, registryClient);

        await Assert.That(consumed.Id).IsEqualTo(person.Id);
        await Assert.That(consumed.Name).IsEqualTo(person.Name);
    }

    [Test]
    public async Task ProtobufSerializer_GuidHeader_RoundTrips()
    {
        var topic = await testInfra.CreateTestTopicAsync();
        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });
        var person = new TestPerson { Id = 72, Name = "GUID", Email = "guid@example.com" };

        await using var producer = await Kafka.CreateProducer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .UseProtobufSchemaRegistry(registryClient, new ProtobufSerializerConfig
            {
                SchemaIdStrategy = SchemaIdSerializerStrategy.Header
            })
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        await producer.ProduceAsync(topic, "guid", person);

        var consumed = await ConsumeOneAsync(topic, registryClient);

        await Assert.That(consumed.Id).IsEqualTo(person.Id);
        await Assert.That(consumed.Name).IsEqualTo(person.Name);
    }

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestPerson>
        {
            Topic = topic,
            Key = "proto-key",
            Value = person
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"proto-test-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseProtobufSchemaRegistry(registryClient)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
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
            }, CancellationToken.None);
        }

        await producer.FlushWithTimeoutAsync();

        await using var consumer = await Kafka.CreateConsumer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"proto-multi-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseProtobufSchemaRegistry(registryClient)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestPerson>
        {
            Topic = topic,
            Key = "test",
            Value = new TestPerson { Id = 1, Name = "Schema Test", Email = "schema@test.com" }
        }, CancellationToken.None);

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
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, TestPerson>
        {
            Topic = topic,
            Key = "default-fields",
            Value = person
        }, CancellationToken.None);

        await using var consumer = await Kafka.CreateConsumer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"proto-default-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseProtobufSchemaRegistry(registryClient)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory()).BuildAsync();

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

    private async Task<TestPerson> ConsumeOneAsync(string topic, ISchemaRegistryClient registryClient)
    {
        await using var consumer = await Kafka.CreateConsumer<string, TestPerson>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithGroupId($"proto-identity-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .UseProtobufSchemaRegistry(registryClient)
            .WithLoggerFactory(GlobalTestSetup.GetLoggerFactory())
            .BuildAsync();
        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await foreach (var message in consumer.ConsumeAsync(cts.Token))
            return message.Value!;

        throw new InvalidOperationException("The produced Protobuf message was not consumed.");
    }
}
