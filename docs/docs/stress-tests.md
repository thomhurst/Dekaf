---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-03 11:01 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 8,950 | 8.54 | - | 2155 | 11.80 | 0.11 |
| Dekaf (3conn) | 8,941 | 8.53 | - | 2145 | 12.04 | 0.11 |
| Confluent | 8,565 | 8.17 | - | 1610 | 2.92 | 0.02 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget) performance.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 11,944 | 11.39 | - | 2212 | 8.50 | 0.10 |
| Dekaf | 10,384 | 9.90 | - | 2012 | 11.93 | 0.12 |
| Confluent | 8,533 | 8.14 | - | 1610 | 3.54 | 0.03 |

:::tip
**Dekaf is 1.22x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 8,929 | 8.52 | - | 2136 | 14.01 | 0.13 |
| Confluent | 8,682 | 8.28 | - | 1612 | 3.05 | 0.03 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all) performance.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 8,937 | 8.52 | - | 2271 | 16.10 | 0.14 |
| Confluent | 8,530 | 8.13 | - | 1611 | 4.22 | 0.04 |

:::note
Dekaf and Confluent.Kafka have similar producer (producer-acks-all), 3 brokers performance.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 8,942 | 8.53 | - | 2146 | 11.68 | 0.10 |
| Dekaf (3conn) | 8,938 | 8.52 | - | 2141 | 11.07 | 0.10 |
| Confluent | 8,534 | 8.14 | - | 1613 | 3.70 | 0.03 |

:::note
Dekaf and Confluent.Kafka have similar producer (fire-and-forget, idempotent) performance.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 11,173 | 10.66 | - | 4342 | 10.55 | 0.12 |
| Dekaf (3conn) | 8,881 | 8.47 | - | 2277 | 12.12 | 0.11 |
| Confluent | 8,641 | 8.24 | - | 1615 | 4.33 | 0.04 |

:::tip
**Dekaf is 1.29x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,457,619 | 1390.09 | - | 0 | 1.03 | 1.51 |
| Confluent | 102,222 | 97.49 | - | 0 | 1.17 | 0.12 |

:::tip
**Dekaf is 14.26x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 41411 | 0 | 0 | 209.08 GB |
| Confluent | Producer (Fire-and-Forget) | 2502 | 4 | 4 | 6.60 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 2481 | 5 | 5 | 6.12 GB |
| Confluent | producer-acks-all | 2574 | 2 | 2 | 7.12 GB |
| Confluent | producer-acks-all, 3 Brokers | 2555 | 6 | 6 | 6.29 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 2594 | 2 | 2 | 7.06 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 2480 | 5 | 5 | 6.16 GB |
| Dekaf | Consumer | 59348 | 35450 | 1280 | 2907.64 GB |
| Dekaf | Producer (Fire-and-Forget) | 595 | 20 | 17 | 5.00 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 429 | 15 | 13 | 4.30 GB |
| Dekaf | producer-acks-all | 471 | 17 | 15 | 4.88 GB |
| Dekaf | producer-acks-all, 3 Brokers | 106 | 101 | 13 | 5.77 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 584 | 40 | 17 | 5.15 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 224 | 20 | 14 | 8.27 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 266 | 48 | 14 | 3.99 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 59 | 55 | 13 | 3.46 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 364 | 75 | 14 | 3.88 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 170 | 161 | 13 | 4.33 GB |

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
