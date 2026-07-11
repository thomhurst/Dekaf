---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-07-11 23:03 UTC

:::info
These tests run weekly (Sunday 2 AM UTC) and can be manually triggered. 
They measure sustained performance over 15+ minutes with real Kafka instances.
:::

## Producer (Fire-and-Forget) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.79 | 1,642,739 | 1,652,620 | +6.2% | +0.63% | 1566.64 | 1,642,739 | 0 | 1.30 |
| Dekaf | 0.87 | 1,527,219 | 1,480,830 | +29.6% | +2.93% | 1456.47 | 1,527,219 | 0 | 1.32 |
| Confluent | 1.42 | 1,156,168 | 1,203,905 | -0.2% | -0.03% | 1102.61 | 1,156,168 | 0 | 1.65 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.64x less CPU per message** than Confluent.Kafka for producer (fire-and-forget); comparison throughput is 1.23x.
:::

## Producer (Fire-and-Forget), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 1.19 | 1,210,993 | 1,210,686 | +4.0% | +0.38% | 1154.89 | 1,210,993 | 0 | 1.44 |
| Dekaf (3conn) | 1.23 | 1,192,391 | 1,187,094 | -0.6% | -0.03% | 1137.15 | 1,192,391 | 0 | 1.46 |
| Confluent | 1.76 | 863,221 | 878,949 | +3.6% | +0.21% | 823.23 | 863,221 | 0 | 1.52 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.48x less CPU per message** than Confluent.Kafka for producer (fire-and-forget), 3 brokers; comparison throughput is 1.38x.
:::

## Producer (Acks All) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.83 | 1,679,890 | 1,656,721 | +18.8% | +1.87% | 1602.07 | 1,679,890 | 0 | 1.40 |
| Confluent | 1.56 | 1,131,053 | 1,190,819 | -2.4% | -0.14% | 1078.66 | 1,131,053 | 0 | 1.76 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.87x less CPU per message** than Confluent.Kafka for producer (acks all); comparison throughput is 1.39x.
:::

## Producer (Acks All), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 1.45 | 1,159,600 | 1,157,732 | -6.1% | -0.63% | 1105.88 | 1,159,600 | 0 | 1.68 |
| Confluent | 1.82 | 838,048 | 842,165 | -4.5% | -0.30% | 799.23 | 838,048 | 0 | 1.52 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.26x less CPU per message** than Confluent.Kafka for producer (acks all), 3 brokers; comparison throughput is 1.37x.
:::

## Producer (Fire-and-Forget, Idempotent) Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 0.85 | 1,500,014 | 1,501,971 | +16.3% | +1.66% | 1430.53 | 1,500,014 | 0 | 1.27 |
| Dekaf | 0.89 | 1,466,221 | 1,467,076 | -4.1% | -0.32% | 1398.30 | 1,466,221 | 0 | 1.30 |
| Confluent | 1.60 | 1,033,762 | 1,049,864 | +7.3% | +0.87% | 985.87 | 1,033,762 | 0 | 1.65 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.80x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent); comparison throughput is 1.40x.
:::

## Producer (Fire-and-Forget, Idempotent), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf (3conn) | 1.21 | 1,063,961 | 1,070,912 | +13.8% | +1.18% | 1014.67 | 1,063,961 | 0 | 1.29 |
| Dekaf | 1.19 | 1,030,964 | 1,033,992 | -3.9% | -0.36% | 983.20 | 1,030,964 | 0 | 1.23 |
| Confluent | 2.18 | 736,010 | 731,315 | -0.0% | +0.16% | 701.91 | 736,010 | 0 | 1.60 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

:::tip
**Dekaf uses 1.82x less CPU per message** than Confluent.Kafka for producer (fire-and-forget, idempotent), 3 brokers; comparison throughput is 1.41x.
:::

## Producer → Consumer Round-Trip Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 6.25 | 232,266 | - | - | - | 221.51 | 232,266 | 0 | 1.45 |
| Confluent | 6.72 | 78,637 | - | - | - | 74.99 | 78,637 | 0 | 0.53 |

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 250,000 | 250,000 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

:::tip
**Dekaf uses 1.07x less CPU per message** than Confluent.Kafka for producer → consumer round-trip; comparison throughput is 2.95x.
:::

## Producer (Transactional EOS), 3 Brokers Throughput (15 minutes, 1000B messages)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 456.70 | 314 | 426 | -3.2% | +0.14% | 0.30 | 419 | 0 | 0.19 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Dekaf | 377,100 | 282,900 | 94,200 | 282,900 | 0 | 0 | 0 | 0 | 0 | PASS |

## Consumer Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.53 | 2,880,161 | 2,940,328 | +1.2% | +0.15% | 2746.74 | - | 0 | 1.53 |
| Confluent | 0.97 | 1,126,284 | 1,199,957 | +0.1% | +0.18% | 1074.11 | - | 0 | 1.09 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

:::tip
**Dekaf uses 1.82x less CPU per message** than Confluent.Kafka for consumer; comparison throughput is 2.45x.
:::

## Consumer (Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.51 | 3,113,352 | 3,152,192 | +0.7% | +0.04% | 2969.12 | - | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Bytes) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.45 | 3,020,418 | 3,006,919 | -1.3% | -0.15% | 2880.50 | - | 0 | 1.35 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Consumer (Raw Batch) Throughput (15 minutes, 1000B messages, 16,384B seed batches)

| Client | CPU μs/msg | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Cores Used |
|--------|------------|--------------|--------------|-------|-------------|--------|----------------|--------|------------|
| Dekaf | 0.35 | 3,363,180 | 3,321,841 | -1.1% | -0.18% | 3207.38 | - | 0 | 1.19 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

## Memory & GC Statistics

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 489194 | 1 | 1 | 2303.55 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 253731 | 1 | 1 | 1248.66 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 198687 | 0 | 0 | 932.33 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 259136 | 1 | 1 | 1221.59 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 193929 | 0 | 0 | 905.13 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 225026 | 1 | 1 | 1116.52 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 165016 | 0 | 0 | 794.93 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip | 140 | 3 | 2 | 788.41 MB | 3.23 KB |
| Dekaf | Consumer | 690 | 681 | 679 | 516.13 GB | 214 B |
| Dekaf | Consumer (Batch) | 767 | 707 | 705 | 503.23 GB | 193 B |
| Dekaf | Consumer (Raw Bytes) | 24 | 13 | 11 | 3.54 GB | 1 B |
| Dekaf | Consumer (Raw Batch) | 118 | 10 | 9 | 1.39 GB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 504 | 16 | 15 | N/A | N/A |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 466 | 44 | 39 | N/A | N/A |
| Dekaf | Producer (Acks All) | 540 | 10 | 9 | N/A | N/A |
| Dekaf | Producer (Acks All), 3 Brokers | 299 | 97 | 70 | N/A | N/A |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 632 | 7 | 6 | N/A | N/A |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 199 | 60 | 27 | N/A | N/A |
| Dekaf | Producer → Consumer Round-Trip | 89 | 11 | 10 | 757.00 MB | 3.10 KB |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 177 | 1 | 1 | 416.24 MB | 1.13 KB |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 510 | 18 | 16 | 3.36 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 432 | 75 | 67 | 3.13 GB | 3 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 581 | 10 | 8 | 2.07 GB | 2 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 511 | 48 | 42 | 3.41 GB | 4 B |

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
