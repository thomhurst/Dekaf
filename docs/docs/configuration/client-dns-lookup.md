---
sidebar_position: 4
description: "How Dekaf resolves broker hostnames before connecting, the available DNS lookup modes, and the bootstrap resolution deadline."
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

Successful-address preferences are shared process-wide and retain at most 1,024
`(hostname, port, lookup mode)` entries. Entries are grouped into 256 buckets of
four, with the oldest entry in a bucket replaced when that bucket fills. Repeated
successes update an existing entry without adding another. After eviction, the
next connection uses the current DNS order; all-address fallback still applies.
A remembered address is preferred only while it remains in the latest DNS result.

## Bootstrap resolution deadline

KIP-909 gives initial bootstrap DNS failures a separate retry budget. A transient host-not-found response does not consume the metadata initialization retry cap or timeout. Dekaf retries until one bootstrap name resolves, cancellation/disposal occurs, or the bootstrap deadline expires. Retries back off exponentially from `RetryBackoffMs` up to `RetryBackoffMaxMs`; the Java client waits a fixed `retry.backoff.ms` between attempts.

```csharp
var producer = new ProducerBuilder<string, string>()
    .WithBootstrapServers("kafka.example.com:9092")
    .WithBootstrapResolveTimeout(TimeSpan.FromMinutes(2))
    .Build();
```

The default is 120000ms (2 minutes), the value from the original KIP-909 proposal. Kafka 4.4 ships `bootstrap.resolve.timeout.ms` with a default of 0 and marks positive values as evolving, so a Java client and a Dekaf client with default settings differ: `BuildAsync()` can wait up to 2 minutes for DNS, then up to the metadata initialization timeout. A consumer's `InitializeAsync()` is also bounded by `DefaultApiTimeoutMs` (60 seconds by default), which can end initialization before the bootstrap deadline does. Set the timeout to 0 to match Java: Dekaf then resolves once with no artificial deadline, and only a real DNS failure fails initialization.

Expiry throws the non-retriable `BootstrapResolutionException`, whose `UnresolvedBootstrapServers` property identifies the failed bootstrap endpoints. One resolvable name is sufficient when multiple bootstrap names are configured. `Build()` remains network-free; `BuildAsync()` and `InitializeAsync()` perform and retry the first network bootstrap.

In Java, `BootstrapResolutionException` is permanent and the client must be recreated. In Dekaf the client stays uninitialized, so calling `InitializeAsync()` again (or the first operation that initializes the client) starts a new resolution with a fresh deadline. Recreating the client is not required.

## Rebootstrap

When metadata recovery rebootstraps (all known brokers unavailable for `MetadataRecoveryRebootstrapTriggerMs`, or a broker returns `REBOOTSTRAP_REQUIRED`), Dekaf resolves each bootstrap server again with the configured `ClientDnsLookup` mode. Each lookup is capped at 5 seconds, independently of `BootstrapResolveTimeoutMs`, because callers are waiting on the metadata refresh. Dekaf then tries each resolved address in turn, always connecting by the bootstrap hostname rather than a raw IP address, so the TLS target and SASL/GSSAPI service host come from the hostname, as on any other connection: with `UseAllDnsIps` they are the configured bootstrap hostname, and with `ResolveCanonicalBootstrapServersOnly` they are its canonical name, unless `TlsConfig.TargetHost` overrides the TLS target. A connection that answers `REBOOTSTRAP_REQUIRED` or fails is closed, so the next address gets a new connection. A bootstrap server whose name does not resolve is still tried once by hostname.

## Differences from the Java client

`ResolveCanonicalBootstrapServersOnly` takes the canonical name from the forward DNS lookup of the configured hostname and uses it for every address of that server. Dekaf applies the mode to every connection, including broker connections; the Java client canonicalizes bootstrap servers only, and it does a reverse lookup for each resolved IP address instead (`InetAddress.getCanonicalHostName`), so with DNS records whose reverse and forward names differ, the TLS target or Kerberos service principal can differ from Java's.

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
