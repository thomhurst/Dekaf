---
sidebar_position: 3
---

# Schema Registry

Dekaf integrates with Confluent Schema Registry for schema management and evolution with Avro and Protobuf serialization.

## Installation

```bash
# Core Schema Registry support
dotnet add package Dekaf.SchemaRegistry

# For Avro serialization
dotnet add package Dekaf.SchemaRegistry.Avro

# For Protobuf serialization
dotnet add package Dekaf.SchemaRegistry.Protobuf
```

## Avro Serialization

### With Generated Classes

```csharp
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro;

var schemaRegistry = new CachedSchemaRegistryClient(
    new SchemaRegistryConfig { Url = "http://localhost:8081" }
);

var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new AvroSerializer<Order>(schemaRegistry))
    .BuildAsync();

await producer.ProduceAsync("orders", order.Id, order);
```

### With Generic Records

```csharp
var serializer = new AvroSerializer<GenericRecord>(schemaRegistry);

var schema = (RecordSchema)Schema.Parse(@"{
    ""type"": ""record"",
    ""name"": ""Order"",
    ""fields"": [
        { ""name"": ""id"", ""type"": ""string"" },
        { ""name"": ""total"", ""type"": ""double"" }
    ]
}");

var record = new GenericRecord(schema);
record.Add("id", "order-123");
record.Add("total", 99.99);

await producer.ProduceAsync("orders", "order-123", record);
```

## Protobuf Serialization

```csharp
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Protobuf;

var schemaRegistry = new CachedSchemaRegistryClient(
    new SchemaRegistryConfig { Url = "http://localhost:8081" }
);

var producer = await Kafka.CreateProducer<string, OrderProto>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new ProtobufSerializer<OrderProto>(schemaRegistry))
    .BuildAsync();
```

Protobuf imports use Schema Registry references by default. If you provide a custom
`ISchemaRegistryClient`, implement `LookupSchemaAsync`; the serializer uses exact lookup to obtain
the assigned version for every non-well-known imported `.proto` schema, including after automatic
registration. The interface's default implementation throws `NotSupportedException` to identify
custom clients that need updating. Set `UseSchemaReferences = false` only to retain the legacy
registration behavior that omits references.

## Schema Registry Configuration

```csharp
var config = new SchemaRegistryConfig
{
    Url = "http://localhost:8081",

    // Choose one authentication source:
    BasicAuthUserInfo = "username:password",
    BearerAuthToken = "eyJhbGciOi...",
    OAuthBearerConfig = oauthConfig,
    OAuthBearerTokenProvider = GetSchemaRegistryTokenAsync,

    // Optional mTLS client certificate
    ClientCertificate = certificate
};

var schemaRegistry = new CachedSchemaRegistryClient(config);
```

## Consumer

```csharp
using Dekaf;

var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")
    .WithValueDeserializer(new AvroDeserializer<Order>(schemaRegistry))
    .SubscribeTo("orders")
    .BuildAsync();

await foreach (var msg in consumer.ConsumeAsync(ct))
{
    Order order = msg.Value;
    // Process order
}
```

## Schema Evolution

Schema Registry handles schema evolution:

```csharp
// V1: Original schema
public class OrderV1
{
    public string Id { get; set; }
    public decimal Total { get; set; }
}

// V2: Added field with default (backward compatible)
public class OrderV2
{
    public string Id { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "pending";  // New field with default
}
```

The serializer automatically registers new schema versions and handles compatibility.

## Subject Naming Strategies

```csharp
var serializer = new AvroSchemaRegistrySerializer<Order>(schemaRegistry, new AvroSerializerConfig
{
    SubjectNameStrategy = SubjectNameStrategy.TopicRecordName
});
```

| Strategy | Value or key subject |
|----------|----------------------|
| `TopicName` | `{topic}-value` or `{topic}-key` |
| `RecordName` | `{fully-qualified-record-name}` |
| `TopicRecordName` | `{topic}-{fully-qualified-record-name}` |

These formats match Confluent serializers. Avro `GenericRecord` subjects use the fullname from the
record's runtime schema, JSON Schema subjects use the schema `title` when present, and Protobuf
subjects use the message descriptor's full name.

When using the generic `SchemaRegistrySerializer` with a record-based strategy, prefer its
subject-independent `Func<Schema>` schema factory overload. The subject-aware `Func<string, Schema>`
overload may be called again with the schema-derived subject until the callback and schema name agree.

### Migrating subjects created before this fix

Older Dekaf releases appended `-key` or `-value` to `RecordName` and `TopicRecordName` subjects.
Before upgrading producers, register or copy each schema version from the old suffixed subject to
the standard subject. Keep the same compatibility mode and version order. For example:

- `com.example.Order-value` becomes `com.example.Order`.
- `orders-com.example.Order-key` becomes `orders-com.example.Order`.

If consumers or deployment sequencing require a gradual migration, keep the old names temporarily:

```csharp
var config = new AvroSerializerConfig
{
    SubjectNameStrategy = SubjectNameStrategy.RecordName,
    UseLegacySubjectNames = true
};
```

`UseLegacySubjectNames` is also available on `ProtobufSerializerConfig` and as an optional
constructor/builder-extension argument for JSON Schema and generic Schema Registry serializers.
It affects only the enum-based `RecordName` and `TopicRecordName` strategies; `TopicName` and custom
`ISubjectNameStrategy` implementations are unchanged. Disable the option after every producer and
schema has moved to the standard subject.
