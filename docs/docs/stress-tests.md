---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-07 00:04 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,738,193 | 1657.67 | 1,738,193 | 0 | 0.76 | 1.33 |
| Confluent | 1,285,392 | 1225.85 | 1,285,392 | 0 | 1.39 | 1.79 |
| Dekaf | 1,127,006 | 1074.80 | 1,127,006 | 0 | 1.12 | 1.26 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.14x faster for producer (fire-and-forget) throughput.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,195,903 | 1140.50 | 1,195,903 | 0 | 1.08 | 1.29 |
| Dekaf (3conn) | 1,032,971 | 985.12 | 1,033,086 | 0 | 1.30 | 1.34 |
| Confluent | 754,266 | 719.32 | 754,266 | 0 | 1.97 | 1.48 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.59x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,183,292 | 1128.48 | 1,183,292 | 0 | 1.09 | 1.29 |
| Confluent | 1,138,917 | 1086.16 | 1,138,917 | 0 | 1.47 | 1.67 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all) performance.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,019,733 | 972.49 | 1,019,733 | 0 | 1.18 | 1.20 |
| Confluent | 668,712 | 637.73 | 668,712 | 0 | 2.34 | 1.57 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.52x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Confluent | 1,470,576 | 1402.45 | 1,470,576 | 0 | 1.21 | 1.78 |
| Dekaf | 1,369,267 | 1305.83 | 1,369,267 | 0 | 0.87 | 1.19 |
| Dekaf (3conn) | 1,320,743 | 1259.56 | 1,320,743 | 0 | 1.04 | 1.38 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.07x faster for producer (fire-and-forget, idempotent) throughput.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,037,095 | 989.05 | 1,037,095 | 0 | 1.11 | 1.15 |
| Dekaf (3conn) | 1,028,489 | 980.84 | 1,028,489 | 0 | 1.22 | 1.26 |
| Confluent | 756,187 | 721.16 | 756,187 | 0 | 2.14 | 1.62 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.37x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,870,273 | 1783.63 | - | 0 | 0.82 | 1.53 |
| Confluent | 779,751 | 743.63 | - | 0 | 1.50 | 1.17 |

:::tip
**Dekaf is 2.40x faster** than Confluent.Kafka for consumer throughput!
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,889,118 | 1801.60 | - | 0 | 0.79 | 1.50 |

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,719,072 | 2593.11 | - | 0 | 0.49 | 1.32 |

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,795,380 | 2665.88 | - | 0 | 0.45 | 1.27 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 334308 | 0 | 0 | 1594.80 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 296546 | 1 | 1 | 1388.27 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 169046 | 0 | 0 | 814.64 GB | 1.26 KB |
| Confluent | producer-acks-all | 261605 | 1 | 1 | 1230.06 GB | 1.26 KB |
| Confluent | producer-acks-all, 3 Brokers | 150639 | 0 | 0 | 722.25 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 336595 | 1 | 1 | 1588.26 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 173263 | 1 | 1 | 816.68 GB | 1.26 KB |
| Dekaf | Consumer | 79669 | 46913 | 2289 | 3723.43 GB | 2.32 KB |
| Dekaf | Consumer (Batch) | 77931 | 48202 | 2335 | 3749.59 GB | 2.31 KB |
| Dekaf | Consumer (Raw Bytes) | 5670 | 4858 | 172 | 172.55 GB | 76 B |
| Dekaf | Consumer (Raw Batch) | 6518 | 5533 | 96 | 177.09 GB | 76 B |
| Dekaf | Producer (Fire-and-Forget) | 295 | 8 | 7 | 833.13 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 283 | 17 | 16 | 1.25 GB | 1 B |
| Dekaf | producer-acks-all | 276 | 8 | 6 | 803.59 MB | 1 B |
| Dekaf | producer-acks-all, 3 Brokers | 238 | 19 | 17 | 2.97 GB | 3 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 494 | 37 | 34 | 9.35 GB | 8 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 245 | 17 | 15 | 2.63 GB | 3 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 557 | 9 | 7 | 1.47 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 266 | 9 | 8 | 1.14 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 460 | 8 | 7 | 1.25 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 481 | 39 | 35 | 3.71 GB | 4 B |

*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Backpressure Parity**: both producers are bounded to the same 512 MB local buffer (Dekaf BufferMemory, librdkafka queue.buffering.max) and block on a full buffer, so neither client can absorb an unbounded backlog into RAM
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
