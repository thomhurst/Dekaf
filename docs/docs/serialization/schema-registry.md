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

For zero-allocation `GenericRecord` serialization, Avro map values must use
`Dictionary<string, object>`. Other `IDictionary<string, object>` implementations are rejected
because their enumeration can allocate per message. Value-type arrays and lists are specialized
for Avro primitives and built-in logical types; unsupported value-type element representations
fail instead of silently boxing each element.

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

### HTTP pipeline customization

`SchemaRegistryClient` accepts a caller-owned `HttpMessageHandler`, or a handler factory whose
returned handler Dekaf owns. This supports custom tracing, retry, authentication, and policy
handlers without adding a dependency on `Microsoft.Extensions.Http`:

```csharp
var handler = new EnterprisePolicyHandler(new SocketsHttpHandler());
using var schemaRegistry = new SchemaRegistryClient(
    new SchemaRegistryConfig
    {
        Url = "https://schema-registry.example.com",
        UserAgent = "orders-service/2.1",
        DefaultHeaders = new Dictionary<string, string>
        {
            ["X-Tenant"] = "orders"
        }
    },
    handler);
```

Disposing `SchemaRegistryClient` never disposes a directly supplied `HttpMessageHandler`. The
`Func<HttpMessageHandler>` overload transfers ownership of the returned handler to Dekaf.
Authentication, timeout, failover, default headers, and the Schema Registry
`Accept` header remain active around every custom pipeline. Content headers such as `Content-Type`
cannot be configured as default request headers. `Accept` and `User-Agent` are also managed by
Dekaf and cannot be supplied through `DefaultHeaders`. When `UserAgent` is not set, Dekaf sends a
versioned `Dekaf.SchemaRegistry/{version}` value. `RequestTimeoutMs` must be positive, or `-1` for
an infinite timeout.

A custom handler owns proxy and TLS behavior completely. Therefore `Tls`, `Proxy`,
`UseProxy = false`, and the legacy `ClientCertificate` property cannot be combined with a custom
pipeline. The default pipeline supports the platform proxy or an explicit `IWebProxy`:

```csharp
var config = new SchemaRegistryConfig
{
    Url = "https://schema-registry.example.com",
    Proxy = new WebProxy("http://proxy.example.com:8080")
};
```

### Schema Registry TLS

The default pipeline supports caller-owned in-memory certificates and Dekaf-owned file or PEM
material through `SchemaRegistryTlsConfig`:

```csharp
var config = new SchemaRegistryConfig
{
    Url = "https://schema-registry.example.com",
    Tls = new SchemaRegistryTlsConfig
    {
        CaCertificatePath = "/run/secrets/schema-registry-ca", // PEM bundle or directory
        ClientCertificatePem = clientCertificatePem,
        ClientPrivateKeyPem = clientPrivateKeyPem,
        ClientCertificatePassword = privateKeyPassword,
        CheckCertificateRevocation = true
    }
};
```

Exactly one CA source and one client-certificate source may be configured. CA sources are a single
certificate, a collection, a PEM string, or a file/directory path. Directories load only `.pem`,
`.crt`, `.cer`, `.pfx`, and `.p12` files, in ordinal path order; an empty directory or any malformed
candidate fails client construction. PEM bundles load every certificate. Self-signed configured
certificates and explicitly configured intermediate CAs are trust anchors. Server-provided
intermediates are available to chain building without becoming trusted roots.

Client sources are an in-memory certificate with a private key, a PFX/P12 file, a PEM certificate
plus separate key files, or PEM certificate/key strings. Encrypted PEM keys and password-protected
PFX files use `ClientCertificatePassword`. Caller-provided certificate objects remain caller-owned;
Dekaf disposes every certificate it loads. A client PFX/P12 may contain its intermediate chain;
Dekaf retains and presents those intermediates with the leaf certificate. Passwords and private-key
text are never included in validation exceptions.

TLS material is loaded once when the client is constructed and applies identically to every
failover URL. To rotate file- or string-backed material, construct a new `SchemaRegistryClient` and
dispose the old instance after in-flight operations complete. `RemoteCertificateValidationCallback`
owns validation when set; otherwise `ValidateServerCertificate`,
`ValidateServerCertificateHostName`, custom roots, revocation, and protocol settings are enforced by
the default pipeline.

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

Avro generated `ISpecificRecord` types serialize without per-message allocations when their record
fields are scalar `null`, `boolean`, `int`, `long`, `float`, `double`, `string`, or `bytes` fields
exposed by matching public properties. Unsupported SpecificRecord shapes fail when the serializer is
created instead of silently falling back to Apache Avro's allocating `Get(int): object` path. Use
`GenericRecord` for collection, union, enum, fixed, logical, or nested record fields.

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
