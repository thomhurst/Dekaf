---
sidebar_position: 14
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-27 11:20 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
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
  items={[{"label": "Produce — fire-and-forget", "dekaf": 1369358.881, "confluent": 1014593.4283, "dekafDisplay": "1.37M msg/s (1.3×)", "confluentDisplay": "1.01M msg/s"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1029416.6439, "confluent": 652005.7924, "dekafDisplay": "1.03M msg/s (1.6×)", "confluentDisplay": "652.01K msg/s"}, {"label": "Produce — acks=all", "dekaf": 1191979.599, "confluent": 901326.3783, "dekafDisplay": "1.19M msg/s (1.3×)", "confluentDisplay": "901.33K msg/s"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 987485.7832, "confluent": 738148.7781, "dekafDisplay": "987.49K msg/s (1.3×)", "confluentDisplay": "738.15K msg/s"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1245247.3972, "confluent": 971117.1363, "dekafDisplay": "1.25M msg/s (1.3×)", "confluentDisplay": "971.12K msg/s"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 759038.9102, "confluent": 627176.9439, "dekafDisplay": "759.04K msg/s (1.2×)", "confluentDisplay": "627.18K msg/s"}, {"label": "Produce + consume round-trip", "dekaf": 3037258.8468, "confluent": 1254152.8032, "dekafDisplay": "3.04M msg/s (2.4×)", "confluentDisplay": "1.25M msg/s"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 1095.2504, "confluent": 158.8498, "dekafDisplay": "1.10K msg/s (6.9×)", "confluentDisplay": "159 msg/s"}, {"label": "Consume — messages", "dekaf": 1611723.216, "confluent": 1128563.9551, "dekafDisplay": "1.61M msg/s (1.4×)", "confluentDisplay": "1.13M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label": "Produce — fire-and-forget", "dekaf": 0.8183, "confluent": 1.7529, "dekafDisplay": "0.82 μs/msg (2.1× less)", "confluentDisplay": "1.75 μs/msg"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1.2118, "confluent": 2.2924, "dekafDisplay": "1.21 μs/msg (1.9× less)", "confluentDisplay": "2.29 μs/msg"}, {"label": "Produce — acks=all", "dekaf": 0.8886, "confluent": 1.9104, "dekafDisplay": "0.89 μs/msg (2.1× less)", "confluentDisplay": "1.91 μs/msg"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1.1823, "confluent": 2.1243, "dekafDisplay": "1.18 μs/msg (1.8× less)", "confluentDisplay": "2.12 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1.0007, "confluent": 1.7634, "dekafDisplay": "1.00 μs/msg (1.8× less)", "confluentDisplay": "1.76 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1.5719, "confluent": 2.6156, "dekafDisplay": "1.57 μs/msg (1.7× less)", "confluentDisplay": "2.62 μs/msg"}, {"label": "Produce + consume round-trip", "dekaf": 0.9822, "confluent": 2.1719, "dekafDisplay": "0.98 μs/msg (2.2× less)", "confluentDisplay": "2.17 μs/msg"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 253.8781, "confluent": 356.4455, "dekafDisplay": "253.88 μs/msg (1.4× less)", "confluentDisplay": "356.45 μs/msg"}, {"label": "Consume — messages", "dekaf": 0.8276, "confluent": 1.2843, "dekafDisplay": "0.83 μs/msg (1.6× less)", "confluentDisplay": "1.28 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,369,359 msg/s | 1,014,593 msg/s | 1.3× faster | 2.1× less |
| Produce — fire-and-forget (3 brokers) | 1,029,417 msg/s | 652,006 msg/s | 1.6× faster | 1.9× less |
| Produce — acks=all | 1,191,980 msg/s | 901,326 msg/s | 1.3× faster | 2.1× less |
| Produce — acks=all (3 brokers) | 987,486 msg/s | 738,149 msg/s | 1.3× faster | 1.8× less |
| Produce — fire-and-forget, idempotent | 1,245,247 msg/s | 971,117 msg/s | 1.3× faster | 1.8× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 759,039 msg/s | 627,177 msg/s | 1.2× faster | 1.7× less |
| Produce + consume round-trip | 3,037,259 msg/s | 1,254,153 msg/s | 2.4× faster | 2.2× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,095 msg/s | 159 msg/s | 6.9× faster | 1.4× less |
| Consume — messages | 1,611,723 msg/s | 1,128,564 msg/s | 1.4× faster | 1.6× less |
| Consume — batches | 1,654,431 msg/s | — | — | — |
| Consume — raw bytes | 3,020,532 msg/s | — | — | — |
| Consume — raw byte batches | 3,912,585 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,369,359 | 1,292,733–1,450,527 | 0.82 | 1.35x |
| Confluent | 2 | 1,014,593 | 975,031–1,055,761 | 1.75 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.76 | 723.20 | 1,787,945 | 1,787,044 | -5.7% | -0.31% | 1705.12 | 1,787,945 | 0 | 1.35 |
| Dekaf (dekaf-first) | 0.74 | 760.23 | 1,413,907 | 1,450,527 | -6.1% | -0.50% | 1348.41 | 1,413,907 | 0 | 1.04 |
| Dekaf (confluent-first) | 0.90 | 921.54 | 1,271,562 | 1,292,733 | +10.5% | +0.74% | 1212.66 | 1,271,562 | 0 | 1.14 |
| Confluent (confluent-first) | 1.74 | - | 981,375 | 1,055,761 | -26.0% | -2.88% | 935.91 | 981,375 | 0 | 1.71 |
| Confluent (dekaf-first) | 1.77 | - | 961,996 | 975,031 | +3.3% | +0.58% | 917.43 | 961,996 | 0 | 1.70 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.02 | 952.18 | 1,260,341 | 1,220,523 | -20.1% | -1.72% | 1201.95 | 1,260,341 | 0 | 1.28 |
| Dekaf | 1.21 | 1114.31 | 1,040,180 | 1,029,417 | +10.6% | +1.14% | 991.99 | 1,040,180 | 0 | 1.26 |
| Confluent | 2.29 | - | 660,092 | 652,006 | -9.2% | -0.83% | 629.51 | 660,092 | 0 | 1.51 |

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
| Dekaf | 2 | 1,191,980 | 1,144,856–1,241,042 | 0.89 | 1.32x |
| Confluent | 2 | 901,326 | 826,049–983,464 | 1.91 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.80 | 766.05 | 1,230,003 | 1,241,042 | -6.2% | -0.60% | 1173.02 | 1,230,003 | 0 | 0.99 |
| Dekaf (confluent-first) | 0.98 | 1001.95 | 1,140,319 | 1,144,856 | +6.3% | +0.36% | 1087.49 | 1,140,319 | 0 | 1.11 |
| Confluent (dekaf-first) | 1.75 | - | 981,544 | 983,464 | -20.7% | -1.56% | 936.07 | 981,544 | 0 | 1.72 |
| Confluent (confluent-first) | 2.07 | - | 837,255 | 826,049 | +1.9% | +0.16% | 798.47 | 837,255 | 0 | 1.73 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.18 | 1140.16 | 984,732 | 987,486 | -1.4% | -0.09% | 939.11 | 984,732 | 0 | 1.16 |
| Confluent | 2.12 | - | 741,759 | 738,149 | -0.4% | +0.08% | 707.40 | 741,759 | 0 | 1.58 |

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
| Dekaf | 2 | 1,245,247 | 1,184,729–1,308,857 | 1.00 | 1.28x |
| Confluent | 2 | 971,117 | 970,002–972,233 | 1.76 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.84 | 787.33 | 1,645,256 | 1,593,636 | -0.1% | -0.01% | 1569.04 | 1,645,256 | 0 | 1.38 |
| Dekaf (confluent-first) | 0.91 | 912.87 | 1,298,166 | 1,308,857 | +0.5% | -0.04% | 1238.03 | 1,298,166 | 0 | 1.18 |
| Dekaf (dekaf-first) | 1.10 | 1124.15 | 1,174,934 | 1,184,729 | -7.4% | -0.78% | 1120.50 | 1,174,934 | 0 | 1.29 |
| Confluent (dekaf-first) | 1.75 | - | 982,564 | 972,233 | +3.9% | +0.63% | 937.05 | 982,564 | 0 | 1.72 |
| Confluent (confluent-first) | 1.78 | - | 942,558 | 970,002 | +15.6% | +1.37% | 898.89 | 942,558 | 0 | 1.67 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.46 | 1374.93 | 842,558 | 823,261 | -16.8% | -1.52% | 803.53 | 842,558 | 0 | 1.23 |
| Dekaf | 1.57 | 1554.09 | 760,858 | 759,039 | +8.3% | +0.65% | 725.61 | 760,858 | 0 | 1.20 |
| Confluent | 2.62 | - | 624,978 | 627,177 | +10.5% | +0.86% | 596.03 | 624,978 | 0 | 1.63 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.98 | 5274.00 | 1,336,226 | 3,037,259 | +44.2% | +689.79% | 163.11 | 1,336,226 | 0 | 1.31 |
| Confluent | 2.17 | - | 125,299 | 1,254,153 | +24.0% | +148.48% | 15.30 | 125,299 | 0 | 0.27 |

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
| Dekaf | 253.88 | 253.88 | 808 | 1,095 | +11.5% | +1.09% | 0.77 | 1,078 | 0 | 0.27 |
| Confluent | 356.45 | - | 119 | 159 | +3.9% | +0.31% | 0.11 | 158 | 0 | 0.06 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 142,300 | 106,800 | 35,500 | 106,800 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 970,000 | 727,500 | 242,500 | 727,500 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.83 | - | 1,602,152 | 1,611,723 | +8.1% | +0.64% | 1527.93 | - | 0 | 1.33 |
| Confluent | 1.28 | - | 1,089,952 | 1,128,564 | -5.5% | +0.02% | 1039.46 | - | 0 | 1.40 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.80 | - | 1,652,254 | 1,654,431 | +0.1% | +0.01% | 1575.71 | - | 0 | 1.33 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.52 | - | 3,057,821 | 3,020,532 | +7.7% | +0.70% | 2916.17 | - | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.40 | - | 3,886,412 | 3,912,585 | +11.4% | +1.09% | 3706.37 | - | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 19267 | 0 | 0 | 2229.16 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 203753 | 1 | 1 | 1059.96 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 211321 | 8 | 1 | 1039.04 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 144044 | 1 | 1 | 712.95 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 164320 | 1 | 1 | 904.24 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 218254 | 8 | 1 | 1060.16 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 153462 | 0 | 0 | 801.11 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 187967 | 1 | 1 | 1018.01 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 218741 | 48 | 1 | 1061.41 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 137637 | 0 | 0 | 674.99 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 5360 | 4 | 4 | 15.77 GB | 856 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 81 | 1 | 1 | 213.09 MB | 1.53 KB |
| Dekaf | Consumer | 23966 | 15 | 2 | 2718.54 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 24703 | 3 | 2 | 2803.87 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 4 | 2 | 1 | 408.28 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 8 | 2 | 1 | 913.06 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 243 | 3 | 2 | 108.84 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 217 | 3 | 2 | 839.40 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 126 | 3 | 2 | 235.54 MB | 0 B |
| Dekaf | Producer (Acks All) | 171 | 3 | 2 | 130.63 MB | 0 B |
| Dekaf | Producer (Acks All) | 178 | 3 | 2 | 654.24 MB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 129 | 3 | 2 | 144.51 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 177 | 2 | 1 | 115.13 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 176 | 3 | 1 | 598.43 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 100 | 3 | 2 | 198.31 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 1101 | 2 | 1 | 286.91 MB | 15 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 79 | 1 | 1 | 110.58 MB | 120 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 257 | 3 | 2 | 948.77 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 174 | 4 | 2 | 714.18 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 271 | 6 | 2 | 880.94 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 116 | 3 | 1 | 556.14 MB | 1 B |

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
