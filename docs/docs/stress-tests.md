---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-07 01:36 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,734,819 | 1654.45 | 1,734,819 | 0 | 0.77 | 1.34 |
| Confluent | 1,410,455 | 1345.11 | 1,410,455 | 0 | 1.25 | 1.76 |
| Dekaf | 1,287,504 | 1227.86 | 1,287,504 | 0 | 0.86 | 1.10 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.10x faster for producer (fire-and-forget) throughput.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,218,582 | 1162.13 | 1,218,731 | 0 | 1.08 | 1.31 |
| Dekaf | 1,195,997 | 1140.59 | 1,196,031 | 0 | 1.10 | 1.31 |
| Confluent | 943,218 | 899.52 | 943,218 | 0 | 1.59 | 1.50 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.27x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,547,147 | 1475.47 | 1,547,147 | 0 | 0.83 | 1.28 |
| Confluent | 1,395,130 | 1330.50 | 1,395,130 | 0 | 1.25 | 1.74 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.11x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,033,683 | 985.80 | 1,033,683 | 0 | 1.06 | 1.10 |
| Confluent | 903,411 | 861.56 | 903,411 | 0 | 1.69 | 1.53 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.14x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,807,545 | 1723.81 | 1,807,545 | 0 | 0.73 | 1.33 |
| Confluent | 1,506,071 | 1436.30 | 1,506,071 | 0 | 1.18 | 1.77 |
| Dekaf | 1,313,158 | 1252.33 | 1,313,158 | 0 | 0.85 | 1.12 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.15x faster for producer (fire-and-forget, idempotent) throughput.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,166,429 | 1112.39 | 1,166,429 | 0 | 1.09 | 1.27 |
| Dekaf | 998,192 | 951.95 | 998,192 | 0 | 1.22 | 1.21 |
| Confluent | 883,102 | 842.19 | 883,102 | 0 | 1.77 | 1.56 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.13x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,897,297 | 1809.40 | - | 0 | 0.80 | 1.52 |
| Confluent | 1,144,916 | 1091.88 | - | 0 | 0.98 | 1.12 |

:::tip
**Dekaf is 1.66x faster** than Confluent.Kafka for consumer throughput!
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,890,497 | 1802.92 | - | 0 | 0.80 | 1.51 |

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,735,564 | 2608.84 | - | 0 | 0.49 | 1.34 |

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,126,673 | 2028.15 | - | 0 | 0.66 | 1.41 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 492943 | 0 | 0 | 2341.66 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 324197 | 1 | 1 | 1523.35 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 218448 | 1 | 1 | 1018.72 GB | 1.26 KB |
| Confluent | producer-acks-all | 323685 | 1 | 1 | 1506.80 GB | 1.26 KB |
| Confluent | producer-acks-all, 3 Brokers | 208721 | 1 | 1 | 975.72 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 345600 | 1 | 1 | 1626.59 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 204506 | 1 | 1 | 953.78 GB | 1.26 KB |
| Dekaf | Consumer | 81652 | 48246 | 2470 | 3748.95 GB | 2.30 KB |
| Dekaf | Consumer (Batch) | 80843 | 47630 | 2490 | 3707.13 GB | 2.28 KB |
| Dekaf | Consumer (Raw Bytes) | 6018 | 5148 | 229 | 174.17 GB | 76 B |
| Dekaf | Consumer (Raw Batch) | 4883 | 4121 | 138 | 135.89 GB | 76 B |
| Dekaf | Producer (Fire-and-Forget) | 407 | 90 | 21 | 6.68 GB | 6 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 338 | 16 | 15 | 1.27 GB | 1 B |
| Dekaf | producer-acks-all | 319 | 19 | 17 | 1.91 GB | 1 B |
| Dekaf | producer-acks-all, 3 Brokers | 184 | 13 | 11 | 2.21 GB | 3 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 449 | 34 | 30 | 8.25 GB | 7 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 322 | 25 | 22 | 2.93 GB | 4 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 391 | 18 | 16 | 2.70 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 288 | 20 | 19 | 1.97 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 383 | 7 | 7 | 1.62 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 406 | 83 | 77 | 5.17 GB | 5 B |

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
