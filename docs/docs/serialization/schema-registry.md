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

var producer = Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new AvroSerializer<Order>(schemaRegistry))
    .Build();

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

var producer = Kafka.CreateProducer<string, OrderProto>()
    .WithBootstrapServers("localhost:9092")
    .WithValueSerializer(new ProtobufSerializer<OrderProto>(schemaRegistry))
    .Build();
```

## Schema Registry Configuration

```csharp
var config = new SchemaRegistryConfig
{
    Url = "http://localhost:8081",

    // Authentication
    BasicAuthUserInfo = "username:password",

    // SSL
    SslCaLocation = "/path/to/ca.crt",
    SslKeystoreLocation = "/path/to/keystore.p12",
    SslKeystorePassword = "password"
};

var schemaRegistry = new CachedSchemaRegistryClient(config);
```

## Consumer

```csharp
using Dekaf;

var consumer = Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("order-processors")
    .WithValueDeserializer(new AvroDeserializer<Order>(schemaRegistry))
    .SubscribeTo("orders")
    .Build();

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
var serializer = new AvroSerializer<Order>(schemaRegistry, new AvroSerializerConfig
{
    SubjectNameStrategy = SubjectNameStrategy.TopicRecord
});
```

| Strategy | Subject Name |
|----------|--------------|
| Topic | `{topic}-value` |
| Record | `{record-name}` |
| TopicRecord | `{topic}-{record-name}` |
