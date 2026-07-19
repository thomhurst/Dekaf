---
sidebar_position: 5
---

# Migrate Confluent configuration

`Dekaf.Extensions.DependencyInjection` can translate existing Confluent-style `ProducerConfig`
and `ConsumerConfig` sections. This is a migration adapter: it accepts only settings that Dekaf
can represent exactly, then lets a fluent callback add or override native Dekaf settings.

```bash
dotnet add package Dekaf.Extensions.DependencyInjection
```

The package has **no runtime dependency on `Confluent.Kafka` or librdkafka**. Configuration is
read through `Microsoft.Extensions.Configuration` and translated directly to Dekaf builders.

## Aspire configuration

The [Aspire Kafka integration](https://aspire.dev/integrations/messaging/apache-kafka/apache-kafka-connect/)
uses these default sections:

```text
Aspire:Confluent:Kafka:Producer:Config
Aspire:Confluent:Kafka:Consumer:Config
```

Named clients use:

```text
Aspire:Confluent:Kafka:Producer:{name}:Config
Aspire:Confluent:Kafka:Consumer:{name}:Config
```

For example, this existing Aspire configuration needs no JSON rewrite:

```json
{
  "ConnectionStrings": {
    "kafka": "kafka:9092",
    "orders": "orders-kafka:9092"
  },
  "Aspire": {
    "Confluent": {
      "Kafka": {
        "Producer": {
          "Config": {
            "BootstrapServers": "json-default:9092",
            "ClientId": "default-producer",
            "Acks": "All",
            "MessageSendMaxRetries": 7
          },
          "orders": {
            "Config": {
              "BootstrapServers": "json-orders:9092",
              "ClientId": "orders-producer",
              "CompressionType": "Lz4"
            }
          }
        },
        "Consumer": {
          "Config": {
            "BootstrapServers": "json-default:9092",
            "ClientId": "default-consumer",
            "GroupId": "default-group",
            "AutoOffsetReset": "Latest",
            "FetchMinBytes": 4096
          },
          "orders": {
            "Config": {
              "BootstrapServers": "json-orders:9092",
              "ClientId": "orders-consumer",
              "GroupId": "orders-group",
              "AutoOffsetReset": "Earliest"
            }
          }
        }
      }
    }
  }
}
```

Pass each exact `Config` leaf to Dekaf. Use the Aspire resource name as the keyed DI service key:

```csharp
using Dekaf.Extensions.DependencyInjection;

var configuration = builder.Configuration;
var ordersProducerConfig = new ConfigurationBuilder()
    .AddConfiguration(configuration.GetSection("Aspire:Confluent:Kafka:Producer:Config"))
    .AddConfiguration(configuration.GetSection("Aspire:Confluent:Kafka:Producer:orders:Config"))
    .Build();
var ordersConsumerConfig = new ConfigurationBuilder()
    .AddConfiguration(configuration.GetSection("Aspire:Confluent:Kafka:Consumer:Config"))
    .AddConfiguration(configuration.GetSection("Aspire:Confluent:Kafka:Consumer:orders:Config"))
    .Build();

builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddProducerFromConfluentConfig<string, string>(
        configuration.GetSection("Aspire:Confluent:Kafka:Producer:Config"),
        producer => producer.WithBootstrapServers(
            configuration.GetConnectionString("kafka")!));

    dekaf.AddConsumerFromConfluentConfig<string, string>(
        configuration.GetSection("Aspire:Confluent:Kafka:Consumer:Config"),
        consumer => consumer
            .WithBootstrapServers(configuration.GetConnectionString("kafka")!)
            .SubscribeTo("events"));

    dekaf.AddProducerFromConfluentConfig<string, string>(
        "orders",
        ordersProducerConfig,
        producer => producer.WithBootstrapServers(
            configuration.GetConnectionString("orders")!));

    dekaf.AddConsumerFromConfluentConfig<string, string>(
        "orders",
        ordersConsumerConfig,
        consumer => consumer
            .WithBootstrapServers(configuration.GetConnectionString("orders")!)
            .SubscribeTo("orders"));
});
```

Resolve named clients with `[FromKeyedServices("orders")]` or
`GetRequiredKeyedService<T>("orders")`.

### Precedence

Aspire's Confluent integration applies settings in this order:

1. Default `Config` section.
2. Named `Config` section, when a client name is used.
3. `ConnectionStrings:{name}` into the Aspire client settings.
4. The Aspire settings callback.
5. The resulting connection string as `Config.BootstrapServers`.

Dekaf does not merge Aspire's default and named sections automatically. For a named client,
compose default then named with `AddConfiguration`, as above. Later providers override earlier
ones. The Dekaf fluent callback runs after translation, so calling
`WithBootstrapServers(GetConnectionString(name))` makes the connection string override a
`BootstrapServers` value inside JSON, matching Aspire's final precedence.

Aspire settings outside `Config`, such as health-check or tracing switches, are integration-host
settings rather than Kafka client properties. Configure equivalent application behavior
separately when replacing the Aspire Confluent integration.

## Compatibility matrix

Property names and enum values are case-insensitive. Hyphens and underscores in enum values are
ignored. An unknown property, unsupported value, or value that cannot be represented exactly
throws during DI registration; it is never silently ignored.

### Shared client properties

These properties are translated for both producers and consumers:

| Status | Confluent properties |
| --- | --- |
| Translated | `BootstrapServers`, `ClientDnsLookup`, `ClientId`, `ClientRack`, `ConnectionsMaxIdleMs`, `MetadataMaxAgeMs`, `MetadataRecoveryRebootstrapTriggerMs`, `MetadataRecoveryStrategy`, `ReconnectBackoffMs`, `ReconnectBackoffMaxMs`, `RetryBackoffMs`, `RetryBackoffMaxMs`, `SocketConnectionSetupTimeoutMs`, `SocketKeepaliveEnable`, `SocketNagleDisable=true`, `SocketReceiveBufferBytes`, `SocketSendBufferBytes` |
| Translated: TLS | `SecurityProtocol`, `EnableSslCertificateVerification`, `SslCaLocation`, `SslCertificateLocation`, `SslEndpointIdentificationAlgorithm`, `SslKeyLocation`, `SslKeyPassword`, `SslKeystoreLocation`, `SslKeystorePassword` |
| Translated: SASL | `SaslMechanism`, `SaslUsername`, `SaslPassword`, `SaslKerberosKeytab`, `SaslKerberosPrincipal`, `SaslKerberosServiceName`, `SaslOauthbearerClientId`, `SaslOauthbearerClientSecret`, `SaslOauthbearerGrantType`, `SaslOauthbearerMethod`, `SaslOauthbearerScope`, `SaslOauthbearerTokenEndpointUrl` |
| Rejected value | `SocketNagleDisable=false`; Dekaf always disables Nagle's algorithm |
| Native-only | Connection sharing, `ConnectionsPerBroker`, retry policies, interceptors, logging, and other Dekaf builder features |

TLS properties require `SecurityProtocol=Ssl` or `SaslSsl`. SASL properties require
`SecurityProtocol=SaslPlaintext` or `SaslSsl`. Unsupported security combinations fail at startup.

### Producer properties

| Status | Properties or behavior |
| --- | --- |
| Translated | `Acks`, `BatchSize`, `CompressionLevel`, `CompressionType`, `EnableIdempotence`, `LingerMs`, `MaxInFlight`, `MessageMaxBytes`, `MessageSendMaxRetries`, `MessageTimeoutMs`, `Partitioner`, `QueueBufferingMaxKbytes`, `RequestTimeoutMs`, `TransactionTimeoutMs`, `TransactionalId` |
| Native-only | Key/value serializers, producer interceptors, retry policy, presets, custom codecs including Brotli, explicit partitioner implementations, and connection-pool controls |
| Rejected property | Any producer property absent from the translated rows, including `EnableBackgroundPoll`, `QueueBufferingMaxMessages`, `DeliveryReportFields`, `EnableDeliveryReports`, `StatisticsIntervalMs`, and `Debug` |
| Rejected value | `MessageTimeoutMs=0` (infinite), a fractional `LingerMs`, or a `CompressionType`/`Partitioner` enum without an exact Dekaf mapping |

Supported compression values are `None`, `Gzip`, `Snappy`, `Lz4`, and `Zstd`. Supported
partitioner values are `Random`, `Consistent`, `ConsistentRandom`, `Murmur2`, `Murmur2Random`,
`Fnv1a`, and `Fnv1aRandom`.

### Consumer properties

| Status | Properties or behavior |
| --- | --- |
| Translated | `AutoCommitIntervalMs`, `AutoOffsetReset`, `CheckCrcs`, `ConsumeResultFields`, `EnableAutoCommit`, `EnableAutoOffsetStore`, `EnablePartitionEof`, `FetchMaxBytes`, `FetchMinBytes`, `FetchWaitMaxMs`, `GroupId`, `GroupInstanceId`, `GroupProtocol`, `GroupProtocolType`, `GroupRemoteAssignor`, `IsolationLevel`, `MaxPartitionFetchBytes`, `MaxPollIntervalMs`, `PartitionAssignmentStrategy`, `QueuedMaxMessagesKbytes`, `QueuedMinMessages` |
| Native-only | Subscription/manual assignment, safer offset-store timing, `ByDuration` offset reset, prefetch pipeline depth, rebalance listeners, retry policies, interceptors, partitioned processing, and dead-letter queues |
| Rejected property | Any consumer property absent from the translated rows, including `FetchErrorBackoffMs`, `SessionTimeoutMs`, `HeartbeatIntervalMs`, `StatisticsIntervalMs`, and `Debug` |
| Rejected value | `GroupProtocol=Classic`, `GroupProtocolType` other than `consumer`, `PartitionAssignmentStrategy` other than `Range`, `GroupRemoteAssignor` other than `range` or `uniform`, or `ConsumeResultFields` other than `all` |

`EnableAutoCommit=false` or `AutoCommitIntervalMs=0` selects manual commits. To match
librdkafka delivery semantics, the adapter selects `OffsetStoreTiming.OnDelivery`. Configure
Dekaf natively instead when you want its safer processing-completion semantics.

## CoreWCF Kafka migration

CoreWCF's [Kafka transport binding](https://corewcf.github.io/blog/2023/10/21/kafkabinding)
uses Confluent.Kafka underneath. Remove the CoreWCF Kafka package and Confluent runtime after all
Kafka endpoints have moved; the Dekaf adapter itself does not retain either dependency.

Suppose the old binding values are stored like this:

```json
{
  "CoreWCF": {
    "Kafka": {
      "Endpoint": "net.kafka://kafka:9092/orders",
      "ConsumerConfig": {
        "GroupId": "orders-service",
        "AutoOffsetReset": "Earliest",
        "IsolationLevel": "ReadCommitted"
      },
      "ProducerConfig": {
        "Acks": "All",
        "CompressionType": "Lz4"
      }
    }
  }
}
```

Bind the existing Confluent config leaves into Dekaf, while moving endpoint values to their
native Dekaf locations:

```csharp
using Dekaf.Extensions.DependencyInjection;

var kafka = builder.Configuration.GetSection("CoreWCF:Kafka");
var endpoint = new Uri(kafka["Endpoint"]!);
var bootstrapServers = endpoint.Authority;
var topic = endpoint.AbsolutePath.TrimStart('/');

builder.Services.AddDekaf(dekaf =>
{
    dekaf.AddConsumerFromConfluentConfig<string, byte[]>(
        kafka.GetSection("ConsumerConfig"),
        consumer => consumer
            .WithBootstrapServers(bootstrapServers)
            .SubscribeTo(topic));

    dekaf.AddProducerFromConfluentConfig<string, byte[]>(
        kafka.GetSection("ProducerConfig"),
        producer => producer.WithBootstrapServers(bootstrapServers));
});
```

Publish with `producer.ProduceAsync(topic, key, value)`. Replace the CoreWCF service contract and
message encoder with application handlers plus Dekaf serializers/deserializers.

| CoreWCF Kafka binding value | Dekaf migration |
| --- | --- |
| `net.kafka://host:port/topic` authority | `WithBootstrapServers("host:port")` or an Aspire connection string |
| Endpoint path | Consumer `SubscribeTo(topic)`; producer topic passed to `ProduceAsync` |
| `GroupId`, `AutoOffsetReset`, `IsolationLevel` | Keep in `ConsumerConfig`; the consumer adapter translates them |
| Custom binding `ConsumerConfig`/`ProducerConfig` values | Pass the appropriate configuration leaf to the matching adapter, subject to the matrix above |
| TLS/SASL transport properties | Keep supported properties in each `Config` leaf or configure Dekaf security builders directly |
| `DeliverySemantics` | Choose Dekaf commit/store timing and processing API explicitly; this is not a Confluent config property |
| `ErrorHandlingStrategy` and dead-letter queue | Configure Dekaf hosted-service error handling and dead-letter queue explicitly |
| Message encoding and CoreWCF contract dispatch | Replace with Dekaf serializers/deserializers and application handlers |

Do not pass the whole CoreWCF or Aspire parent section to an adapter. Pass an exact producer or
consumer `Config` leaf, or a default-plus-named merge of those leaves. Outer integration and
transport settings need explicit mapping.
