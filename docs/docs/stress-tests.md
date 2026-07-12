---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-12 03:52 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.76 | 1,907,887 | 1,904,509 | -2.3% | -0.35% | 1819.50 | 1,907,887 | 0 | 1.45 |
| Confluent | 1.32 | 1,331,118 | 1,341,616 | +2.2% | +0.27% | 1269.45 | 1,331,118 | 0 | 1.75 |
| Dekaf (3conn) | 1.18 | 1,391,575 | 1,331,886 | -25.7% | -2.50% | 1327.11 | 1,391,575 | 0 | 1.64 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.73x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.42x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.43 | 1,083,900 | 1,082,557 | +0.2% | +0.02% | 1033.69 | 1,083,900 | 0 | 1.54 |
| Dekaf | 1.43 | 1,051,202 | 1,038,271 | +3.7% | +0.56% | 1002.50 | 1,051,202 | 0 | 1.50 |
| Confluent | 1.98 | 750,545 | 749,550 | +1.6% | +0.15% | 715.78 | 750,545 | 0 | 1.49 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.39x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.39x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.80 | 1,803,783 | 1,789,279 | +4.9% | +0.23% | 1720.22 | 1,803,783 | 0 | 1.44 |
| Confluent | 1.33 | 1,294,855 | 1,348,337 | -14.1% | -1.36% | 1234.87 | 1,294,855 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.67x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.33x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 1.54 | 1,023,376 | 1,041,089 | -6.2% | -0.49% | 975.97 | 1,023,376 | 0 | 1.57 |
| Confluent | 1.66 | 929,736 | 930,151 | -2.8% | -0.25% | 886.67 | 929,736 | 0 | 1.54 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.08x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.12x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.79 | 1,679,573 | 1,687,367 | +3.4% | +0.32% | 1601.77 | 1,679,573 | 0 | 1.33 |
| Dekaf | 0.92 | 1,449,262 | 1,593,066 | -14.7% | -1.18% | 1382.12 | 1,449,262 | 0 | 1.33 |
| Confluent | 1.41 | 1,278,666 | 1,299,567 | -7.1% | -0.62% | 1219.43 | 1,278,666 | 0 | 1.80 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.53x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.23x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.15 | 1,140,721 | 1,150,760 | +1.7% | +0.17% | 1087.88 | 1,140,721 | 0 | 1.31 |
| Dekaf | 1.17 | 1,063,016 | 1,078,261 | -0.5% | +0.05% | 1013.77 | 1,063,016 | 0 | 1.24 |
| Confluent | 1.83 | 860,277 | 861,949 | -1.2% | -0.09% | 820.42 | 860,277 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.57x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.25x.
:::

## Producer → Consumer Round-Trip Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 8.22 | 161,473 | - | - | - | 153.99 | 161,473 | 0 | 1.33 |
| Confluent | 5.80 | 130,926 | - | - | - | 124.86 | 130,926 | 0 | 0.76 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::note
Confluent.Kafka uses 1.42x less CPU per message for producer → consumer round-trip; comparison throughput is 1.23x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 472.30 | 325 | 438 | +5.2% | +0.70% | 0.31 | 433 | 0 | 0.20 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Dekaf | 389,800 | 292,400 | 97,400 | 292,400 | 0 | 0 | 0 | 0 | 0 | PASS |

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.57 | 2,803,542 | 3,030,272 | -16.1% | -1.44% | 2673.67 | - | 0 | 1.61 |
| Confluent | 1.02 | 976,079 | 974,439 | -1.6% | -0.12% | 930.86 | - | 0 | 0.99 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.77x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 3.11x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.50 | 3,146,681 | 3,162,864 | -1.5% | -0.11% | 3000.91 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.46 | 2,973,346 | 3,045,273 | -11.2% | -0.99% | 2835.60 | - | 0 | 1.37 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.36 | 3,131,131 | 3,101,284 | -0.4% | -0.02% | 2986.08 | - | 0 | 1.11 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 424431 | 286 | 0 | 1996.36 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 307383 | 36 | 1 | 1437.86 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 170584 | 109 | 1 | 811.21 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 298618 | 138 | 1 | 1398.68 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 215637 | 68 | 1 | 1004.32 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 294962 | 110 | 1 | 1381.14 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 199350 | 125 | 1 | 929.71 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip | 63 | 15 | 0 | 797.55 MB | 3.27 KB |
| Dekaf | Consumer | 490 | 484 | 483 | 341.96 GB | 146 B |
| Dekaf | Consumer (Batch) | 818 | 736 | 735 | 513.00 GB | 195 B |
| Dekaf | Consumer (Raw Bytes) | 50 | 42 | 41 | 17.87 GB | 7 B |
| Dekaf | Consumer (Raw Batch) | 105 | 8 | 7 | 1.49 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 679 | 15 | 14 | 3.17 GB | 2 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 392 | 44 | 38 | 2.11 GB | 2 B |
| Dekaf | Producer (Acks All) | 702 | 15 | 14 | 3.06 GB | 2 B |
| Dekaf | Producer (Acks All), 3 Brokers | 130 | 76 | 21 | 2.07 GB | 2 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 639 | 6 | 5 | 1.82 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 146 | 65 | 10 | 1.88 GB | 2 B |
| Dekaf | Producer → Consumer Round-Trip | 92 | 13 | 13 | 789.87 MB | 3.24 KB |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 183 | 1 | 1 | 405.68 MB | 1.07 KB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 247 | 17 | 13 | 2.33 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 339 | 97 | 77 | 3.35 GB | 4 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 433 | 9 | 7 | 2.22 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 429 | 58 | 20 | 2.57 GB | 3 B |

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
- **Round-Trip CPU Scope**: CPU time covers both bulk production and consumer validation; it is not a producer-only metric
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario is compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load
