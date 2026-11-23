---
sidebar_position: 14
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-11-23 10:19 UTC

:::info
The paired Dekaf vs Confluent comparison runs on demand (`full_run=true` from main) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## At a glance

Each row is a like-for-like comparison: both clients run the same sustained workload sequentially on the same VM, and repeated samples are aggregated with a geometric mean across both run orders.

<ComparisonChartGrid>

<ComparisonChart
  title="Sustained throughput"
  metric="Paired same-VM stress run"
  description="Broker-confirmed messages per second for the same workload."
  items={[{"label": "Produce — fire-and-forget", "dekaf": 1241051.5272, "confluent": 790943.576, "dekafDisplay": "1.24M msg/s (1.6×)", "confluentDisplay": "790.94K msg/s"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1006099.1693, "confluent": 664663.5833, "dekafDisplay": "1.01M msg/s (1.5×)", "confluentDisplay": "664.66K msg/s"}, {"label": "Produce — acks=all", "dekaf": 1336522.1171, "confluent": 1099623.0182, "dekafDisplay": "1.34M msg/s (1.2×)", "confluentDisplay": "1.10M msg/s"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1153529.7342, "confluent": 905404.4984, "dekafDisplay": "1.15M msg/s (1.3×)", "confluentDisplay": "905.40K msg/s"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1398546.1676, "confluent": 995218.5855, "dekafDisplay": "1.40M msg/s (1.4×)", "confluentDisplay": "995.22K msg/s"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 836526.1463, "confluent": 558635.9689, "dekafDisplay": "836.53K msg/s (1.5×)", "confluentDisplay": "558.64K msg/s"}, {"label": "Produce + consume round-trip", "dekaf": 2670823.6459, "confluent": 1778286.0843, "dekafDisplay": "2.67M msg/s (1.5×)", "confluentDisplay": "1.78M msg/s"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 1141.3821, "confluent": 170.8815, "dekafDisplay": "1.14K msg/s (6.7×)", "confluentDisplay": "171 msg/s"}, {"label": "Consume — messages", "dekaf": 1508712.3979, "confluent": 979922.357, "dekafDisplay": "1.51M msg/s (1.5×)", "confluentDisplay": "979.92K msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label": "Produce — fire-and-forget", "dekaf": 0.8233, "confluent": 2.0977, "dekafDisplay": "0.82 μs/msg (2.5× less)", "confluentDisplay": "2.10 μs/msg"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1.1637, "confluent": 2.4217, "dekafDisplay": "1.16 μs/msg (2.1× less)", "confluentDisplay": "2.42 μs/msg"}, {"label": "Produce — acks=all", "dekaf": 0.673, "confluent": 1.5709, "dekafDisplay": "0.67 μs/msg (2.3× less)", "confluentDisplay": "1.57 μs/msg"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 0.8721, "confluent": 1.7353, "dekafDisplay": "0.87 μs/msg (2.0× less)", "confluentDisplay": "1.74 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 0.7902, "confluent": 1.8584, "dekafDisplay": "0.79 μs/msg (2.4× less)", "confluentDisplay": "1.86 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1.1626, "confluent": 2.9112, "dekafDisplay": "1.16 μs/msg (2.5× less)", "confluentDisplay": "2.91 μs/msg"}, {"label": "Produce + consume round-trip", "dekaf": 0.8765, "confluent": 1.67, "dekafDisplay": "0.88 μs/msg (1.9× less)", "confluentDisplay": "1.67 μs/msg"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 196.3338, "confluent": 223.783, "dekafDisplay": "196.33 μs/msg (1.1× less)", "confluentDisplay": "223.78 μs/msg"}, {"label": "Consume — messages", "dekaf": 0.9093, "confluent": 1.5279, "dekafDisplay": "0.91 μs/msg (1.7× less)", "confluentDisplay": "1.53 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,241,052 msg/s | 790,944 msg/s | 1.6× faster | 2.5× less |
| Produce — fire-and-forget (3 brokers) | 1,006,099 msg/s | 664,664 msg/s | 1.5× faster | 2.1× less |
| Produce — acks=all | 1,336,522 msg/s | 1,099,623 msg/s | 1.2× faster | 2.3× less |
| Produce — acks=all (3 brokers) | 1,153,530 msg/s | 905,404 msg/s | 1.3× faster | 2.0× less |
| Produce — fire-and-forget, idempotent | 1,398,546 msg/s | 995,219 msg/s | 1.4× faster | 2.4× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 836,526 msg/s | 558,636 msg/s | 1.5× faster | 2.5× less |
| Produce + consume round-trip | 2,670,824 msg/s | 1,778,286 msg/s | 1.5× faster | 1.9× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,141 msg/s | 171 msg/s | 6.7× faster | 1.1× less |
| Consume — messages | 1,508,712 msg/s | 979,922 msg/s | 1.5× faster | 1.7× less |
| Consume — batches | 1,580,569 msg/s | — | — | — |
| Consume — raw bytes | 3,231,688 msg/s | — | — | — |
| Consume — raw byte batches | 3,576,536 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,241,052 | 1,216,500–1,266,098 | 0.82 | 1.57x |
| Confluent | 2 | 790,944 | 737,854–847,853 | 2.10 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.83 | 784.07 | 1,644,617 | 1,613,432 | -7.0% | -0.69% | 1568.43 | 1,644,617 | 0 | 1.36 |
| Dekaf (adaptive) | 0.88 | 801.87 | 1,406,539 | 1,390,534 | +4.9% | +0.46% | 1341.38 | 1,406,539 | 0 | 1.24 |
| Dekaf (dekaf-first) | 0.81 | 784.43 | 1,275,423 | 1,266,098 | -4.0% | -0.55% | 1216.34 | 1,275,423 | 0 | 1.03 |
| Dekaf (confluent-first) | 0.84 | 783.92 | 1,202,403 | 1,216,500 | +11.1% | +0.97% | 1146.70 | 1,202,403 | 0 | 1.01 |
| Confluent (confluent-first) | 1.97 | - | 892,209 | 847,853 | -13.1% | -1.07% | 850.88 | 892,209 | 0 | 1.76 |
| Confluent (dekaf-first) | 2.23 | - | 779,728 | 737,854 | +4.7% | -0.04% | 743.61 | 779,728 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.16 | 1052.63 | 1,001,301 | 1,006,099 | +2.6% | +0.24% | 954.92 | 1,001,301 | 0 | 1.17 |
| Dekaf (adaptive) | 1.29 | 1138.48 | 975,039 | 969,624 | +6.4% | +0.74% | 929.87 | 975,039 | 0 | 1.26 |
| Dekaf (3conn) | 1.44 | 1172.45 | 830,934 | 865,590 | +20.5% | +1.36% | 792.44 | 830,934 | 0 | 1.20 |
| Confluent | 2.42 | - | 665,132 | 664,664 | -1.9% | -0.21% | 634.32 | 665,132 | 0 | 1.61 |

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
| Dekaf | 2 | 1,336,522 | 1,287,629–1,387,272 | 0.67 | 1.22x |
| Confluent | 2 | 1,099,623 | 1,074,842–1,124,976 | 1.57 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.68 | 644.83 | 1,371,498 | 1,387,272 | -6.0% | -0.57% | 1307.96 | 1,371,498 | 0 | 0.93 |
| Dekaf (confluent-first) | 0.67 | 631.35 | 1,306,288 | 1,287,629 | -6.4% | -0.53% | 1245.77 | 1,306,288 | 0 | 0.87 |
| Confluent (confluent-first) | 1.53 | - | 1,118,112 | 1,124,976 | -0.3% | -0.03% | 1066.32 | 1,118,112 | 0 | 1.71 |
| Confluent (dekaf-first) | 1.62 | - | 1,055,074 | 1,074,842 | -9.2% | -0.81% | 1006.20 | 1,055,074 | 0 | 1.71 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.87 | 865.08 | 1,132,808 | 1,153,530 | -4.1% | -0.27% | 1080.33 | 1,132,808 | 0 | 0.99 |
| Confluent | 1.74 | - | 898,174 | 905,404 | -0.2% | -0.07% | 856.57 | 898,174 | 0 | 1.56 |

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
| Dekaf | 2 | 1,398,546 | 1,393,608–1,403,502 | 0.79 | 1.41x |
| Confluent | 2 | 995,219 | 966,733–1,024,544 | 1.86 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.77 | 744.14 | 1,855,931 | 1,917,940 | -25.9% | -2.00% | 1769.95 | 1,855,931 | 0 | 1.42 |
| Dekaf (adaptive) | 0.80 | 746.45 | 1,547,428 | 1,538,389 | -11.0% | -0.93% | 1475.74 | 1,547,428 | 0 | 1.24 |
| Dekaf (dekaf-first) | 0.80 | 800.92 | 1,375,413 | 1,403,502 | -3.9% | -0.41% | 1311.70 | 1,375,413 | 0 | 1.10 |
| Dekaf (confluent-first) | 0.78 | 754.86 | 1,387,897 | 1,393,608 | -5.4% | -0.44% | 1323.60 | 1,387,897 | 0 | 1.08 |
| Confluent (dekaf-first) | 1.85 | - | 937,209 | 1,024,544 | -1.8% | -0.13% | 893.79 | 937,209 | 0 | 1.74 |
| Confluent (confluent-first) | 1.86 | - | 942,333 | 966,733 | +6.1% | +0.31% | 898.68 | 942,333 | 0 | 1.76 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.13 | 1122.93 | 992,072 | 1,001,799 | +6.8% | +0.68% | 946.11 | 992,072 | 0 | 1.12 |
| Dekaf (adaptive) | 1.16 | 1123.91 | 950,805 | 951,037 | -14.3% | -1.40% | 906.76 | 950,805 | 0 | 1.10 |
| Dekaf | 1.16 | 1022.25 | 826,197 | 836,526 | -10.0% | -1.25% | 787.92 | 826,197 | 0 | 0.96 |
| Confluent | 2.91 | - | 575,340 | 558,636 | +8.8% | +0.75% | 548.69 | 575,340 | 0 | 1.67 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.88 | 2969.40 | 1,408,970 | 2,670,824 | +90.7% | +865.20% | 171.99 | 1,408,970 | 0 | 1.23 |
| Confluent | 1.67 | - | 126,944 | 1,778,286 | +13.4% | +106.91% | 15.50 | 126,944 | 0 | 0.21 |

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
| Dekaf | 196.33 | 196.33 | 850 | 1,141 | +2.8% | +0.35% | 0.81 | 1,133 | 0 | 0.22 |
| Confluent | 223.78 | - | 127 | 171 | +2.4% | +0.25% | 0.12 | 170 | 0 | 0.04 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 152,900 | 114,700 | 38,200 | 114,700 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 1,019,900 | 765,000 | 254,900 | 765,000 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.91 | - | 1,506,362 | 1,508,712 | +0.6% | +0.04% | 1436.58 | - | 0 | 1.37 |
| Confluent | 1.53 | - | 970,395 | 979,922 | -6.9% | -0.39% | 925.44 | - | 0 | 1.48 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.87 | - | 1,566,083 | 1,580,569 | +0.8% | +0.06% | 1493.53 | - | 0 | 1.36 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.49 | - | 3,230,722 | 3,231,688 | -7.3% | -0.71% | 3081.06 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.42 | - | 3,573,726 | 3,576,536 | +2.1% | +0.00% | 3408.17 | - | 0 | 1.51 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 51713 | 262 | 0 | 1984.74 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 144567 | 0 | 0 | 963.61 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 150334 | 0 | 0 | 842.33 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 130948 | 0 | 0 | 718.44 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 182464 | 0 | 0 | 1207.82 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 184368 | 0 | 0 | 1139.61 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 169582 | 0 | 0 | 970.31 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 174443 | 0 | 0 | 1012.44 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 153905 | 0 | 0 | 1017.94 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 97383 | 0 | 0 | 621.53 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 5388 | 3 | 3 | 17.56 GB | 953 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 101 | 1 | 1 | 341.33 MB | 2.29 KB |
| Dekaf | Consumer | 67141 | 0 | 0 | 2556.00 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 69809 | 0 | 0 | 2657.72 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 11 | 1 | 0 | 444.30 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 7 | 0 | 0 | 872.81 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 186 | 2 | 1 | 725.22 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 192 | 2 | 1 | 766.92 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 213 | 7 | 0 | 636.01 MB | 1 B |
| Dekaf | Producer (Acks All) | 197 | 0 | 0 | 784.99 MB | 1 B |
| Dekaf | Producer (Acks All) | 208 | 0 | 0 | 827.03 MB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 163 | 3 | 2 | 696.68 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 199 | 0 | 0 | 776.34 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 204 | 2 | 1 | 788.35 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 115 | 1 | 1 | 531.86 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 613 | 2 | 0 | 2.81 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 100 | 2 | 1 | 442.81 MB | 455 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 274 | 2 | 1 | 1002.89 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 160 | 2 | 0 | 639.87 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 270 | 2 | 1 | 1.07 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 123 | 3 | 3 | 609.75 MB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget) | 196 | 0 | 0 | 885.08 MB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget), 3 Brokers | 143 | 1 | 1 | 692.78 MB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget, Idempotent) | 226 | 0 | 0 | 967.80 MB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 134 | 0 | 0 | 593.90 MB | 1 B |

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
- **Adaptive-Connections Row**: the four paired fire-and-forget/idempotent producer lanes also run one Dekaf pass with the library default (adaptive connection scaling enabled, one connection to start), labelled `Dekaf (adaptive)`; like the 3-connection control it is excluded from the headline comparison, which stays pinned to one connection to match Confluent
- **Round-Trip Correctness**: Bounded sequenced payloads are consumed back and checked for corruption, wrong partitions, gaps, duplicates, and reordering
- **Round-Trip CPU Scope**: CPU time covers both bulk production and consumer validation; it is not a producer-only metric
- **Round-Trip Alloc Scope**: the GC/alloc window likewise spans production plus consume-side validation; values are deliberately consumed as byte[] on both clients for parity, so each consumed payload is materialized as a fresh array (~152 B at 128 B messages) — the expected allocation floor for this lane, not a leak
- **CPU Efficiency**: CPU time per message differentiates client efficiency even at equal throughput
- **Noise-Aware Trends**: each scenario's throughput, CPU per message and Dekaf delivery-latency p50/p95/p99 are compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow, and paired lanes fail only when the same-run Dekaf/Confluent ratio regressed too
- **Latency Product Bars**: p95 within 3× the configured delivery-latency target and p50/p99 within 2× the same-run Confluent control are reported as product goals, not gates; a lane that misses a bar shows a warning until the trend band moves it
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load

</details>
