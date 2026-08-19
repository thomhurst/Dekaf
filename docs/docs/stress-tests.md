---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-19 12:40 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## At a glance

Each row is a like-for-like comparison: both clients run the same sustained workload sequentially on the same VM, and repeated samples are aggregated with a geometric mean across both run orders.

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,291,483 msg/s | 937,035 msg/s | 1.4× faster | 2.1× less |
| Produce — fire-and-forget (3 brokers) | 1,362,698 msg/s | 862,686 msg/s | 1.6× faster | 2.0× less |
| Produce — acks=all | 1,566,435 msg/s | 1,362,737 msg/s | 1.1× faster | 1.8× less |
| Produce — acks=all (3 brokers) | 923,962 msg/s | 747,384 msg/s | 1.2× faster | 1.8× less |
| Produce — fire-and-forget, idempotent | 1,426,301 msg/s | 1,026,282 msg/s | 1.4× faster | 2.0× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,108,419 msg/s | 908,084 msg/s | 1.2× faster | 1.9× less |
| Produce + consume round-trip | 2,407,981 msg/s | 1,428,949 msg/s | 1.7× faster | 2.3× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,101 msg/s | 169 msg/s | 6.5× faster | 1.2× less |
| Consume — messages | 1,732,327 msg/s | 1,041,974 msg/s | 1.7× faster | 1.5× less |
| Consume — batches | 1,539,599 msg/s | — | — | — |
| Consume — raw bytes | 3,407,268 msg/s | — | — | — |
| Consume — raw byte batches | 4,066,996 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,291,483 | 1,261,008–1,322,694 | 0.89 | 1.38x |
| Confluent | 2 | 937,035 | 817,865–1,073,568 | 1.87 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.67 | 647.39 | 2,115,113 | 2,163,386 | -25.7% | -1.78% | 2017.13 | 2,115,113 | 0 | 1.41 |
| Dekaf (dekaf-first) | 0.89 | 911.95 | 1,317,033 | 1,322,694 | +3.0% | +0.27% | 1256.02 | 1,317,033 | 0 | 1.17 |
| Dekaf (confluent-first) | 0.89 | 913.53 | 1,270,307 | 1,261,008 | +37.7% | +3.66% | 1211.46 | 1,270,307 | 0 | 1.13 |
| Confluent (dekaf-first) | 1.63 | - | 1,042,684 | 1,073,568 | +29.6% | +2.81% | 994.38 | 1,042,684 | 0 | 1.70 |
| Confluent (confluent-first) | 2.11 | - | 818,107 | 817,865 | +0.5% | +0.14% | 780.21 | 818,107 | 0 | 1.73 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.89 | 860.09 | 1,515,730 | 1,515,633 | +2.0% | +0.16% | 1445.51 | 1,515,730 | 0 | 1.35 |
| Dekaf | 0.89 | 865.52 | 1,353,135 | 1,362,698 | +1.7% | +0.27% | 1290.45 | 1,353,135 | 0 | 1.20 |
| Confluent | 1.78 | - | 850,632 | 862,686 | -1.0% | -0.06% | 811.23 | 850,632 | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,566,435 | 1,544,342–1,588,843 | 0.75 | 1.15x |
| Confluent | 2 | 1,362,737 | 1,344,801–1,380,912 | 1.32 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.75 | 769.52 | 1,571,829 | 1,588,843 | -0.0% | +0.04% | 1499.01 | 1,571,829 | 0 | 1.18 |
| Dekaf (confluent-first) | 0.76 | 776.23 | 1,533,532 | 1,544,342 | +1.6% | +0.15% | 1462.49 | 1,533,532 | 0 | 1.16 |
| Confluent (dekaf-first) | 1.33 | - | 1,312,598 | 1,380,912 | +22.7% | +1.69% | 1251.79 | 1,312,598 | 0 | 1.75 |
| Confluent (confluent-first) | 1.31 | - | 1,327,914 | 1,344,801 | +1.2% | +0.11% | 1266.40 | 1,327,914 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.16 | 1154.97 | 930,818 | 923,962 | +9.7% | +0.87% | 887.70 | 930,818 | 0 | 1.08 |
| Confluent | 2.11 | - | 740,941 | 747,384 | +1.6% | +0.21% | 706.62 | 740,941 | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,426,301 | 1,330,869–1,528,577 | 0.84 | 1.39x |
| Confluent | 2 | 1,026,282 | 1,004,233–1,048,815 | 1.68 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.71 | 707.99 | 2,271,477 | 2,330,728 | -32.0% | -2.80% | 2166.25 | 2,271,477 | 0 | 1.61 |
| Dekaf (dekaf-first) | 0.81 | 823.04 | 1,496,486 | 1,528,577 | +0.8% | +0.06% | 1427.16 | 1,496,486 | 0 | 1.22 |
| Dekaf (confluent-first) | 0.87 | 889.23 | 1,368,304 | 1,330,869 | +38.3% | +3.65% | 1304.92 | 1,368,304 | 0 | 1.18 |
| Confluent (dekaf-first) | 1.61 | - | 1,048,465 | 1,048,815 | +8.3% | +0.93% | 999.89 | 1,048,465 | 0 | 1.69 |
| Confluent (confluent-first) | 1.76 | - | 977,598 | 1,004,233 | -14.9% | -1.42% | 932.31 | 977,598 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.89 | 897.25 | 1,223,346 | 1,228,794 | +1.2% | +0.18% | 1166.67 | 1,223,346 | 0 | 1.09 |
| Dekaf | 0.89 | 892.59 | 1,098,646 | 1,108,419 | +2.6% | +0.27% | 1047.75 | 1,098,646 | 0 | 0.98 |
| Confluent | 1.73 | - | 907,948 | 908,084 | +1.3% | +0.11% | 865.89 | 907,948 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.00 | 3226.23 | 1,241,516 | 2,407,981 | +64.1% | +691.55% | 151.55 | 1,241,516 | 0 | 1.24 |
| Confluent | 2.25 | - | 122,605 | 1,428,949 | -2.7% | +2.49% | 14.97 | 122,605 | 0 | 0.28 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Round-Trip Validation

| Client | Expected | Consumed | Missing | Duplicates | Corrupt | Out of Order | Wrong Partition | Unexpected | Timed Out | Result |
|--------|----------|----------|---------|------------|---------|--------------|-----------------|------------|-----------|--------|
| Confluent | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |
| Dekaf | 19,792,477 | 19,792,477 | 0 | 0 | 0 | 0 | 0 | 0 | no | PASS |

<details>
<summary>Producer (Transactional EOS), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 220.36 | 220.36 | 824 | 1,101 | +3.1% | +0.44% | 0.79 | 1,098 | 0 | 0.24 |
| Confluent | 267.08 | - | 126 | 169 | -1.4% | -0.15% | 0.12 | 168 | 0 | 0.04 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 151,500 | 113,700 | 37,800 | 113,700 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 988,400 | 741,300 | 247,100 | 741,300 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.76 | - | 1,723,234 | 1,732,327 | -1.9% | -0.20% | 1643.40 | - | 0 | 1.31 |
| Confluent | 1.16 | - | 1,001,852 | 1,041,974 | +7.7% | +0.80% | 955.44 | - | 0 | 1.17 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.85 | - | 1,567,280 | 1,539,599 | -23.3% | -2.32% | 1494.67 | - | 0 | 1.32 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.46 | - | 3,373,662 | 3,407,268 | -2.8% | -0.23% | 3217.38 | - | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.36 | - | 4,112,775 | 4,066,996 | +0.2% | +0.02% | 3922.25 | - | 0 | 1.48 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 53434 | 302 | 0 | 2049.19 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 231950 | 19 | 1 | 1126.16 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 159857 | 1 | 1 | 883.57 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 187060 | 1 | 1 | 918.78 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 296387 | 34 | 1 | 1417.72 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 291460 | 1 | 1 | 1434.16 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 166141 | 4 | 1 | 800.35 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 235608 | 2 | 1 | 1132.42 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 211253 | 1 | 1 | 1055.86 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 208982 | 1 | 1 | 980.64 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 3698 | 2 | 0 | 17.56 GB | 952 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 100 | 1 | 1 | 80.70 MB | 559 B |
| Dekaf | Consumer | 76881 | 41 | 2 | 2924.15 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 69895 | 3 | 2 | 2659.62 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 4 | 1 | 1 | 488.91 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 21 | 4 | 2 | 1.00 GB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 211 | 3 | 1 | 712.78 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 220 | 3 | 2 | 118.54 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 169 | 4 | 3 | 752.99 MB | 1 B |
| Dekaf | Producer (Acks All) | 233 | 3 | 2 | 873.70 MB | 1 B |
| Dekaf | Producer (Acks All) | 213 | 3 | 2 | 192.95 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 101 | 3 | 2 | 492.06 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 209 | 2 | 1 | 777.57 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 188 | 4 | 2 | 110.42 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 122 | 3 | 2 | 672.72 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 605 | 3 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 85 | 1 | 0 | 353.63 MB | 375 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 304 | 2 | 1 | 1.11 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 250 | 9 | 2 | 855.31 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 357 | 4 | 2 | 1.11 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 152 | 3 | 2 | 658.41 MB | 1 B |

*Confluent.Kafka uses native librdkafka; .NET GC allocation counters exclude unmanaged allocations.*

</details>

---

## About These Tests

Stress tests measure sustained performance over extended periods against real Kafka brokers, with both clients paired on the same VM for a fair comparison.

<details>
<summary>Methodology — how these numbers are produced</summary>

- **Real Kafka**: Tests run against actual Apache Kafka instances
- **CPU Isolation**: Brokers are pinned to dedicated cores and the client under test to its own cores, so the client — not the broker — is the measured bottleneck
- **RAM-backed Broker Logs**: Kafka log dirs are mounted on tmpfs so disk I/O never caps broker ingestion
- **Delivered Throughput**: producer tables report broker-confirmed throughput, measured as the end-offset delta across all partitions — not the client-side append rate, which can run far ahead of what the broker ever accepts
- **Median Interval Throughput**: table order and comparison ratios use median sampled client-side msg/s when available, which is less sensitive to short late-run stalls than the whole-run mean
- **Same-VM Pairing**: comparable Dekaf and Confluent scenarios run sequentially inside one job/VM; 1-broker producer acceptance lanes run twice in opposite client orders and publish a geometric-mean aggregate, while other lanes alternate order by workflow run number
- **Backpressure Parity**: both producers are bounded to the same 512 MB local buffer (Dekaf BufferMemory, librdkafka queue.buffering.max) and block on a full buffer, so neither client can absorb an unbounded backlog into RAM
- **Consumer Loop Replay**: Consumer tests re-read a pre-seeded topic (seek to beginning when drained) instead of racing a live feeder, so the consumer itself is measured; table headings report the 16KB seed batch size because it amplifies per-batch costs relative to well-batched workloads
- **Delivery Latency Sampling**: 1 in 1000 produced messages is awaited end-to-end to record true broker round-trip latency
- **Round-Trip Correctness**: Bounded sequenced payloads are consumed back and checked for corruption, wrong partitions, gaps, duplicates, and reordering
- **Round-Trip CPU Scope**: CPU time covers both bulk production and consumer validation; it is not a producer-only metric
- **Round-Trip Alloc Scope**: the GC/alloc window likewise spans production plus consume-side validation; values are deliberately consumed as byte[] on both clients for parity, so each consumed payload is materialized as a fresh array (~152 B at 128 B messages) — the expected allocation floor for this lane, not a leak
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario is compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load

</details>
