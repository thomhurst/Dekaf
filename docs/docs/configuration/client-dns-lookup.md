---
sidebar_position: 4
---

# Client DNS Lookup

Dekaf resolves broker hostnames before opening TCP connections. By default it tries every A or AAAA record returned by DNS until one connects, then remembers the last successful address and tries it first on the next reconnect.

```csharp
var producer = new ProducerBuilder<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithClientDnsLookup(ClientDnsLookup.UseAllDnsIps)
    .Build();
```

Use `ResolveCanonicalBootstrapServersOnly` when a bootstrap alias must be canonicalized before TLS or SASL/GSSAPI authentication:

```csharp
var consumer = new ConsumerBuilder<string, string>()
    .WithBootstrapServers("bootstrap.kafka.example.com:9093")
    .WithClientDnsLookup(ClientDnsLookup.ResolveCanonicalBootstrapServersOnly)
    .UseTls()
    .Build();
```

## Modes

| Mode | Behavior |
|------|----------|
| `UseAllDnsIps` | Resolves all IP addresses for the broker hostname and tries each address until a connection succeeds. This is the default. |
| `ResolveCanonicalBootstrapServersOnly` | Resolves the canonical hostname and uses that name as the TLS/SASL target host while still trying the returned IP addresses. |

DNS is resolved on every reconnect, so changes in broker DNS records are picked up without recreating the client.

## Bootstrap resolution deadline

KIP-909 gives initial bootstrap DNS failures a separate retry budget. A transient host-not-found response does not consume the metadata initialization retry cap or timeout. Dekaf retries with the configured reconnect backoff until one bootstrap name resolves, cancellation/disposal occurs, or the bootstrap deadline expires.

```csharp
var producer = new ProducerBuilder<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithBootstrapResolveTimeout(TimeSpan.FromMinutes(2))
    .Build();
```

The default is 120000ms (2 minutes). Expiry throws the non-retriable `BootstrapResolutionException`, whose `UnresolvedBootstrapServers` property identifies the failed bootstrap endpoints. One resolvable name is sufficient when multiple bootstrap names are configured. `Build()` remains network-free; `BuildAsync()` and `InitializeAsync()` perform and retry the first network bootstrap.

## Configuration

`ClientDnsLookup` and `BootstrapResolveTimeoutMs` can be configured on root clients, producers, consumers, share consumers, and admin clients. Producer, consumer, and admin options also bind from `appsettings.json`:

```json
{
  "Kafka": {
    "Producers": {
      "Orders": {
        "BootstrapServers": "kafka.example.com:9092",
        "ClientDnsLookup": "UseAllDnsIps",
        "BootstrapResolveTimeoutMs": 120000
      }
    }
  }
}
```
