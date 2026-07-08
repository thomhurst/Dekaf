---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-08 18:20 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.84 | 1,425,485 | 1,426,843 | 1359.45 | 1,425,485 | 0 | 1.20 |
| Dekaf | 0.92 | 1,081,658 | 1,051,848 | 1031.55 | 1,081,658 | 0 | 1.00 |
| Confluent | 1.26 | 1,401,498 | 1,422,194 | 1336.57 | 1,401,498 | 0 | 1.76 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.36x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); throughput is 0.77x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.23 | 1,039,733 | 1,035,159 | 991.57 | 1,039,733 | 0 | 1.28 |
| Dekaf (3conn) | 1.53 | 887,123 | 875,680 | 846.03 | 887,123 | 0 | 1.36 |
| Confluent | 2.02 | 754,924 | 742,056 | 719.95 | 754,924 | 0 | 1.52 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.64x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; throughput is 1.38x.
:::

## Producer (producer-acks-all) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.91 | 1,262,141 | 1,258,733 | 1203.67 | 1,262,141 | 0 | 1.14 |
| Confluent | 1.51 | 1,148,896 | 1,177,493 | 1095.67 | 1,148,896 | 0 | 1.73 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.66x less CPU per message** than Confluent.Kafka for producer (producer-acks-all); throughput is 1.10x.
:::

## Producer (producer-acks-all), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.29 | 997,560 | 1,016,289 | 951.35 | 997,560 | 0 | 1.29 |
| Confluent | 1.73 | 892,593 | 883,315 | 851.24 | 892,593 | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.34x less CPU per message** than Confluent.Kafka for producer (producer-acks-all), 3 brokers; throughput is 1.12x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.84 | 1,549,398 | 1,546,870 | 1477.62 | 1,549,398 | 0 | 1.30 |
| Dekaf | 0.88 | 1,123,383 | 1,103,881 | 1071.34 | 1,123,383 | 0 | 0.99 |
| Confluent | 1.45 | 1,194,636 | 1,200,867 | 1139.29 | 1,194,636 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.65x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); throughput is 0.94x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.15 | 1,012,985 | 1,056,343 | 966.06 | 1,012,985 | 0 | 1.16 |
| Dekaf (3conn) | 1.34 | 966,717 | 965,717 | 921.93 | 966,717 | 0 | 1.30 |
| Confluent | 1.81 | 867,314 | 876,560 | 827.13 | 867,314 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.58x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; throughput is 1.17x.
:::

## Consumer Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.66 | 2,442,229 | 2,456,274 | 2329.09 | - | 0 | 1.61 |
| Confluent | 1.01 | 1,071,203 | 1,101,165 | 1021.58 | - | 0 | 1.08 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

:::tip
**Dekaf uses 1.53x less CPU per message** than Confluent.Kafka for consumer; throughput is 2.28x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.64 | 2,493,315 | 2,506,392 | 2377.81 | - | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.60 | 2,348,898 | 2,388,447 | 2240.08 | - | 0 | 1.40 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.50 | 2,607,284 | 2,636,144 | 2486.50 | - | 0 | 1.30 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 465879 | 1 | 1 | 2190.90 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 322347 | 1 | 1 | 1513.67 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 172586 | 0 | 0 | 815.38 GB | 1.26 KB |
| Confluent | producer-acks-all | 262925 | 1 | 1 | 1240.86 GB | 1.26 KB |
| Confluent | producer-acks-all, 3 Brokers | 206560 | 1 | 1 | 964.06 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 274338 | 1 | 1 | 1290.26 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 200827 | 0 | 0 | 936.75 GB | 1.26 KB |
| Dekaf | Consumer | 4175 | 2479 | 434 | 358.06 GB | 175 B |
| Dekaf | Consumer (Batch) | 3966 | 3066 | 318 | 243.16 GB | 116 B |
| Dekaf | Consumer (Raw Bytes) | 5152 | 4376 | 183 | 148.51 GB | 75 B |
| Dekaf | Consumer (Raw Batch) | 6194 | 5205 | 118 | 165.49 GB | 76 B |
| Dekaf | Producer (Fire-and-Forget) | 183 | 23 | 21 | 3.75 GB | 4 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 218 | 10 | 9 | 1.25 GB | 1 B |
| Dekaf | producer-acks-all | 239 | 51 | 47 | 8.61 GB | 8 B |
| Dekaf | producer-acks-all, 3 Brokers | 410 | 24 | 20 | 2.81 GB | 3 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 213 | 29 | 25 | 6.65 GB | 7 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 188 | 13 | 12 | 2.90 GB | 3 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 495 | 7 | 6 | 1.30 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 212 | 21 | 20 | 1.23 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 528 | 9 | 8 | 1.45 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 291 | 23 | 21 | 2.32 GB | 3 B |

*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*

---

## About These Tests

Stress tests measure sustained performance over extended periods:

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Median Interval Throughput**: tables also show median sampled client-side msg/s, which is less sensitive to short late-run stalls than the whole-run mean
- **Backpressure Parity**: both producers are bounded to the same 512 MB local buffer (Dekaf BufferMemory, librdkafka queue.buffering.max) and block on a full buffer, so neither client can absorb an unbounded backlog into RAM
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Parallel Execution**: Each test runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
