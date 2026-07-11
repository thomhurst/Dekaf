---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-11 19:22 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.82 | 1,715,016 | 1,719,807 | +1.5% | +0.24% | 1635.57 | 1,715,016 | 0 | 1.41 |
| Dekaf (3conn) | 0.96 | 1,580,880 | 1,670,369 | -23.6% | -2.23% | 1507.65 | 1,580,880 | 0 | 1.52 |
| Confluent | 1.35 | 1,281,262 | 1,326,052 | -3.9% | -0.40% | 1221.91 | 1,281,262 | 0 | 1.73 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.64x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.30x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 1.26 | 1,199,524 | 1,225,868 | +12.0% | +1.29% | 1143.95 | 1,199,524 | 0 | 1.51 |
| Dekaf (3conn) | 1.28 | 1,173,288 | 1,191,531 | -9.6% | -0.72% | 1118.93 | 1,173,288 | 0 | 1.50 |
| Confluent | 1.94 | 776,065 | 797,381 | -9.9% | -0.96% | 740.11 | 776,065 | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.55x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.54x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.94 | 1,384,210 | 1,368,284 | -1.2% | +0.01% | 1320.09 | 1,384,210 | 0 | 1.31 |
| Confluent | 1.73 | 947,552 | 947,444 | +13.5% | +1.15% | 903.66 | 947,552 | 0 | 1.64 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.83x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.44x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 1.84 | 899,400 | 931,435 | -1.3% | -0.26% | 857.73 | 899,400 | 0 | 1.65 |
| Confluent | 1.98 | 786,336 | 807,330 | -1.0% | -0.10% | 749.91 | 786,336 | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.08x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.15x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.77 | 1,714,830 | 1,719,761 | +1.4% | +0.14% | 1635.39 | 1,714,830 | 0 | 1.32 |
| Dekaf (3conn) | 0.81 | 1,621,440 | 1,619,989 | +6.8% | +0.74% | 1546.33 | 1,621,440 | 0 | 1.31 |
| Confluent | 1.39 | 1,261,381 | 1,266,397 | -7.0% | -0.64% | 1202.95 | 1,261,381 | 0 | 1.75 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.80x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.36x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.19 | 1,078,854 | 1,090,703 | +2.9% | +0.43% | 1028.87 | 1,078,854 | 0 | 1.28 |
| Dekaf | 1.12 | 1,057,946 | 1,069,505 | +1.7% | +0.22% | 1008.94 | 1,057,946 | 0 | 1.19 |
| Confluent | 2.00 | 787,575 | 790,893 | -7.9% | -0.90% | 751.09 | 787,575 | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.78x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.35x.
:::

## Producer → Consumer Round-Trip Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 9.44 | 136,039 | - | - | - | 129.74 | 136,039 | 0 | 1.28 |
| Confluent | 5.74 | 87,456 | - | - | - | 83.40 | 87,456 | 0 | 0.50 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::note
Confluent.Kafka uses 1.65x less CPU per message for producer → consumer round-trip; comparison throughput is 1.56x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 482.15 | 320 | 438 | +12.2% | +1.52% | 0.31 | 427 | 0 | 0.21 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Dekaf | 383,900 | 288,000 | 95,900 | 288,000 | 0 | 0 | 0 | 0 | 0 | PASS |

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.54 | 2,706,588 | 2,661,923 | +4.1% | +0.33% | 2581.20 | - | 0 | 1.45 |
| Confluent | 0.95 | 1,030,812 | 1,034,895 | +3.2% | +0.30% | 983.06 | - | 0 | 0.98 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.77x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 2.57x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.48 | 3,193,742 | 3,205,622 | +3.2% | +0.26% | 3045.79 | - | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.39 | 3,302,238 | 3,310,265 | +8.5% | +0.76% | 3149.26 | - | 0 | 1.28 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.32 | 3,538,733 | 3,614,464 | +10.2% | +1.06% | 3374.80 | - | 0 | 1.15 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 448223 | 303 | 1 | 2118.98 GB | 2.39 KB |
| Confluent | Producer (Fire-and-Forget) | 294803 | 49 | 1 | 1384.12 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 177587 | 120 | 1 | 838.64 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 212775 | 169 | 1 | 1023.51 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 181845 | 83 | 1 | 849.86 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 288927 | 180 | 1 | 1362.47 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 182148 | 127 | 1 | 851.14 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip | 60 | 15 | 0 | 796.48 MB | 3.26 KB |
| Dekaf | Consumer | 731 | 724 | 723 | 538.37 GB | 237 B |
| Dekaf | Consumer (Batch) | 443 | 368 | 366 | 239.30 GB | 89 B |
| Dekaf | Consumer (Raw Bytes) | 59 | 38 | 37 | 9.85 GB | 4 B |
| Dekaf | Consumer (Raw Batch) | 152 | 7 | 6 | 1.30 GB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 553 | 37 | 35 | 5.64 GB | 4 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 478 | 73 | 64 | 3.02 GB | 3 B |
| Dekaf | Producer (Acks All) | 461 | 12 | 10 | 2.23 GB | 2 B |
| Dekaf | Producer (Acks All), 3 Brokers | 132 | 38 | 20 | 1.49 GB | 2 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 655 | 12 | 11 | 2.79 GB | 2 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 180 | 61 | 17 | 2.79 GB | 3 B |
| Dekaf | Producer → Consumer Round-Trip | 101 | 11 | 10 | 785.04 MB | 3.22 KB |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 183 | 1 | 1 | 431.34 MB | 1.15 KB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 608 | 58 | 50 | 8.22 GB | 6 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 402 | 86 | 71 | 3.26 GB | 3 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 647 | 7 | 6 | 2.10 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 421 | 48 | 45 | 3.22 GB | 4 B |

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
