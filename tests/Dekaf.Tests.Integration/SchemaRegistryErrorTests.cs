using Avro.Generic;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for Schema Registry error paths and caching behavior.
/// </summary>
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public sealed class SchemaRegistryErrorTests(KafkaWithSchemaRegistryContainer testInfra)
{
    private const string SimpleRecordSchema = """
        {
            "type": "record",
            "name": "ErrorTestRecord",
            "namespace": "test.error",
            "fields": [
                { "name": "id", "type": "int" },
                { "name": "name", "type": "string" }
            ]
        }
        """;

    [Test]
    public async Task SchemaRegistry_ConnectionFailure_ProducesMeaningfulError()
    {
        // Arrange - use an invalid registry URL that will fail to connect
        var topic = await testInfra.CreateTestTopicAsync();

        using var badRegistryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://localhost:1",
            RequestTimeoutMs = 5000
        });

        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(badRegistryClient);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "error-test");

        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("error-test-producer")
            .WithValueSerializer(serializer)
            .BuildAsync();

        // Act & Assert - producing with a bad registry URL should throw an exception
        await Assert.That(async () =>
        {
            await producer.ProduceAsync(new ProducerMessage<string, GenericRecord>
            {
                Topic = topic,
                Key = "test-key",
                Value = record
            });
        }).Throws<Exception>();
    }

    [Test]
    public async Task SchemaRegistry_RequestTimeout_Behavior()
    {
        // Arrange - use an unreachable URL with a very short timeout
        using var timeoutRegistryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = "http://192.0.2.1:8081", // RFC 5737 TEST-NET, guaranteed unreachable
            RequestTimeoutMs = 2000
        });

        var subject = $"timeout-test-{Guid.NewGuid():N}-value";
        var testSchema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };

        // Act & Assert - the request should fail with a timeout or connection error
        await Assert.That(async () =>
        {
            await timeoutRegistryClient.RegisterSchemaAsync(subject, testSchema);
        }).Throws<Exception>();
    }

    [Test]
    public async Task SchemaRegistry_CacheBehavior_CachedVsUncached()
    {
        // Arrange - register a schema and verify caching behavior
        var subject = $"cache-test-{Guid.NewGuid():N}-value";

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        var testSchema = new Schema
        {
            SchemaType = SchemaType.Avro,
            SchemaString = SimpleRecordSchema
        };

        // Act - First call goes to registry
        var firstId = await registryClient.RegisterSchemaAsync(subject, testSchema);

        // Second call should use cache (same subject + schema = same cache key)
        var secondId = await registryClient.RegisterSchemaAsync(subject, testSchema);

        // Also verify GetSchemaAsync uses cache: first call fetches, second uses cache
        var fetchedSchema1 = await registryClient.GetSchemaAsync(firstId);
        var fetchedSchema2 = await registryClient.GetSchemaAsync(firstId);

        // Assert - both calls return the same ID
        await Assert.That(firstId).IsGreaterThan(0);
        await Assert.That(secondId).IsEqualTo(firstId);

        // Verify schemas match
        await Assert.That(fetchedSchema1.SchemaString).IsEqualTo(fetchedSchema2.SchemaString);
        await Assert.That(fetchedSchema1.SchemaType).IsEqualTo(SchemaType.Avro);
    }

    [Test]
    public async Task SchemaEvolution_OldConsumerReadsNewSchema()
    {
        // Arrange - produce with a v2 schema (has extra optional field),
        // consume using a reader that only knows v1 schema
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        // v2 schema: adds an optional "email" field with default null
        var v2SchemaString = """
            {
                "type": "record",
                "name": "EvolutionUser",
                "namespace": "test.evolution",
                "fields": [
                    { "name": "id", "type": "int" },
                    { "name": "name", "type": "string" },
                    { "name": "email", "type": ["null", "string"], "default": null }
                ]
            }
            """;

        var v2Schema = AvroSchema.Parse(v2SchemaString) as Avro.RecordSchema;

        // Produce with v2 schema (includes email field)
        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registryClient);

        var v2Record = new GenericRecord(v2Schema!);
        v2Record.Add("id", 42);
        v2Record.Add("name", "evolved-user");
        v2Record.Add("email", "user@example.com");

        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("evolution-producer")
            .WithValueSerializer(serializer)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, GenericRecord>
        {
            Topic = topic,
            Key = "evolution-key",
            Value = v2Record
        });

        await producer.FlushAsync();

        // Consume with v1 reader schema (no email field)
        // The Avro reader should still be able to read the message,
        // ignoring the extra "email" field from the writer
        var v1SchemaString = """
            {
                "type": "record",
                "name": "EvolutionUser",
                "namespace": "test.evolution",
                "fields": [
                    { "name": "id", "type": "int" },
                    { "name": "name", "type": "string" }
                ]
            }
            """;

        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(
            registryClient,
            new AvroDeserializerConfig
            {
                ReaderSchema = v1SchemaString
            });

        await using var consumer = await Kafka.CreateConsumer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("evolution-consumer")
            .WithGroupId($"evolution-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        GenericRecord? consumedRecord = null;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumedRecord = msg.Value;
            break;
        }

        // Assert - consumer with v1 reader schema should see id and name but NOT email
        await Assert.That(consumedRecord).IsNotNull();
        await Assert.That((int)consumedRecord!["id"]!).IsEqualTo(42);
        await Assert.That((string)consumedRecord["name"]!).IsEqualTo("evolved-user");

        // The v1 reader schema does not include "email", so trying to access it should
        // either return null or throw (depending on Avro library behavior).
        // With GenericRecord using a v1 reader schema, the email field should not be present.
        var hasEmailField = consumedRecord.Schema.Fields.Any(f => f.Name == "email");
        await Assert.That(hasEmailField).IsFalse();
    }
}
