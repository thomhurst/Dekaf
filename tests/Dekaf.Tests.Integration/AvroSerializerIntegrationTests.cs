using System.Buffers;
using Avro.Generic;
using Dekaf.Consumer;
using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;
using Dekaf.Serialization;
using AvroSchema = Avro.Schema;

namespace Dekaf.Tests.Integration;

/// <summary>
/// Integration tests for Avro serializer with Kafka and Schema Registry.
/// </summary>
[ClassDataSource<KafkaWithSchemaRegistryContainer>(Shared = SharedType.PerTestSession)]
public class AvroSerializerIntegrationTests(KafkaWithSchemaRegistryContainer testInfra)
{
    private const string SimpleRecordSchema = """
        {
            "type": "record",
            "name": "SimpleRecord",
            "namespace": "test",
            "fields": [
                { "name": "id", "type": "int" },
                { "name": "name", "type": "string" }
            ]
        }
        """;

    private const string UserRecordSchema = """
        {
            "type": "record",
            "name": "User",
            "namespace": "com.example",
            "fields": [
                { "name": "userId", "type": "long" },
                { "name": "username", "type": "string" },
                { "name": "email", "type": ["null", "string"], "default": null },
                { "name": "createdAt", "type": "long" }
            ]
        }
        """;

    [Test]
    public async Task AvroSerializer_ProduceAndConsume_RoundTrips()
    {
        // Arrange
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registryClient);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registryClient);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 42);
        record.Add("name", "Integration Test");

        // Act - Produce
        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-test-producer")
            .WithValueSerializer(serializer)
            .BuildAsync();

        var metadata = await producer.ProduceAsync(new ProducerMessage<string, GenericRecord>
        {
            Topic = topic,
            Key = "test-key",
            Value = record
        });

        await Assert.That(metadata.Offset).IsGreaterThanOrEqualTo(0);

        // Act - Consume
        await using var consumer = await Kafka.CreateConsumer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-test-consumer")
            .WithGroupId($"avro-test-group-{Guid.NewGuid():N}")
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

        // Assert
        await Assert.That(consumedRecord).IsNotNull();
        await Assert.That((int)consumedRecord!["id"]!).IsEqualTo(42);
        await Assert.That((string)consumedRecord["name"]!).IsEqualTo("Integration Test");
    }

    [Test]
    public async Task AvroSerializer_MultipleMessages_AllRoundTrip()
    {
        // Arrange
        var topic = await testInfra.CreateTestTopicAsync();
        const int messageCount = 10;

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registryClient);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registryClient);

        var schema = AvroSchema.Parse(UserRecordSchema) as Avro.RecordSchema;

        // Act - Produce multiple messages
        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-multi-producer")
            .WithValueSerializer(serializer)
            .BuildAsync();

        for (var i = 0; i < messageCount; i++)
        {
            var record = new GenericRecord(schema!);
            record.Add("userId", (long)i);
            record.Add("username", $"user-{i}");
            record.Add("email", $"user{i}@example.com");
            record.Add("createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await producer.ProduceAsync(new ProducerMessage<string, GenericRecord>
            {
                Topic = topic,
                Key = $"user-{i}",
                Value = record
            });
        }

        await producer.FlushAsync();

        // Act - Consume all messages
        await using var consumer = await Kafka.CreateConsumer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-multi-consumer")
            .WithGroupId($"avro-multi-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithValueDeserializer(deserializer)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var consumedRecords = new List<GenericRecord>();

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumedRecords.Add(msg.Value!);
            if (consumedRecords.Count >= messageCount)
                break;
        }

        // Assert
        await Assert.That(consumedRecords).Count().IsEqualTo(messageCount);

        for (var i = 0; i < messageCount; i++)
        {
            var record = consumedRecords.First(r => (long)r["userId"]! == i);
            await Assert.That((string)record["username"]!).IsEqualTo($"user-{i}");
            await Assert.That((string)record["email"]!).IsEqualTo($"user{i}@example.com");
        }
    }

    [Test]
    public async Task AvroSerializer_SchemaEvolution_HandlesNullableFields()
    {
        // Arrange
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registryClient);
        await using var deserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registryClient);

        var schema = AvroSchema.Parse(UserRecordSchema) as Avro.RecordSchema;

        // Create record with null email
        var record = new GenericRecord(schema!);
        record.Add("userId", 100L);
        record.Add("username", "nullemail-user");
        record.Add("email", null);
        record.Add("createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        // Act - Produce
        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-null-producer")
            .WithValueSerializer(serializer)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, GenericRecord>
        {
            Topic = topic,
            Key = "null-test",
            Value = record
        });

        // Act - Consume
        await using var consumer = await Kafka.CreateConsumer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-null-consumer")
            .WithGroupId($"avro-null-group-{Guid.NewGuid():N}")
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

        // Assert
        await Assert.That(consumedRecord).IsNotNull();
        await Assert.That((long)consumedRecord!["userId"]!).IsEqualTo(100L);
        await Assert.That((string)consumedRecord["username"]!).IsEqualTo("nullemail-user");
        await Assert.That(consumedRecord["email"]).IsNull();
    }

    [Test]
    public async Task AvroSerializer_WithKeySerializer_RoundTrips()
    {
        // Arrange
        var topic = await testInfra.CreateTestTopicAsync();

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var keySerializer = new AvroSchemaRegistrySerializer<GenericRecord>(registryClient);
        await using var valueSerializer = new AvroSchemaRegistrySerializer<GenericRecord>(registryClient);
        await using var keyDeserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registryClient);
        await using var valueDeserializer = new AvroSchemaRegistryDeserializer<GenericRecord>(registryClient);

        var keySchema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var valueSchema = AvroSchema.Parse(UserRecordSchema) as Avro.RecordSchema;

        var keyRecord = new GenericRecord(keySchema!);
        keyRecord.Add("id", 1);
        keyRecord.Add("name", "key-record");

        var valueRecord = new GenericRecord(valueSchema!);
        valueRecord.Add("userId", 999L);
        valueRecord.Add("username", "avro-key-test");
        valueRecord.Add("email", "keytest@example.com");
        valueRecord.Add("createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        // Act - Produce
        await using var producer = await Kafka.CreateProducer<GenericRecord, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-key-producer")
            .WithKeySerializer(keySerializer)
            .WithValueSerializer(valueSerializer)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<GenericRecord, GenericRecord>
        {
            Topic = topic,
            Key = keyRecord,
            Value = valueRecord
        });

        // Act - Consume
        await using var consumer = await Kafka.CreateConsumer<GenericRecord, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-key-consumer")
            .WithGroupId($"avro-key-group-{Guid.NewGuid():N}")
            .WithAutoOffsetReset(AutoOffsetReset.Earliest)
            .WithKeyDeserializer(keyDeserializer)
            .WithValueDeserializer(valueDeserializer)
            .BuildAsync();

        consumer.Subscribe(topic);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        GenericRecord? consumedKey = null;
        GenericRecord? consumedValue = null;

        await foreach (var msg in consumer.ConsumeAsync(cts.Token))
        {
            consumedKey = msg.Key;
            consumedValue = msg.Value;
            break;
        }

        // Assert
        await Assert.That(consumedKey).IsNotNull();
        await Assert.That((int)consumedKey!["id"]!).IsEqualTo(1);
        await Assert.That((string)consumedKey["name"]!).IsEqualTo("key-record");

        await Assert.That(consumedValue).IsNotNull();
        await Assert.That((long)consumedValue!["userId"]!).IsEqualTo(999L);
        await Assert.That((string)consumedValue["username"]!).IsEqualTo("avro-key-test");
    }

    [Test]
    public async Task AvroSerializer_RegistersSchemaInRegistry()
    {
        // Arrange
        var topic = $"avro-schema-test-{Guid.NewGuid():N}";
        await testInfra.CreateTopicAsync(topic);

        using var registryClient = new SchemaRegistryClient(new SchemaRegistryConfig
        {
            Url = testInfra.RegistryUrl
        });

        await using var serializer = new AvroSchemaRegistrySerializer<GenericRecord>(registryClient);

        var schema = AvroSchema.Parse(SimpleRecordSchema) as Avro.RecordSchema;
        var record = new GenericRecord(schema!);
        record.Add("id", 1);
        record.Add("name", "schema-test");

        // Act
        await using var producer = await Kafka.CreateProducer<string, GenericRecord>()
            .WithBootstrapServers(testInfra.BootstrapServers)
            .WithClientId("avro-schema-producer")
            .WithValueSerializer(serializer)
            .BuildAsync();

        await producer.ProduceAsync(new ProducerMessage<string, GenericRecord>
        {
            Topic = topic,
            Key = "test",
            Value = record
        });

        // Assert - verify schema was registered
        var subjects = await registryClient.GetAllSubjectsAsync();
        await Assert.That(subjects).Contains($"{topic}-value");
    }
}
