---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-08 02:00 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,319,529 | 1258.40 | 1,320,600 | 1069 | 0.95 | 1.26 |
| Confluent | 1,067,459 | 1018.01 | 1,067,459 | 0 | 1.62 | 1.73 |
| Dekaf (3conn) | 970,782 | 925.81 | 970,782 | 0 | 1.32 | 1.28 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.24x faster** than Confluent.Kafka for producer (fire-and-forget) throughput!
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,139,011 | 1086.25 | 1,139,079 | 0 | 1.16 | 1.32 |
| Dekaf (3conn) | 989,958 | 944.10 | 990,048 | 0 | 1.26 | 1.25 |
| Confluent | 904,513 | 862.61 | 904,513 | 0 | 1.66 | 1.50 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.26x faster** than Confluent.Kafka for producer (fire-and-forget), 3 brokers throughput!
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,409,182 | 1343.90 | 1,409,184 | 0 | 0.91 | 1.29 |
| Confluent | 1,082,839 | 1032.68 | 1,082,839 | 0 | 1.56 | 1.69 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.30x faster** than Confluent.Kafka for producer (acks all) throughput!
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 915,692 | 873.27 | 915,692 | 0 | 1.40 | 1.28 |
| Confluent | 795,138 | 758.30 | 795,138 | 0 | 1.94 | 1.54 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.15x faster** than Confluent.Kafka for producer (acks all), 3 brokers throughput!
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 1,667,995 | 1590.72 | 1,667,995 | 0 | 0.78 | 1.30 |
| Dekaf | 1,532,930 | 1461.92 | 1,532,930 | 0 | 0.76 | 1.17 |
| Confluent | 1,310,934 | 1250.20 | 1,310,934 | 0 | 1.33 | 1.75 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.17x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent) throughput!
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf (3conn) | 981,969 | 936.48 | 981,969 | 0 | 1.32 | 1.30 |
| Dekaf | 960,271 | 915.79 | 960,271 | 0 | 1.27 | 1.22 |
| Confluent | 861,939 | 822.01 | 861,939 | 0 | 1.83 | 1.58 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf is 1.11x faster** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers throughput!
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,262,271 | 1203.80 | - | 0 | 1.22 | 1.54 |
| Confluent | 1,095,235 | 1044.50 | - | 0 | 1.02 | 1.11 |

:::tip
**Dekaf is 1.15x faster** than Confluent.Kafka for consumer throughput!
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 1,849,266 | 1763.60 | - | 0 | 0.82 | 1.52 |

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,405,812 | 2294.36 | - | 0 | 0.58 | 1.39 |

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages)

| Client | Messages/sec | MB/sec | Accepted msg/s | Errors | CPU μs/msg | Cores Used |
|--------|--------------|--------|----------------|--------|------------|------------|
| Dekaf | 2,576,874 | 2457.50 | - | 0 | 0.51 | 1.32 |

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 474970 | 1 | 1 | 2240.16 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 240595 | 1 | 1 | 1152.90 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 208960 | 0 | 0 | 976.92 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 225454 | 1 | 1 | 1169.49 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 183809 | 0 | 0 | 858.79 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 300189 | 1 | 1 | 1415.86 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 199336 | 1 | 0 | 930.92 GB | 1.26 KB |
| Dekaf | Consumer | 56245 | 32897 | 1556 | 2500.18 GB | 2.31 KB |
| Dekaf | Consumer (Batch) | 76026 | 46949 | 2340 | 3714.18 GB | 2.34 KB |
| Dekaf | Consumer (Raw Bytes) | 5286 | 4487 | 182 | 152.56 GB | 76 B |
| Dekaf | Consumer (Raw Batch) | 5879 | 5067 | 140 | 163.84 GB | 76 B |
| Dekaf | Producer (Fire-and-Forget) | 516 | 137 | 67 | 34.13 GB | 30 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 236 | 15 | 14 | 1.73 GB | 2 B |
| Dekaf | Producer (Acks All) | 527 | 113 | 57 | 26.45 GB | 22 B |
| Dekaf | Producer (Acks All), 3 Brokers | 449 | 336 | 20 | 7.56 GB | 10 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 299 | 19 | 17 | 1.89 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 444 | 24 | 22 | 2.86 GB | 4 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 377 | 6 | 5 | 1003.88 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 383 | 9 | 8 | 1.05 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 542 | 9 | 8 | 1.47 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 412 | 110 | 101 | 8.93 GB | 11 B |

*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Median Interval Throughput**: table order and comparison ratios use median sampled client-side msg/s when available, which is less sensitive to short late-run stalls than the whole-run mean
- **Same-VM Pairing**: comparable Dekaf and Confluent scenarios run sequentially inside one job/VM, with client order alternating by workflow run number to reduce order bias
- **Backpressure Parity**: both producers are bounded to the same 512 MB local buffer (Dekaf BufferMemory, librdkafka queue.buffering.max) and block on a full buffer, so neither client can absorb an unbounded backlog into RAM
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
