---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-05 02:11 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## At a glance

Each row is a like-for-like comparison: both clients run the same sustained workload sequentially on the same VM, and repeated samples are aggregated with a geometric mean across both run orders.

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,589,592 msg/s | 1,364,593 msg/s | 1.2× faster | 1.4× less |
| Produce — fire-and-forget (3 brokers) | 1,202,782 msg/s | 896,773 msg/s | 1.3× faster | 1.6× less |
| Produce — acks=all | 1,588,223 msg/s | 1,401,374 msg/s | 1.1× faster | 1.4× less |
| Produce — acks=all (3 brokers) | 852,910 msg/s | 769,005 msg/s | 1.1× faster | 1.3× less |
| Produce — fire-and-forget, idempotent | 1,617,459 msg/s | 1,450,111 msg/s | 1.1× faster | 1.4× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,117,158 msg/s | 858,416 msg/s | 1.3× faster | 1.6× less |
| Produce + consume round-trip | 2,652,540 msg/s | 1,731,781 msg/s | 1.5× faster | 2.0× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,244 msg/s | 174 msg/s | 7.1× faster | 1.1× less |
| Consume — messages | 1,771,526 msg/s | 1,365,730 msg/s | 1.3× faster | 1.4× less |
| Consume — batches | 1,195,583 msg/s | — | — | — |
| Consume — raw bytes | 3,647,900 msg/s | — | — | — |
| Consume — raw byte batches | 4,178,857 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,589,592 | 1,567,598–1,611,895 | 0.94 | 1.16x |
| Confluent | 2 | 1,364,593 | 1,334,107–1,395,777 | 1.34 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.71 | 688.85 | 1,916,810 | 1,971,393 | +15.7% | +1.55% | 1828.01 | 1,916,810 | 0 | 1.36 |
| Dekaf (dekaf-first) | 0.95 | 974.98 | 1,589,098 | 1,611,895 | +1.5% | +0.07% | 1515.48 | 1,589,098 | 0 | 1.51 |
| Dekaf (confluent-first) | 0.92 | 943.22 | 1,541,970 | 1,567,598 | -6.3% | -0.50% | 1470.54 | 1,541,970 | 0 | 1.42 |
| Confluent (dekaf-first) | 1.28 | - | 1,384,814 | 1,395,777 | -0.5% | -0.07% | 1320.66 | 1,384,814 | 0 | 1.77 |
| Confluent (confluent-first) | 1.40 | - | 1,323,202 | 1,334,107 | +1.3% | +0.12% | 1261.90 | 1,323,202 | 0 | 1.85 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.03 | 992.58 | 1,251,395 | 1,250,962 | +2.6% | +0.26% | 1193.42 | 1,251,395 | 0 | 1.29 |
| Dekaf | 1.05 | 1010.85 | 1,198,260 | 1,202,782 | +4.3% | +0.43% | 1142.75 | 1,198,260 | 0 | 1.26 |
| Confluent | 1.68 | - | 895,991 | 896,773 | +0.3% | +0.02% | 854.48 | 895,991 | 0 | 1.51 |

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
| Dekaf | 2 | 1,588,223 | 1,572,835–1,603,762 | 0.94 | 1.13x |
| Confluent | 2 | 1,401,374 | 1,384,156–1,418,807 | 1.27 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.94 | 966.90 | 1,587,185 | 1,603,762 | +1.5% | +0.11% | 1513.66 | 1,587,185 | 0 | 1.49 |
| Dekaf (confluent-first) | 0.93 | 956.65 | 1,560,040 | 1,572,835 | -0.6% | -0.03% | 1487.77 | 1,560,040 | 0 | 1.46 |
| Confluent (confluent-first) | 1.25 | - | 1,413,322 | 1,418,807 | +0.9% | +0.09% | 1347.85 | 1,413,322 | 0 | 1.77 |
| Confluent (dekaf-first) | 1.29 | - | 1,375,551 | 1,384,156 | -2.9% | -0.25% | 1311.83 | 1,375,551 | 0 | 1.78 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.50 | 1499.04 | 849,533 | 852,910 | +5.2% | +0.54% | 810.18 | 849,533 | 0 | 1.28 |
| Confluent | 1.96 | - | 772,783 | 769,005 | +0.3% | +0.07% | 736.98 | 772,783 | 0 | 1.52 |

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
| Dekaf | 2 | 1,617,459 | 1,570,640–1,665,674 | 0.91 | 1.12x |
| Confluent | 2 | 1,450,111 | 1,441,199–1,459,079 | 1.25 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.89 | 916.60 | 1,641,390 | 1,665,674 | +1.4% | +0.11% | 1565.35 | 1,641,390 | 0 | 1.47 |
| Dekaf (3conn) | 0.77 | 711.85 | 1,550,532 | 1,577,926 | +10.6% | +0.90% | 1478.70 | 1,550,532 | 0 | 1.19 |
| Dekaf (confluent-first) | 0.93 | 952.77 | 1,543,502 | 1,570,640 | -6.4% | -0.51% | 1472.00 | 1,543,502 | 0 | 1.44 |
| Confluent (dekaf-first) | 1.25 | - | 1,444,531 | 1,459,079 | +1.4% | +0.06% | 1377.61 | 1,444,531 | 0 | 1.80 |
| Confluent (confluent-first) | 1.26 | - | 1,411,305 | 1,441,199 | +2.0% | -0.13% | 1345.93 | 1,411,305 | 0 | 1.78 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.09 | 1095.57 | 1,199,638 | 1,208,997 | +0.8% | +0.09% | 1144.06 | 1,199,638 | 0 | 1.30 |
| Dekaf | 1.17 | 1181.56 | 1,105,422 | 1,117,158 | +4.2% | +0.43% | 1054.21 | 1,105,422 | 0 | 1.29 |
| Confluent | 1.82 | - | 858,561 | 858,416 | -0.0% | +0.00% | 818.79 | 858,561 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.93 | 5003.84 | 1,399,723 | 2,652,540 | +45.8% | +459.80% | 170.86 | 1,399,723 | 0 | 1.30 |
| Confluent | 1.85 | - | 125,543 | 1,731,781 | +28.5% | +233.66% | 15.33 | 125,543 | 0 | 0.23 |

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
| Dekaf | 205.90 | 205.90 | 925 | 1,244 | +10.3% | +0.89% | 0.88 | 1,234 | 0 | 0.25 |
| Confluent | 231.28 | - | 131 | 174 | +0.2% | +0.03% | 0.12 | 175 | 0 | 0.04 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 157,200 | 117,900 | 39,300 | 117,900 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 1,110,500 | 832,900 | 277,600 | 832,900 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.74 | - | 1,769,310 | 1,771,526 | -1.6% | -0.20% | 1687.35 | - | 0 | 1.31 |
| Confluent | 1.07 | - | 1,359,428 | 1,365,730 | +1.6% | +0.17% | 1296.45 | - | 0 | 1.46 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.15 | - | 1,198,245 | 1,195,583 | -0.9% | -0.14% | 1142.74 | - | 0 | 1.38 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.43 | - | 3,624,160 | 3,647,900 | +1.9% | +0.04% | 3456.27 | - | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.36 | - | 4,227,312 | 4,178,857 | -0.6% | -0.11% | 4031.48 | - | 0 | 1.53 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 24027 | 103 | 0 | 2780.29 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 249305 | 1 | 1 | 1429.12 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 312378 | 1 | 1 | 1495.72 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 206005 | 1 | 1 | 967.84 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 309292 | 24 | 1 | 1485.71 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 307843 | 1 | 1 | 1526.46 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 172292 | 15 | 1 | 834.72 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 310289 | 1 | 1 | 1524.28 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 323966 | 35 | 1 | 1560.23 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 197379 | 1 | 1 | 927.40 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 5445 | 1 | 1 | 12.07 GB | 655 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 104 | 1 | 1 | 129.35 MB | 863 B |
| Dekaf | Consumer | 26426 | 17 | 2 | 3002.24 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 53452 | 3 | 1 | 2033.45 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 3 | 1 | 0 | 462.58 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 9 | 1 | 0 | 1.05 GB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 425 | 2 | 2 | 145.14 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 450 | 1 | 1 | 1.84 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 218 | 3 | 2 | 913.14 MB | 1 B |
| Dekaf | Producer (Acks All) | 437 | 2 | 2 | 1.81 GB | 1 B |
| Dekaf | Producer (Acks All) | 425 | 2 | 2 | 98.31 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 159 | 3 | 2 | 702.40 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 401 | 2 | 2 | 121.07 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 423 | 2 | 2 | 1.61 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 217 | 4 | 2 | 859.59 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 601 | 8 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 194 | 8 | 1 | 486.53 MB | 459 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 332 | 2 | 2 | 1.46 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 229 | 3 | 2 | 987.35 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 303 | 2 | 2 | 1.25 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 241 | 3 | 2 | 995.14 MB | 1 B |

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
