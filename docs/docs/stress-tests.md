---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-03 09:13 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|-------|--------|------------|------------|
| Dekaf (3conn) | 15,966 | 15.23 | 15,095,078 | 8780 | 6.32 | 0.10 |
| Dekaf | 15,937 | 15.20 | 15,088,746 | 8784 | 7.19 | 0.11 |
| Confluent | 7,191 | 6.86 | 6,625,482 | 301 | 3.41 | 0.02 |

:::tip
**Dekaf is 2.22x faster** than Confluent.Kafka for producer (fire-and-forget) throughput!
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|-------|--------|------------|------------|
| Dekaf (3conn) | 18,002 | 17.17 | 17,113,653 | 8051 | 7.77 | 0.14 |
| Dekaf | 17,342 | 16.54 | 16,506,153 | 8082 | 8.92 | 0.15 |
| Confluent | 7,172 | 6.84 | 6,670,268 | 227 | 5.39 | 0.04 |

:::tip
**Dekaf is 2.42x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|-------|--------|------------|------------|
| Dekaf | 15,956 | 15.22 | 15,103,057 | 8760 | 7.48 | 0.12 |
| Confluent | 7,193 | 6.86 | 6,625,953 | 299 | 3.68 | 0.03 |

:::tip
**Dekaf is 2.22x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|-------|--------|------------|------------|
| Dekaf | 15,874 | 15.14 | 15,086,619 | 8829 | 9.12 | 0.14 |
| Confluent | 7,122 | 6.79 | 6,623,257 | 233 | 5.31 | 0.04 |

:::tip
**Dekaf is 2.23x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|-------|--------|------------|------------|
| Dekaf (3conn) | 15,945 | 15.21 | 15,097,342 | 8790 | 8.27 | 0.13 |
| Dekaf | 15,930 | 15.19 | 15,078,402 | 8771 | 7.30 | 0.12 |
| Confluent | 7,192 | 6.86 | 6,625,780 | 300 | 3.87 | 0.03 |

:::tip
**Dekaf is 2.21x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent) throughput!
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|-------|--------|------------|------------|
| Dekaf (3conn) | 15,841 | 15.11 | 15,071,892 | 8956 | 8.38 | 0.13 |
| Confluent | 7,249 | 6.91 | 6,622,326 | 299 | 13.61 | 0.10 |

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Total | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|-------|--------|------------|------------|
| Dekaf | 1,706,022 | 1626.99 | 1,535,486,814 | 0 | 0.88 | 1.51 |
| Confluent | 121,837 | 116.19 | 109,662,756 | 0 | 1.07 | 0.13 |

:::tip
**Dekaf is 14.00x faster** than Confluent.Kafka for consumer throughput!
:::

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated |
|--------|----------|------|------|------|-----------------|
| Confluent | Consumer | 52194 | 1 | 1 | 249.44 GB |
| Confluent | Producer (Fire-and-Forget) | 2417 | 2 | 2 | 6.00 GB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 2283 | 3 | 3 | 5.36 GB |
| Confluent | producer-acks-all | 2512 | 2 | 2 | 6.05 GB |
| Confluent | producer-acks-all, 3 Brokers | 2269 | 3 | 3 | 5.40 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 2208 | 2 | 2 | 5.54 GB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 2263 | 6 | 5 | 5.32 GB |
| Dekaf | Consumer | 69571 | 41111 | 1437 | 3386.74 GB |
| Dekaf | Producer (Fire-and-Forget) | 173 | 25 | 23 | 18.46 GB |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 92 | 83 | 17 | 19.03 GB |
| Dekaf | producer-acks-all | 231 | 28 | 24 | 18.23 GB |
| Dekaf | producer-acks-all, 3 Brokers | 468 | 22 | 18 | 17.29 GB |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 97 | 24 | 20 | 17.80 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 142 | 22 | 18 | 13.14 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 451 | 96 | 19 | 15.57 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 208 | 22 | 19 | 16.69 GB |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 227 | 20 | 17 | 17.95 GB |

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Backpressure Parity**: Confluent producers block and retry on a full local queue, mirroring Dekaf's BufferMemory backpressure, so throughput is delivered goodput for both clients
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
