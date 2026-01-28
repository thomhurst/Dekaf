---
sidebar_position: 1
slug: /
---

# Introduction

**Dekaf** is a high-performance, pure C# Apache Kafka client for .NET 10+. The name comes from "decaf"—we've taken the Java out of Kafka.

## Why Build Another Kafka Client?

Unlike libraries that wrap librdkafka (a C library), Dekaf is **pure C# from the wire protocol to the API**:

- **No native dependencies**—it runs anywhere .NET runs
- **Zero-allocation hot paths**—we use `Span<T>`, `ref struct`, and object pooling throughout
- **Full debuggability**—when something breaks, you stay in C# land
- **Modern API**—built for how we actually write .NET code in 2024

## What You Get

### Performance Without Compromise

We sweat the details so you don't have to:

- `Span<T>` and `ref struct` keep allocations out of hot paths
- Object pooling for anything we can't avoid allocating
- `ValueTask` where operations might complete synchronously
- Channel-based batching for efficient message handling

### Modern .NET, Not Legacy Patterns

We use the same patterns you're using in your own code:

- Nullable reference types (no more guessing what can be null)
- `IAsyncEnumerable<T>` for streaming consumption
- `ConfigureAwait(false)` everywhere (we're a library, we know the rules)
- Fluent builders that guide you to valid configurations

### Full Kafka Protocol Support

Everything you'd expect from a production Kafka client:

- Producer with acks, batching, and compression
- Consumer groups with automatic rebalancing
- Manual partition assignment when you need control
- Transactions and exactly-once semantics
- Idempotent producers
- All the compression codecs (LZ4, Zstd, Snappy, Gzip)

## Is Dekaf Right for You?

Dekaf makes sense if you:

- Want a pure .NET solution (no native DLLs to wrangle)
- Care about predictable performance and low GC pressure
- Like modern C# APIs (async streams, nullable references, etc.)
- Are on .NET 10 or later

## Getting Help

- **Documentation** - You're reading it!
- **GitHub Issues** - [Report bugs or request features](https://github.com/thomhurst/Dekaf/issues)
- **Source Code** - [Browse the implementation](https://github.com/thomhurst/Dekaf)

Ready to get started? Head to the [Getting Started](./getting-started) guide.
