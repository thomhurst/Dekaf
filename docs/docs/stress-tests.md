---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-11 16:56 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.78 | 1,899,584 | 1,942,544 | 1811.58 | 1,899,584 | 0 | 1.49 |
| Dekaf (3conn) | 0.76 | 1,925,012 | 1,927,808 | 1835.83 | 1,925,012 | 0 | 1.45 |
| Confluent | 1.25 | 1,428,823 | 1,456,527 | 1362.63 | 1,428,823 | 0 | 1.78 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.59x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.33x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.28 | 1,127,151 | 1,119,031 | 1074.93 | 1,127,151 | 0 | 1.44 |
| Dekaf (3conn) | 1.27 | 1,114,037 | 1,101,879 | 1062.43 | 1,114,037 | 0 | 1.42 |
| Confluent | 1.79 | 845,887 | 852,420 | 806.70 | 845,887 | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.39x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.31x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Confluent | 1.31 | 1,377,646 | 1,397,279 | 1313.83 | 1,377,646 | 0 | 1.80 |
| Dekaf | 1.15 | 1,441,027 | 1,385,441 | 1374.27 | 1,441,027 | 0 | 1.66 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.14x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 0.99x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 1.68 | 942,973 | 943,191 | 899.29 | 942,973 | 0 | 1.58 |
| Confluent | 1.77 | 867,379 | 863,211 | 827.20 | 867,379 | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.06x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.09x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.79 | 1,647,258 | 1,646,284 | 1570.95 | 1,647,258 | 0 | 1.30 |
| Dekaf (3conn) | 0.79 | 1,635,300 | 1,644,749 | 1559.54 | 1,635,300 | 0 | 1.29 |
| Confluent | 1.64 | 1,025,980 | 1,022,699 | 978.45 | 1,025,980 | 0 | 1.68 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 2.07x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.61x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.09 | 1,100,268 | 1,106,897 | 1049.30 | 1,100,268 | 0 | 1.20 |
| Dekaf | 1.17 | 1,021,315 | 1,046,578 | 974.00 | 1,021,315 | 0 | 1.20 |
| Confluent | 1.90 | 826,309 | 833,769 | 788.03 | 826,309 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.62x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.26x.
:::

## Producer → Consumer Round-Trip Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 5.43 | 248,062 | - | 236.57 | 248,062 | 0 | 1.35 |
| Confluent | 6.96 | 110,018 | - | 104.92 | 110,018 | 0 | 0.77 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.28x less CPU per message** than Confluent.Kafka for producer → consumer round-trip; comparison throughput is 2.25x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 399.97 | 330 | 443 | 0.31 | 440 | 0 | 0.18 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Dekaf | 396,000 | 297,000 | 99,000 | 297,000 | 0 | 0 | 0 | 0 | 0 | PASS |

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.51 | 3,002,995 | 3,119,165 | 2863.88 | - | 0 | 1.54 |
| Confluent | 1.03 | 1,065,666 | 1,124,886 | 1016.30 | - | 0 | 1.09 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

:::tip
**Dekaf uses 2.00x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 2.77x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.48 | 3,194,072 | 3,201,732 | 3046.10 | - | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.38 | 3,286,685 | 3,289,531 | 3134.43 | - | 0 | 1.26 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|--------|----------------|--------|------------|
| Dekaf | 0.34 | 3,254,766 | 3,224,289 | 3103.99 | - | 0 | 1.11 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 463639 | 0 | 0 | 2179.57 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 323690 | 1 | 1 | 1543.13 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 194938 | 1 | 1 | 913.60 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 307077 | 1 | 1 | 1487.94 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 201332 | 1 | 1 | 936.81 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 226380 | 1 | 1 | 1108.06 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 190664 | 0 | 0 | 892.45 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip | 92 | 13 | 4 | 789.73 MB | 3.23 KB |
| Dekaf | Consumer | 619 | 610 | 608 | 431.95 GB | 172 B |
| Dekaf | Consumer (Batch) | 666 | 587 | 586 | 406.66 GB | 152 B |
| Dekaf | Consumer (Raw Bytes) | 35 | 10 | 9 | 1.53 GB | 1 B |
| Dekaf | Consumer (Raw Batch) | 110 | 9 | 7 | 1.30 GB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 882 | 36 | 34 | N/A | N/A |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 428 | 47 | 40 | N/A | N/A |
| Dekaf | Producer (Acks All) | 332 | 25 | 22 | N/A | N/A |
| Dekaf | Producer (Acks All), 3 Brokers | 116 | 65 | 21 | N/A | N/A |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 685 | 11 | 10 | N/A | N/A |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 182 | 59 | 14 | N/A | N/A |
| Dekaf | Producer → Consumer Round-Trip | 94 | 9 | 8 | 767.95 MB | 3.15 KB |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 178 | 1 | 1 | 403.31 MB | 1.04 KB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 850 | 20 | 18 | 3.93 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 413 | 43 | 41 | 2.40 GB | 3 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 771 | 17 | 15 | 3.49 GB | 3 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 189 | 94 | 16 | 2.74 GB | 3 B |

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
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured; table headings report the 16KB seed batch size because it amplifies per-batch costs relative to well-batched workloads
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **Round-Trip Correctness**: Bounded sequenced payloads are consumed back and checked for corruption, wrong partitions, gaps, duplicates, and reordering
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario is compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
