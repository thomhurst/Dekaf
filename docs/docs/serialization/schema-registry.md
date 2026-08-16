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

# Optional JSON Schema payload validation
dotnet add package Dekaf.SchemaRegistry.Json
```

`Dekaf.SchemaRegistry` keeps JSON validation disabled by default and does not depend on a JSON
Schema engine. Install `Dekaf.SchemaRegistry.Json` only in applications that enable validation.

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

## JSON Schema validation

JSON Schema Registry serialization does not validate payloads unless a
`JsonSchemaValidationOptions` instance is supplied. Enable validation independently for writes,
reads, or both:

```csharp
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Json;

var registry = new SchemaRegistryClient(new SchemaRegistryConfig
{
    Url = "http://localhost:8081"
});

var validation = new JsonSchemaValidationOptions
{
    ValidatorFactory = new StreamingJsonSchemaValidatorFactory(registry),
    Mode = JsonSchemaValidationMode.Both
};

var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .UseJsonSchemaRegistry(
        registry,
        orderSchema,
        jsonOptions: null,
        validationOptions: validation)
    .BuildAsync();

var consumer = await Kafka.CreateConsumer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders")
    .UseJsonSchemaRegistry(
        registry,
        jsonOptions: null,
        validationOptions: validation)
    .BuildAsync();
```

`JsonSchemaValidationMode.Serialize` validates plaintext JSON immediately after serialization and
before write rules. `Deserialize` validates after read rules and before JSON deserialization. This
ordering lets encryption and migration rules transform the wire payload without validating
ciphertext or a pre-migration shape.

Streaming validators are compiled once per exact registered `Schema` object and weakly cached. The
serializer fetches the complete registered schema after registration or lookup, so write validation
includes Schema Registry `references`. The deserializer compiles once when each schema ID is first
encountered. References are resolved by subject and version with a configurable 30-second default
timeout. Relative references resolve from the effective `$id`; internal JSON Pointer references are
supported directly.

The allocation-free evaluator supports the common structural and scalar assertion subset of Draft
7, 2019-09, and 2020-12: types and nullability, object properties and required fields, additional
properties, arrays and tuple items, size limits, and numeric limits. Unsupported assertion keywords
fail during cold validator compilation instead of being silently ignored. Configure reference
resolution and schema nesting limits when needed:

```csharp
var factory = new StreamingJsonSchemaValidatorFactory(registry, new StreamingJsonSchemaValidatorOptions
{
    ReferenceResolutionTimeout = TimeSpan.FromSeconds(10),
    MaxSchemaDepth = 192
});
```

Invalid payloads throw `JsonSchemaValidationException`. `SchemaId`, `Keyword`, and `JsonPath`
identify the failure. Exception messages never include payload contents. Validation has measurable
CPU cost when enabled because each payload must be parsed and evaluated. Steady-state validation is
zero-allocation; disabled serializers remain validation-neutral and do not load the optional JSON
Schema package.

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

## Google Cloud KMS

Install the opt-in Google Cloud provider when Schema Registry client-side field-level encryption
(CSFLE) uses a Cloud KMS key:

```bash
dotnet add package Dekaf.SchemaRegistry.Kms.Gcp
```

```csharp
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Gcp;

var gcpKms = new GcpKmsProvider();
var csfle = new SchemaRegistryCsfleRuleHandler(schemaRegistry, [gcpKms]);
```

The default constructor uses Google Application Default Credentials and the default Cloud KMS
endpoint. Build and inject a client to select a regional endpoint, explicit credentials, emulator,
or custom channel configuration:

```csharp
using Google.Cloud.Kms.V1;

var kmsClient = new KeyManagementServiceClientBuilder
{
    Endpoint = "europe-west2-kms.googleapis.com"
}.Build();
var gcpKms = new GcpKmsProvider(kmsClient);
```

The supplied `KeyManagementServiceClient` is caller-owned and safe to share. Use the full CryptoKey
resource name
`projects/<project>/locations/<location>/keyRings/<key-ring>/cryptoKeys/<key>`. An optional
`gcp-kms://` prefix is accepted. Cloud KMS embeds the primary key version in its ciphertext, so the
CryptoKey resource—not a CryptoKeyVersion—is used for both encryption and decryption.

Grant the runtime identity `cloudkms.cryptoKeyVersions.useToEncrypt` and
`cloudkms.cryptoKeyVersions.useToDecrypt`, for example with the Cloud KMS CryptoKey Encrypter/
Decrypter role. Cancellation is forwarded to the gRPC call. Provider errors omit service response
text and key material, and temporary SDK plaintext buffers are cleared after copy-out.

## HashiCorp Vault Transit KMS

Install the opt-in Vault provider when Schema Registry client-side field-level encryption (CSFLE)
uses a Transit secrets-engine key:

```bash
dotnet add package Dekaf.SchemaRegistry.Kms.Vault
```

For token authentication, create one shared `HttpClient` and read the token from your secret-delivery
mechanism, such as `VAULT_TOKEN` or a Vault Agent sink:

```csharp
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Vault;

var httpClient = new HttpClient();
var tokenProvider = new VaultStaticTokenProvider(
    Environment.GetEnvironmentVariable("VAULT_TOKEN")!);
var transitClient = new VaultTransitHttpClient(httpClient, tokenProvider);
var vaultKms = new VaultKmsProvider(
    transitClient,
    vaultAddress: new Uri("https://vault.example:8200"),
    vaultNamespace: Environment.GetEnvironmentVariable("VAULT_NAMESPACE"));
var csfle = new SchemaRegistryCsfleRuleHandler(schemaRegistry, [vaultKms]);
```

For AppRole, replace the token provider. The provider logs in through the configured auth mount and
caches each address/namespace token until shortly before its lease expires:

```csharp
var tokenProvider = new VaultAppRoleTokenProvider(
    httpClient,
    roleId: Environment.GetEnvironmentVariable("VAULT_APPROLE_ROLE_ID")!,
    secretId: Environment.GetEnvironmentVariable("VAULT_APPROLE_SECRET_ID")!,
    authMountPoint: "approle");
```

Use KMS type `hcvault` and a Confluent-compatible key identifier in
`https://<vault-address>/<mount>/keys/<key-name>` format, for example
`https://vault.example:8200/transit/keys/orders-kek`. The provider extracts `transit` as the mount
and `orders-kek` as the key name. For nested mounts, use a path such as
`https://vault.example:8200/team/transit/keys/orders-kek`. The key identifier's scheme, host, and
port must exactly match the locally configured `vaultAddress`; this prevents a Schema Registry KEK
from redirecting Vault credentials to another server. Configure `vaultAddress` as a root authority
without a path prefix. The namespace is sent as `X-Vault-Namespace`.

Grant `update` capability on `<mount>/encrypt/<key>` and `<mount>/decrypt/<key>`. The supplied
`HttpClient` is caller-owned; `VaultTransitHttpClient`, `VaultKmsProvider`, and both token providers
are safe for concurrent use. Cancellation reaches AppRole login and Transit HTTP requests. Errors
omit Vault response bodies, key material, ciphertext, role IDs, secret IDs, and tokens. Serialized
request/response buffers are zeroed before release. Credential and returned-token strings remain
managed .NET strings and cannot be zeroed; source them from a secret-delivery mechanism and limit
their lifetime accordingly.

## AWS KMS for client-side field-level encryption

Install `Dekaf.SchemaRegistry.Kms.Aws` only in applications that use AWS KMS. The AWS SDK dependency
stays out of `Dekaf.SchemaRegistry` and other serializer packages.

```csharp
using Amazon;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Aws;

using var awsKms = new AwsKmsProvider(RegionEndpoint.EUWest2);
var csfle = new SchemaRegistryCsfleRuleHandler(schemaRegistry, [awsKms]);
var rules = new SchemaRegistryRuleExecutor([csfle]);
```

`AwsKmsProvider()` uses the AWS SDK default credential and region provider chains. The region
constructor fixes the KMS endpoint while retaining the default credential chain. For custom
endpoints, retry settings, or other SDK options, pass an `AmazonKeyManagementServiceConfig`. For
explicit credentials or application-managed client lifetimes, construct an
`AmazonKeyManagementServiceClient` and pass it as `IAmazonKeyManagementService`; injected clients
remain caller-owned unless `ownsClient: true` is specified.

The AWS SDK default credential chain checks explicitly configured client credentials first, then
environment credentials, web-identity/container credentials, shared AWS profiles, and instance
metadata as applicable to the host. Prefer short-lived workload credentials over long-lived access
keys. The principal needs `kms:Encrypt` and `kms:Decrypt` for each configured key.

Schema Registry key references may contain a raw key ARN/alias or a Confluent-compatible
`aws-kms://` URI. Configure the provider's region or endpoint to match the key. The provider forwards
cancellation to the AWS SDK, is safe for concurrent use, never logs key material or ciphertext, and
clears temporary plaintext buffers where the runtime exposes them.

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
