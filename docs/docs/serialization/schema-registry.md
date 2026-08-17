---
sidebar_position: 3
description: "Confluent Schema Registry with Avro and Protobuf, JSON Schema validation, and client-side field-level encryption via AWS, Azure, GCP, or Vault KMS."
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

For zero-allocation `GenericRecord` serialization, Avro map values must use
`Dictionary<string, object>`. Other `IDictionary<string, object>` implementations are rejected
because their enumeration can allocate per message. Value-type arrays and lists are specialized
for Avro primitives and built-in logical types; unsupported value-type element representations
fail instead of silently boxing each element. Custom logical branches in unions must declare one
sealed CLR type and have at most one value-dependent candidate for that type. Assignable or
multi-candidate custom logical dispatch is rejected during writer construction because it would
require a per-message candidate scan.

### With source-generated POCOs

`Dekaf.SchemaRegistry.Avro` includes source-generated POCO support for plain CLR models that do
not implement Apache Avro's `ISpecificRecord`. Opt in with `[AvroRecord]` on a top-level `partial`
class, record, or struct. The package's bundled source generator emits the schema and strongly
typed codec at build time; serialization uses constrained static dispatch with no reflection,
boxing, runtime schema walk, or codec lookup.

```csharp
using Dekaf.SchemaRegistry.Avro.Poco;

[AvroRecord(Name = "Order", Namespace = "example.orders")]
public sealed partial class Order
{
    [AvroField(Order = 0)]
    public required string Id { get; init; }

    [AvroField(Order = 1, Precision = 12, Scale = 2)]
    public decimal Total { get; init; }

    [AvroField(Order = 2, DefaultJson = "null")]
    public string? Note { get; init; }
}
```

```csharp
using Dekaf;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Avro.Poco;

using var registry = new SchemaRegistryClient(new SchemaRegistryConfig
{
    Url = "http://localhost:8081"
});

await using var producer = await Kafka.CreateProducer<string, Order>()
    .WithBootstrapServers("localhost:9092")
    .UseAvroPocoSchemaRegistry(registry)
    .BuildAsync();
```

Supported generated shapes are primitives, nullable members, enums, arrays, `List<T>`,
`Dictionary<string,T>`, nested `[AvroRecord]` types, and explicit unions configured with
`UnionTypes`. Only public fields and properties with public getters and setters are included;
class inheritance is rejected so inherited state cannot be omitted silently. Logical mappings are
`DateOnly` → `date`, `TimeOnly`/`TimeSpan` → `time-micros`, `DateTime`/`DateTimeOffset` →
`timestamp-micros`, `Guid` → `uuid`, and `decimal` → `decimal`. `TimeSpan` values must represent a
time of day from zero through less than 24 hours. `DateTimeKind.Unspecified` values follow
`DateTime.ToUniversalTime()` and therefore use the producer host's local time zone; use UTC
`DateTime` or `DateTimeOffset` for host-independent wire values. Decimal members require
`Precision` from 1 through 29 and `Scale` from 0 through the smaller of 28 and `Precision`.

Use `Name`, `Aliases`, and `DefaultJson` for schema evolution. Defaults must match the first Avro
union branch; nullable fields therefore use `DefaultJson = "null"`. Current generated defaults
support null, primitive, string, bytes, and enum values. Invalid shapes, cycles, duplicate
names/orders, ambiguous unions, and incompatible defaults fail compilation with `DKAVRO` diagnostics.

Call `WarmupAsync` before measuring or entering a latency-sensitive path. After warmup,
serialization is `0 B` per message for supported shapes. Deserialization allocates only the
returned class/record and declared arrays, lists, dictionaries, strings, or byte arrays; cached
schema-resolution plans add no per-message intermediate object graph. Rules remain opt-in and can
allocate according to the configured rule executor. Generated and standard collection writers use
one Avro block, allowing exact returned collection capacity. Valid external multi-block collections
are also accepted and may grow their returned backing storage as later blocks arrive.

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

Known `google/protobuf` imports are skipped by default, matching Confluent's built-in dependency
handling. This differs from earlier Dekaf releases. Set `SkipKnownTypes = false` if existing subjects
must retain explicit references to those imports.

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

`JsonSchemaValidationMode.Serialize` validates plaintext JSON after built-in domain rules and before
encoding rules. Custom rule executors are validated before their transform because they do not
expose phase boundaries. `Deserialize` validates after read rules and before JSON deserialization.
This ordering lets domain rules establish the final logical shape and encoding rules transform the
wire payload without validating ciphertext or a pre-migration shape.

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

## Migration rules

Set `UseLatestVersion = true` on a deserializer config to select the subject's latest registered
schema as the reader schema. Dekaf resolves the writer's exact subject version, walks every adjacent
version, and executes active migration rules before deserializing with the reader schema:

```csharp
var rules = new SchemaRegistryRuleExecutor([migrationHandler]);

var config = new AvroDeserializerConfig
{
    UseLatestVersion = true,
    RuleExecutor = rules
};

var consumer = await Kafka.CreateConsumer<string, GenericRecord>()
    .WithBootstrapServers("localhost:9092")
    .WithGroupId("orders")
    .UseAvroSchemaRegistry(registry, config)
    .BuildAsync();
```

`SchemaRegistryDeserializerConfig`, `AvroDeserializerConfig`, and `ProtobufDeserializerConfig`
all expose `UseLatestVersion`. Avro does not allow `UseLatestVersion` together with an explicit
`ReaderSchema`.

Ordering matches Schema Registry behavior. Read encoding rules run against the writer schema first;
upgrade or downgrade rules then run for each version edge; read domain rules run against the final
reader schema last. The higher schema owns each edge's migration rules. Upgrade paths visit versions
and rules in ascending/forward order. Downgrade paths visit versions and rules in descending/reverse
order. `UpDown` rules participate in both directions, and paired success/failure actions select the
first action for upgrade and the second for downgrade. Disabled rules are skipped.

Using the latest reader schema without active migration rules does not require a rule executor. If
an active migration path exists, configure the built-in `SchemaRegistryRuleExecutor`; Dekaf fails
closed instead of silently skipping the transform. Warm cached no-migration, disabled-migration, and
active pass-through paths remain allocation-free, including interleaved writer schema IDs.

Migration plans follow `SchemaRegistryConfig.LatestCacheTtlSecs`. The Confluent-compatible default
is `-1`, which disables time-based expiry. Set a non-negative TTL to
periodically re-resolve latest schemas; `0` refreshes on every use. Historical version lookup includes
deleted versions so migration paths remain complete. Custom `ISchemaRegistryClient` implementations
must override the deleted-version overload; its default implementation fails closed.

### JSONata rules

Install the optional `Dekaf.SchemaRegistry.Jsonata` package and register its handler when a data
contract contains `JSONATA` rules:

```csharp
using Dekaf.SchemaRegistry.Jsonata;

var rules = new SchemaRegistryRuleExecutor(
[
    new JsonataSchemaRegistryRuleHandler()
]);
```

The handler compiles and caches each rule expression, then evaluates JSONata against JSON codec
payloads for write, read, and migration transforms. For example,
`$merge([$, {'fullName': first & ' ' & last}])` preserves the input object and adds `fullName`.
JSONata dependencies remain isolated in the optional package; applications without the handler add
no JSONata work or allocation.

Transform results may be any JSON value, including `null`, numbers, objects, and collections.
JSONata's standard sequence semantics apply: a singleton sequence collapses to its value; append
`[]` when the output must remain an array. An undefined result (for example, a missing-field query)
fails explicitly rather than emitting invalid JSON. Condition rules must return `true` or `false`;
`false` fails the rule. Invalid expressions, malformed JSON, and non-JSON payload formats fail with
`SchemaRegistryRuleException`. Error messages identify the rule and engine error, but never include
the payload.

Binary Avro and Protobuf codec payloads are not currently supported by the JSONata byte handler and
are rejected explicitly. Their object-level transforms require codec-specific conversion before
binary encoding.

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

Each provider instance registers one KMS type. Applications using keys in multiple regions can give
each regional provider a distinct type and use that type on the matching KEK:

```csharp
using var euKms = new AwsKmsProvider(RegionEndpoint.EUWest2, type: "aws-kms-eu-west-2");
using var usKms = new AwsKmsProvider(RegionEndpoint.USEast1, type: "aws-kms-us-east-1");
var multiRegionCsfle = new SchemaRegistryCsfleRuleHandler(schemaRegistry, [euKms, usKms]);
```

## Azure Key Vault KMS

Install the opt-in Azure provider when Schema Registry client-side field-level encryption (CSFLE)
uses an Azure Key Vault key:

```bash
dotnet add package Dekaf.SchemaRegistry.Kms.Azure
```

```csharp
using Azure.Identity;
using Dekaf.SchemaRegistry;
using Dekaf.SchemaRegistry.Kms.Azure;

var credential = new DefaultAzureCredential();
var azureKms = new AzureKeyVaultKmsProvider(credential);
var confluentAzureKms = new AzureKeyVaultKmsProvider(
    credential,
    type: AzureKeyVaultKmsProvider.ConfluentType);
var csfle = new SchemaRegistryCsfleRuleHandler(
    schemaRegistry,
    [azureKms, confluentAzureKms]);
```

`DefaultAzureCredential` uses the standard Azure credential chain. Production applications can
instead pass a specific credential such as `ManagedIdentityCredential` or
`ClientSecretCredential`. The credential and any supplied `CryptographyClientOptions` are
caller-owned. For complete client-construction control, implement
`IAzureKeyVaultCryptographyClientFactory`.

Use an absolute HTTPS key identifier with `/keys/<name>` or `/keys/<name>/<version>`, for example
`https://payments.vault.azure.net/keys/orders-kek`. Azure public, US Government, and China Key Vault
and Managed HSM DNS authorities are accepted; other authorities are rejected before credential use.
Each provider instance registers one KMS type;
register the default instance for `azure-kv`, the `ConfluentType` instance for Confluent-compatible
`azure-kms`, or both as shown above. Matching `azure-kv://` and `azure-kms://` prefixes on the key
identifier are optional. The provider uses RSA-OAEP-256. Prefer a versioned key identifier so
existing data keeps decrypting after rotation. For a versionless key, set the KEK property
`encrypt.azure.key.version.save=true` to embed the exact Azure key version in newly wrapped key
material.

For RBAC-enabled vaults, grant the identity the Key Vault Crypto User role. For vaults using legacy
access policies, grant the `keys/wrapKey` and `keys/unwrapKey` permissions. Managed HSM uses its own
local RBAC system: grant the identity the
[Managed HSM Crypto User role](https://learn.microsoft.com/azure/key-vault/managed-hsm/role-management)
at the `/keys` scope or the specific key's scope. One provider instance is safe for concurrent use.
It bounds both its configured-key client cache and its ciphertext key-version client cache to 64
entries.
Cancellation is forwarded to Azure; provider error messages do not include service response text
or key material.

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
