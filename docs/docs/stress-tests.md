---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-11 08:14 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.74 | 1,655,891 | 1,685,304 | 1579.18 | 1,655,891 | 0 | 1.23 |
| Dekaf | 0.79 | 1,506,779 | 1,516,768 | 1436.98 | 1,506,779 | 0 | 1.19 |
| Confluent | 1.42 | 1,196,003 | 1,197,037 | 1140.60 | 1,196,003 | 0 | 1.69 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.80x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.27x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.22 | 1,247,019 | 1,244,793 | 1189.25 | 1,247,019 | 0 | 1.52 |
| Dekaf | 1.26 | 1,237,415 | 1,237,215 | 1180.09 | 1,237,415 | 0 | 1.57 |
| Confluent | 1.68 | 904,114 | 905,688 | 862.23 | 904,114 | 0 | 1.52 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.33x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.37x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.79 | 1,750,158 | 1,748,486 | 1669.08 | 1,750,158 | 0 | 1.39 |
| Confluent | 1.33 | 1,305,265 | 1,322,397 | 1244.80 | 1,305,265 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.68x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.32x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.62 | 933,086 | 958,779 | 889.86 | 933,086 | 0 | 1.52 |
| Confluent | 1.86 | 829,722 | 849,725 | 791.28 | 829,722 | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.15x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.13x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.74 | 1,609,616 | 1,611,553 | 1535.05 | 1,609,616 | 0 | 1.20 |
| Dekaf | 0.89 | 1,380,547 | 1,390,972 | 1316.59 | 1,380,547 | 0 | 1.23 |
| Confluent | 1.37 | 1,246,851 | 1,247,971 | 1189.09 | 1,246,851 | 0 | 1.71 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.53x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.11x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.02 | 1,109,754 | 1,121,113 | 1058.34 | 1,109,754 | 0 | 1.14 |
| Dekaf | 1.03 | 1,102,900 | 1,115,427 | 1051.81 | 1,102,900 | 0 | 1.14 |
| Confluent | 1.77 | 884,844 | 887,860 | 843.85 | 884,844 | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.71x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.26x.
:::

## Producer → Consumer Round-Trip Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 7.42 | 178,453 | - | 170.19 | 178,453 | 0 | 1.32 |
| Confluent | 6.22 | 129,609 | - | 123.60 | 129,609 | 0 | 0.81 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::note
Confluent.Kafka uses 1.19x less CPU per message for producer → consumer round-trip; comparison throughput is 1.38x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 403.67 | 330 | 444 | 0.31 | 440 | 0 | 0.18 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Dekaf | 395,600 | 296,700 | 98,900 | 296,700 | 0 | 0 | 0 | 0 | 0 | PASS |

## Consumer Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Confluent | 0.91 | 1,028,815 | 1,038,668 | 981.15 | - | 0 | 0.94 |
| Dekaf | 1.25 | 1,056,909 | 1,032,776 | 1007.95 | - | 0 | 1.32 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

:::note
Confluent.Kafka uses 1.38x less CPU per message for consumer; comparison throughput is 0.99x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.23 | 1,046,669 | 1,031,682 | 998.18 | - | 0 | 1.28 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.10 | 1,052,394 | 1,022,581 | 1003.64 | - | 0 | 1.15 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.08 | 1,058,032 | 1,038,852 | 1009.02 | - | 0 | 1.14 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 447422 | 304 | 1 | 2104.37 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 274233 | 164 | 1 | 1291.81 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 209629 | 165 | 1 | 976.85 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 301517 | 166 | 1 | 1409.87 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 191805 | 135 | 1 | 896.65 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 287333 | 1 | 1 | 1346.77 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 205053 | 53 | 1 | 956.47 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip | 63 | 14 | 0 | 791.84 MB | 3.24 KB |
| Dekaf | Consumer | 2245 | 2055 | 96 | 65.02 GB | 73 B |
| Dekaf | Consumer (Batch) | 2084 | 1964 | 73 | 66.01 GB | 75 B |
| Dekaf | Consumer (Raw Bytes) | 4782 | 3033 | 74 | 58.02 GB | 66 B |
| Dekaf | Consumer (Raw Batch) | 2420 | 2189 | 51 | 67.03 GB | 76 B |
| Dekaf | Producer (Fire-and-Forget) | 806 | 13 | 11 | 2.58 GB | 2 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 399 | 132 | 109 | 4.52 GB | 4 B |
| Dekaf | Producer (Acks All) | 451 | 12 | 11 | 2.73 GB | 2 B |
| Dekaf | Producer (Acks All), 3 Brokers | 96 | 59 | 12 | 1.34 GB | 2 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 711 | 5 | 4 | 1.78 GB | 2 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 176 | 57 | 12 | 2.08 GB | 2 B |
| Dekaf | Producer → Consumer Round-Trip | 97 | 10 | 10 | 774.40 MB | 3.17 KB |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 191 | 1 | 1 | 448.23 MB | 1.16 KB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 842 | 6 | 5 | 2.24 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 450 | 106 | 92 | 3.79 GB | 4 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 834 | 4 | 3 | 2.07 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 169 | 64 | 11 | 2.13 GB | 2 B |

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
- **Round-Trip Correctness**: Bounded sequenced payloads are consumed back and checked for corruption, wrong partitions, gaps, duplicates, and reordering
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario is compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
