---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-06 12:48 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Confluent | 942,067 | 898.43 | 942,067 | 0 | 1.76 | 1.66 |
| Dekaf | 765,713 | 730.24 | 765,713 | 0 | 1.18 | 0.90 |
| Dekaf (3conn) | 647,414 | 617.42 | 647,414 | 0 | 1.43 | 0.93 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.23x faster for producer (fire-and-forget) throughput.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 705,450 | 672.77 | 705,450 | 0 | 1.39 | 0.98 |
| Confluent | 691,895 | 659.84 | 691,895 | 0 | 2.19 | 1.52 |
| Dekaf (3conn) | 664,712 | 633.92 | 664,712 | 0 | 1.57 | 1.04 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget), 3 brokers performance.
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Confluent | 1,061,966 | 1012.77 | 1,061,966 | 0 | 1.62 | 1.73 |
| Dekaf | 743,464 | 709.02 | 743,464 | 0 | 1.23 | 0.92 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.43x faster for producer (producer-acks-all) throughput.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 720,043 | 686.69 | 720,043 | 0 | 1.26 | 0.91 |
| Confluent | 616,238 | 587.69 | 616,238 | 0 | 2.55 | 1.57 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.17x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Confluent | 1,240,145 | 1182.69 | 1,240,145 | 0 | 1.46 | 1.82 |
| Dekaf (3conn) | 766,839 | 731.31 | 766,839 | 0 | 1.12 | 0.86 |
| Dekaf | 766,105 | 730.61 | 766,105 | 0 | 1.13 | 0.87 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::note
Confluent.Kafka is 1.62x faster for producer (fire-and-forget, idempotent) throughput.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 716,599 | 683.40 | 716,599 | 0 | 1.32 | 0.95 |
| Dekaf | 672,149 | 641.01 | 672,149 | 0 | 1.55 | 1.04 |
| Confluent | 618,260 | 589.62 | 618,260 | 0 | 2.63 | 1.63 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.09x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,685,558 | 1607.47 | - | 0 | 0.87 | 1.47 |
| Confluent | 1,015,707 | 968.65 | - | 0 | 1.03 | 1.05 |

:::tip
**Dekaf is 1.66x faster** than Confluent.Kafka for consumer throughput!
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,924,351 | 1835.20 | - | 0 | 0.77 | 1.48 |

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,870,980 | 2737.98 | - | 0 | 0.47 | 1.34 |

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,642,294 | 2519.89 | - | 0 | 0.50 | 1.32 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 437462 | 0 | 0 | 2077.39 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 209171 | 1 | 1 | 1017.47 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 157437 | 1 | 1 | 747.25 GB | 1.26 KB |
| Confluent | producer-acks-all | 241800 | 1 | 1 | 1146.91 GB | 1.26 KB |
| Confluent | producer-acks-all, 3 Brokers | 140999 | 1 | 0 | 665.57 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 284405 | 1 | 1 | 1339.38 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 140872 | 0 | 0 | 667.75 GB | 1.26 KB |
| Dekaf | Consumer | 72254 | 42722 | 2029 | 3404.48 GB | 2.35 KB |
| Dekaf | Consumer (Batch) | 83274 | 48457 | 2415 | 3748.37 GB | 2.27 KB |
| Dekaf | Consumer (Raw Bytes) | 6299 | 5487 | 263 | 181.05 GB | 75 B |
| Dekaf | Consumer (Raw Batch) | 6450 | 5476 | 202 | 167.81 GB | 76 B |
| Dekaf | Producer (Fire-and-Forget) | 508 | 379 | 347 | 9.79 GB | 15 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 105 | 26 | 9 | 1.33 GB | 2 B |
| Dekaf | producer-acks-all | 539 | 316 | 243 | 8.41 GB | 14 B |
| Dekaf | producer-acks-all, 3 Brokers | 183 | 44 | 12 | 2.06 GB | 3 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 497 | 266 | 192 | 6.36 GB | 10 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 377 | 14 | 13 | 1.98 GB | 4 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 141 | 31 | 4 | 1.03 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 111 | 25 | 7 | 1.12 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 194 | 45 | 4 | 1.27 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 161 | 41 | 12 | 1.58 GB | 3 B |

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
