---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-06 15:57 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,474,519 | 1406.21 | 1,474,519 | 0 | 0.91 | 1.33 |
| Dekaf | 1,186,090 | 1131.14 | 1,186,090 | 0 | 1.04 | 1.24 |
| Confluent | 1,106,703 | 1055.43 | 1,106,703 | 0 | 1.51 | 1.68 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.07x faster** than Confluent.Kafka for producer (fire-and-forget) throughput!
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 932,885 | 889.67 | 932,885 | 0 | 1.40 | 1.30 |
| Dekaf (3conn) | 809,338 | 771.84 | 809,345 | 0 | 1.64 | 1.33 |
| Confluent | 693,856 | 661.71 | 693,856 | 0 | 2.20 | 1.52 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.34x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,229,803 | 1172.83 | 1,229,803 | 0 | 1.01 | 1.24 |
| Confluent | 1,127,070 | 1074.86 | 1,127,070 | 0 | 1.56 | 1.76 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.09x faster** than Confluent.Kafka for producer (producer-acks-all) throughput!
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 965,303 | 920.58 | 965,303 | 0 | 1.21 | 1.17 |
| Confluent | 631,440 | 602.19 | 631,440 | 0 | 2.48 | 1.57 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.53x faster** than Confluent.Kafka for producer (producer-acks-all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,635,944 | 1560.16 | 1,635,944 | 0 | 0.71 | 1.16 |
| Dekaf | 1,302,478 | 1242.14 | 1,302,478 | 0 | 0.96 | 1.25 |
| Confluent | 1,051,726 | 1003.00 | 1,051,726 | 0 | 1.59 | 1.67 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.24x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent) throughput!
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,112,546 | 1061.01 | 1,112,546 | 0 | 1.14 | 1.27 |
| Dekaf | 912,711 | 870.43 | 912,711 | 0 | 1.33 | 1.21 |
| Confluent | 658,197 | 627.71 | 658,197 | 0 | 2.46 | 1.62 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.39x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,599,366 | 1525.27 | - | 0 | 0.94 | 1.51 |
| Confluent | 915,059 | 872.67 | - | 0 | 1.26 | 1.16 |

:::tip
**Dekaf is 1.75x faster** than Confluent.Kafka for consumer throughput!
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,629,795 | 1554.29 | - | 0 | 0.94 | 1.54 |

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,777,786 | 2649.10 | - | 0 | 0.48 | 1.34 |

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,688,897 | 2564.33 | - | 0 | 0.49 | 1.32 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 397513 | 0 | 0 | 1871.55 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 248666 | 1 | 1 | 1195.30 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 158790 | 0 | 0 | 749.40 GB | 1.26 KB |
| Confluent | producer-acks-all | 247671 | 1 | 1 | 1217.27 GB | 1.26 KB |
| Confluent | producer-acks-all, 3 Brokers | 142821 | 1 | 1 | 681.98 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 235817 | 1 | 1 | 1135.89 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 150984 | 2 | 2 | 710.88 GB | 1.26 KB |
| Dekaf | Consumer | 68483 | 41296 | 2030 | 3219.12 GB | 2.34 KB |
| Dekaf | Consumer (Batch) | 67805 | 41988 | 1988 | 3261.21 GB | 2.33 KB |
| Dekaf | Consumer (Raw Bytes) | 6039 | 5263 | 246 | 176.23 GB | 76 B |
| Dekaf | Consumer (Raw Batch) | 6373 | 5444 | 185 | 170.31 GB | 76 B |
| Dekaf | Producer (Fire-and-Forget) | 345 | 50 | 45 | 9.89 GB | 10 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 253 | 11 | 10 | 1010.04 MB | 1 B |
| Dekaf | producer-acks-all | 262 | 32 | 28 | 4.70 GB | 5 B |
| Dekaf | producer-acks-all, 3 Brokers | 206 | 21 | 19 | 3.01 GB | 4 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 545 | 76 | 66 | 17.67 GB | 16 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 230 | 20 | 19 | 2.71 GB | 4 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 447 | 8 | 7 | 1.36 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 292 | 7 | 6 | 1.03 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 598 | 7 | 6 | 1.54 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 504 | 79 | 71 | 6.14 GB | 7 B |

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
