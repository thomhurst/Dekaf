---
description: "Run Kafka and Schema Registry in an Aspire AppHost and register Dekaf producers, consumers, share consumers and Schema Registry clients with configuration, health checks and OpenTelemetry."
---

# Aspire

Dekaf ships three [Aspire](https://aspire.dev) integrations:

| Package | Use it in | What it adds |
| --- | --- | --- |
| `Dekaf.Aspire.Hosting` | The AppHost | A Kafka broker container, Kafka UI, and a Confluent Schema Registry container |
| `Dekaf.Aspire` | Services | Producers, consumers, share consumers, admin clients and shared root clients |
| `Dekaf.Aspire.SchemaRegistry` | Services | `ISchemaRegistryClient` for Dekaf's JSON Schema, Avro and Protobuf serializers |

The client packages work with any Kafka connection string, including the `Aspire.Hosting.Kafka` resource. `Dekaf.Aspire.Hosting` is the Dekaf-oriented alternative: its health check uses Dekaf, so the AppHost does not load librdkafka, and share groups are enabled on the broker.

A runnable AppHost and API live in [`samples/Aspire`](https://github.com/thomhurst/Dekaf/tree/main/samples/Aspire).

## AppHost

```bash
dotnet add package Dekaf.Aspire.Hosting
```

```csharp
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var kafka = builder.AddDekafKafka("messaging")
    .WithDataVolume()
    .WithKafkaUI();

var registry = builder.AddDekafSchemaRegistry("schema-registry", kafka);

builder.AddProject("orders", "../Orders/Orders.csproj")
    .WithReference(kafka)
    .WithReference(registry)
    .WaitFor(kafka)
    .WaitFor(registry);

builder.Build().Run();
```

`AddDekafKafka` runs a single-node KRaft broker from the `apache/kafka` image. Host processes connect through the `tcp` endpoint, and other containers connect through the `internal` endpoint on the Aspire container network. The broker is configured for local development:

- Share groups (KIP-932) are enabled, so share consumers work without broker changes.
- Internal topics, including the share group state topic, use a replication factor of one.
- `WithDataVolume` or `WithDataBindMount` keeps topics and schemas across restarts.
- `WithKafkaUI` adds one Kafka UI container that manages every broker in the application.

The broker health check describes the cluster with a Dekaf admin client.

`AddDekafSchemaRegistry` runs `confluentinc/cp-schema-registry`. It stores schemas in the broker's `_schemas` topic, waits for the broker to be healthy, and reports ready once `/subjects` responds. When Aspire has an HTTPS certificate for the resource, such as the ASP.NET Core development certificate, the registry serves HTTPS on the same endpoint.

`WithReference` supplies these connection strings:

| Resource | Connection string | Connection properties |
| --- | --- | --- |
| Kafka | Bootstrap servers, `host:port` | `Host`, `Port` |
| Schema Registry | Registry URL | `Host`, `Port`, `Uri` |

Kafka's connection string addresses the host endpoint, so `WithReference(kafka)` suits projects and other host processes. A container that needs the broker should use `kafka.Resource.InternalEndpoint` instead, as the Schema Registry and Kafka UI containers do.

The containers are for development. Publishing emits the same resources with deferred endpoint expressions, but it does not configure authentication, TLS on the broker, or replication.

## Services

```bash
dotnet add package Dekaf.Aspire
dotnet add package Dekaf.Aspire.SchemaRegistry
```

Register clients by the connection name used in the AppHost:

```csharp
using Dekaf.Consumer;
using Dekaf.SchemaRegistry;

builder.AddDekafProducer<string, string>("messaging");
builder.AddDekafConsumer<string, string>("messaging", consumer => consumer
    .WithGroupId("orders-service")
    .SubscribeTo("orders"));
builder.AddDekafAdminClient("messaging");

builder.AddDekafSchemaRegistryClient("schema-registry");
builder.AddDekafProducer<string, Order>("messaging", configureBuilder: (services, producer) => producer
    .UseJsonSchemaRegistry(services.GetRequiredService<ISchemaRegistryClient>(), orderJson));
```

Inject `IKafkaProducer<TKey, TValue>`, `IKafkaConsumer<TKey, TValue>`, `IAdminClient` or `ISchemaRegistryClient`. The host initializes producers and consumers at startup and disposes every client on shutdown.

Each method has an `AddKeyed...` variant that uses the name as the service key, for multiple clusters or several clients with the same message types:

```csharp
builder.AddKeyedDekafProducer<string, string>("orders");
builder.AddKeyedDekafProducer<string, string>("payments");

// Resolve with [FromKeyedServices("payments")] or:
var app = builder.Build();
var payments = app.Services.GetRequiredKeyedService<IKafkaProducer<string, string>>("payments");
```

Every registration takes two optional callbacks: `configureSettings` for the Aspire settings below, and `configureBuilder` for the native Dekaf builder. The builder callback receives the `IServiceProvider`, so serializers, interceptors and other application services can be resolved from it.

### Share consumers

Share consumers (KIP-932) need Kafka 4.2 or later with share groups enabled, which `AddDekafKafka` provides:

```csharp
builder.AddDekafShareConsumerService<ShareOrderWorker, string, string>("messaging",
    configureBuilder: (services, consumer) => consumer.WithGroupId("order-workers"));
```

`AddDekafShareConsumerService` registers a consumer together with a `KafkaShareConsumerService<TKey, TValue>` that processes its records, with explicit acknowledgement by default. Each service type and key pair gets its own consumer and health check. Use `AddDekafShareConsumer` to register just the consumer. Create topics before a share consumer subscribes to them.

### Shared root clients

`AddDekafClient` registers a root `KafkaClient`. Producers, consumers and admin clients created from it share its connections, metadata and memory budget:

```csharp
builder.AddDekafClient("messaging", configureBuilder: (services, client) => client.WithClientId("orders"));
```

The host owns the root client. Initialize and dispose the child clients you create from it.

## Configuration

Settings are read from `Aspire:Dekaf:{Role}`, where the role is `Producer`, `Consumer`, `ShareConsumer`, `AdminClient`, `Client` or `SchemaRegistry`. A subsection named after the connection overrides the shared settings for that registration:

```json
{
  "ConnectionStrings": {
    "messaging": "localhost:9092"
  },
  "Aspire": {
    "Dekaf": {
      "Producer": {
        "Config": {
          "Acks": "All",
          "LingerMs": 5
        }
      },
      "Consumer": {
        "Config": {
          "GroupId": "orders-service",
          "AutoOffsetReset": "Earliest"
        },
        "HealthCheck": {
          "Timeout": "00:00:05",
          "DegradedThreshold": 1000
        },
        "payments": {
          "Config": {
            "GroupId": "payments-service"
          }
        }
      }
    }
  }
}
```

Native options under `Config` use Dekaf's option names, the same as [dependency injection](./dependency-injection) configuration. Precedence, from lowest to highest:

1. The shared role section.
2. The section named after the connection.
3. `ConnectionStrings:{name}`.
4. `configureSettings`.
5. `configureBuilder`.

Connection fields are replaced as a unit: when a named section sets `ConnectionString` or `Config:BootstrapServers`, none of the shared broker addresses are kept. An admin client connection string also replaces configured `BootstrapControllers`.

A Schema Registry connection string holds one or more comma-separated registry URLs, and replaces configured `Config:Url` and `Config:Urls`. Other `SchemaRegistryConfig` options, such as authentication and caching, still bind from `Aspire:Dekaf:SchemaRegistry:Config`.

The packages include an `appsettings.json` schema, so editors complete these settings.

Configuration binding uses reflection, so the registration methods are annotated with `RequiresDynamicCode` and `RequiresUnreferencedCode`. For Native AOT, register clients with [dependency injection](./dependency-injection) typed options instead.

## Health checks

Health checks are on by default. Set `DisableHealthChecks` to turn one off.

| Registration | Check name | What it checks |
| --- | --- | --- |
| Producer | `Dekaf_producer<TKey,TValue>` | A flush checkpoint completes within the timeout |
| Consumer | `Dekaf_consumer<TKey,TValue>` | Group liveness and partition lag |
| Share consumer | `Dekaf_shareconsumer<TKey,TValue>` | Stable share group membership with a fresh heartbeat |
| Share consumer service | `Dekaf_shareconsumer_service<TService,TKey,TValue>` | The same, for that service's own consumer |
| Admin client | `Dekaf_admin` | A cluster description request returns at least one broker |
| Root client | `Dekaf_client` | A cluster description request through the root's connections |
| Schema Registry | `Dekaf_schema_registry` | Listing subjects succeeds |

Type arguments are written as full .NET type names, for example `Dekaf_producer<System.String,MyApp.Order>`, and keyed registrations append `_{name}`. See [health checks](./health-checks) for exactly what each check proves; for example, a producer check does not prove broker connectivity.

## Telemetry

Each registration subscribes OpenTelemetry to Dekaf's meter and activity source. Set `DisableMetrics` or `DisableTracing` to opt a registration out. Dekaf uses one meter and one source for all clients, so another registration can still collect the same signals. See [observability](./observability).

## Migrating from Aspire.Confluent.Kafka

Replace `AddKafkaProducer` and `AddKafkaConsumer` with `AddDekafProducer` and `AddDekafConsumer`, and inject Dekaf's client interfaces. The connection string and the AppHost Kafka resource can stay the same. Native settings move from Confluent names under `Aspire:Confluent:Kafka` to Dekaf names under `Aspire:Dekaf`. The [migration guide](./migrating-from-confluent-kafka) covers the client API differences.
