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

## Configuration

`ClientDnsLookup` can be configured on root clients, producers, consumers, share consumers, and admin clients. It also binds from `appsettings.json` by enum name:

```json
{
  "Kafka": {
    "Producers": {
      "Orders": {
        "BootstrapServers": "kafka.example.com:9092",
        "ClientDnsLookup": "UseAllDnsIps"
      }
    }
  }
}
```
