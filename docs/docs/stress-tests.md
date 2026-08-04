---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-04 17:32 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## At a glance

Each row is a like-for-like comparison: both clients run the same sustained workload sequentially on the same VM, and repeated samples are aggregated with a geometric mean across both run orders.

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 887,387 msg/s | 949,450 msg/s | 1.1× slower | 1.3× less |
| Produce — fire-and-forget (3 brokers) | 1,008,520 msg/s | 738,121 msg/s | 1.4× faster | 1.6× less |
| Produce — acks=all | 1,288,197 msg/s | 1,004,250 msg/s | 1.3× faster | 1.5× less |
| Produce — acks=all (3 brokers) | 893,359 msg/s | 683,207 msg/s | 1.3× faster | 1.6× less |
| Produce — fire-and-forget, idempotent | 1,391,493 msg/s | 1,089,585 msg/s | 1.3× faster | 1.6× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 852,018 msg/s | 611,482 msg/s | 1.4× faster | 1.7× less |
| Produce + consume round-trip | 2,194,741 msg/s | 1,526,241 msg/s | 1.4× faster | 1.8× less |
| Produce — transactional (exactly-once) (3 brokers) | 349 msg/s | 166 msg/s | 2.1× faster | 1.5× more |
| Consume — messages | 1,551,981 msg/s | 1,159,116 msg/s | 1.3× faster | 1.4× less |
| Consume — batches | 1,563,173 msg/s | — | — | — |
| Consume — raw bytes | 3,451,174 msg/s | — | — | — |
| Consume — raw byte batches | 3,966,343 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 887,387 | 826,522–952,735 | 1.47 | 0.93x |
| Confluent | 2 | 949,450 | 826,511–1,090,675 | 1.94 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.87 | 777.55 | 1,332,227 | 1,319,665 | -5.9% | -0.47% | 1270.51 | 1,332,227 | 0 | 1.16 |
| Confluent (dekaf-first) | 1.63 | - | 1,061,543 | 1,090,675 | -15.0% | -1.28% | 1012.37 | 1,061,543 | 0 | 1.73 |
| Dekaf (dekaf-first) | 1.45 | 1396.09 | 936,149 | 952,735 | -12.4% | -1.30% | 892.78 | 936,149 | 0 | 1.36 |
| Dekaf (confluent-first) | 1.49 | 1247.64 | 825,974 | 826,522 | +19.3% | +1.75% | 787.71 | 825,974 | 0 | 1.23 |
| Confluent (confluent-first) | 2.25 | - | 798,682 | 826,511 | -7.5% | -0.91% | 761.68 | 798,682 | 0 | 1.79 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.28 | 1163.85 | 994,847 | 1,008,520 | -13.1% | -1.57% | 948.76 | 994,847 | 0 | 1.28 |
| Dekaf (3conn) | 1.32 | 1200.16 | 977,519 | 970,976 | -19.9% | -1.85% | 932.23 | 977,519 | 0 | 1.29 |
| Confluent | 2.02 | - | 745,950 | 738,121 | +2.2% | -0.05% | 711.39 | 745,950 | 0 | 1.51 |

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
| Dekaf | 2 | 1,288,197 | 1,205,458–1,376,614 | 1.10 | 1.28x |
| Confluent | 2 | 1,004,250 | 935,831–1,077,671 | 1.67 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 1.05 | 1078.06 | 1,369,706 | 1,376,614 | +1.6% | +0.10% | 1306.25 | 1,369,706 | 0 | 1.44 |
| Dekaf (dekaf-first) | 1.16 | 1184.41 | 1,203,548 | 1,205,458 | -13.4% | -1.36% | 1147.79 | 1,203,548 | 0 | 1.39 |
| Confluent (confluent-first) | 1.56 | - | 1,078,469 | 1,077,671 | +7.8% | +0.63% | 1028.51 | 1,078,469 | 0 | 1.68 |
| Confluent (dekaf-first) | 1.77 | - | 934,529 | 935,831 | +1.7% | +0.05% | 891.24 | 934,529 | 0 | 1.66 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.42 | 1426.43 | 900,047 | 893,359 | +3.8% | +0.38% | 858.35 | 900,047 | 0 | 1.27 |
| Confluent | 2.31 | - | 677,693 | 683,207 | +2.9% | +0.30% | 646.30 | 677,693 | 0 | 1.56 |

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
| Dekaf | 2 | 1,391,493 | 1,359,294–1,424,454 | 0.99 | 1.28x |
| Confluent | 2 | 1,089,585 | 1,056,830–1,123,356 | 1.59 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.93 | 937.88 | 1,414,197 | 1,424,454 | -3.2% | -0.43% | 1348.68 | 1,414,197 | 0 | 1.32 |
| Dekaf (3conn) | 0.82 | 759.31 | 1,442,321 | 1,416,254 | -7.4% | -0.61% | 1375.50 | 1,442,321 | 0 | 1.19 |
| Dekaf (confluent-first) | 1.05 | 1068.62 | 1,348,981 | 1,359,294 | +6.3% | +0.53% | 1286.49 | 1,348,981 | 0 | 1.42 |
| Confluent (confluent-first) | 1.57 | - | 1,098,216 | 1,123,356 | -27.6% | -2.55% | 1047.34 | 1,098,216 | 0 | 1.72 |
| Confluent (dekaf-first) | 1.61 | - | 1,055,552 | 1,056,830 | +4.2% | +0.24% | 1006.65 | 1,055,552 | 0 | 1.70 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.50 | 1411.19 | 875,515 | 892,011 | -4.7% | -0.55% | 834.96 | 875,515 | 0 | 1.31 |
| Dekaf | 1.60 | 1571.23 | 823,253 | 852,018 | +16.6% | +1.45% | 785.12 | 823,253 | 0 | 1.32 |
| Confluent | 2.66 | - | 606,363 | 611,482 | -10.4% | -0.97% | 578.27 | 606,363 | 0 | 1.61 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.03 | 3968.69 | 1,245,018 | 2,194,741 | +8.9% | +149.24% | 151.98 | 1,245,018 | 0 | 1.28 |
| Confluent | 1.84 | - | 124,265 | 1,526,241 | +4.2% | +21.87% | 15.17 | 124,265 | 0 | 0.23 |

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
| Dekaf | 400.17 | 400.16 | 260 | 349 | +3.9% | +0.28% | 0.25 | 347 | 0 | 0.14 |
| Confluent | 274.51 | - | 123 | 166 | +7.1% | +0.72% | 0.12 | 164 | 0 | 0.04 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 147,300 | 110,500 | 36,800 | 110,500 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 312,200 | 234,200 | 78,000 | 234,200 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.86 | - | 1,564,065 | 1,551,981 | -15.8% | -1.37% | 1491.61 | - | 0 | 1.34 |
| Confluent | 1.19 | - | 1,095,710 | 1,159,116 | -1.9% | -0.06% | 1044.95 | - | 0 | 1.31 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.86 | - | 1,553,352 | 1,563,173 | +7.2% | +0.74% | 1481.39 | - | 0 | 1.34 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.46 | - | 3,424,139 | 3,451,174 | -4.9% | -0.49% | 3265.51 | - | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.39 | - | 3,920,046 | 3,966,343 | -9.0% | -0.77% | 3738.45 | - | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 19368 | 72 | 1 | 2240.93 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 239417 | 15 | 1 | 1146.63 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 172102 | 1 | 1 | 862.68 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 167972 | 1 | 1 | 805.66 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 201457 | 6 | 1 | 1009.36 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 211821 | 1 | 1 | 1164.75 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 128022 | 0 | 0 | 731.94 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 238274 | 21 | 1 | 1140.10 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 233956 | 1 | 1 | 1186.11 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 136890 | 1 | 1 | 654.90 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 5046 | 1 | 1 | 17.57 GB | 953 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 89 | 2 | 1 | 237.97 MB | 1.65 KB |
| Dekaf | Consumer | 23395 | 40 | 2 | 2653.98 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 69285 | 4 | 1 | 2635.89 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 5 | 2 | 1 | 479.29 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 15 | 3 | 1 | 991.29 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 266 | 2 | 2 | 983.37 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 181 | 2 | 2 | 173.15 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 218 | 6 | 2 | 161.54 MB | 0 B |
| Dekaf | Producer (Acks All) | 339 | 2 | 2 | 1.26 GB | 1 B |
| Dekaf | Producer (Acks All) | 388 | 2 | 2 | 153.82 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 179 | 3 | 2 | 161.64 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 367 | 2 | 2 | 1.33 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 349 | 2 | 2 | 138.55 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 152 | 2 | 2 | 116.60 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 600 | 2 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 35 | 1 | 1 | 89.63 MB | 301 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 292 | 2 | 2 | 1.09 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 218 | 5 | 1 | 785.19 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 299 | 2 | 2 | 1.15 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 170 | 2 | 1 | 794.48 MB | 1 B |

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
