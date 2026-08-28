---
sidebar_position: 14
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-28 23:36 UTC

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
  items={[{"label": "Produce — fire-and-forget", "dekaf": 1448277.0992, "confluent": 1179842.7604, "dekafDisplay": "1.45M msg/s (1.2×)", "confluentDisplay": "1.18M msg/s"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1229033.5044, "confluent": 922131.1496, "dekafDisplay": "1.23M msg/s (1.3×)", "confluentDisplay": "922.13K msg/s"}, {"label": "Produce — acks=all", "dekaf": 1569688.9942, "confluent": 1096130.2872, "dekafDisplay": "1.57M msg/s (1.4×)", "confluentDisplay": "1.10M msg/s"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1022915.2315, "confluent": 683031.8881, "dekafDisplay": "1.02M msg/s (1.5×)", "confluentDisplay": "683.03K msg/s"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1402800.4827, "confluent": 1252839.5863, "dekafDisplay": "1.40M msg/s (1.1×)", "confluentDisplay": "1.25M msg/s"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1043316.8799, "confluent": 762823.1446, "dekafDisplay": "1.04M msg/s (1.4×)", "confluentDisplay": "762.82K msg/s"}, {"label": "Produce + consume round-trip", "dekaf": 2584164.8628, "confluent": 1119135.1949, "dekafDisplay": "2.58M msg/s (2.3×)", "confluentDisplay": "1.12M msg/s"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 1304.0977, "confluent": 172.9632, "dekafDisplay": "1.30K msg/s (7.5×)", "confluentDisplay": "173 msg/s"}, {"label": "Consume — messages", "dekaf": 1645613.2587, "confluent": 1159617.3902, "dekafDisplay": "1.65M msg/s (1.4×)", "confluentDisplay": "1.16M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label": "Produce — fire-and-forget", "dekaf": 0.7608, "confluent": 1.4734, "dekafDisplay": "0.76 μs/msg (1.9× less)", "confluentDisplay": "1.47 μs/msg"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1.0033, "confluent": 1.6453, "dekafDisplay": "1.00 μs/msg (1.6× less)", "confluentDisplay": "1.65 μs/msg"}, {"label": "Produce — acks=all", "dekaf": 0.7477, "confluent": 1.5652, "dekafDisplay": "0.75 μs/msg (2.1× less)", "confluentDisplay": "1.57 μs/msg"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1.0778, "confluent": 2.2044, "dekafDisplay": "1.08 μs/msg (2.0× less)", "confluentDisplay": "2.20 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 0.8278, "confluent": 1.4135, "dekafDisplay": "0.83 μs/msg (1.7× less)", "confluentDisplay": "1.41 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1.1203, "confluent": 2.1623, "dekafDisplay": "1.12 μs/msg (1.9× less)", "confluentDisplay": "2.16 μs/msg"}, {"label": "Produce + consume round-trip", "dekaf": 0.9724, "confluent": 2.2881, "dekafDisplay": "0.97 μs/msg (2.4× less)", "confluentDisplay": "2.29 μs/msg"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 219.655, "confluent": 291.2274, "dekafDisplay": "219.65 μs/msg (1.3× less)", "confluentDisplay": "291.23 μs/msg"}, {"label": "Consume — messages", "dekaf": 0.8062, "confluent": 1.1919, "dekafDisplay": "0.81 μs/msg (1.5× less)", "confluentDisplay": "1.19 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,448,277 msg/s | 1,179,843 msg/s | 1.2× faster | 1.9× less |
| Produce — fire-and-forget (3 brokers) | 1,229,034 msg/s | 922,131 msg/s | 1.3× faster | 1.6× less |
| Produce — acks=all | 1,569,689 msg/s | 1,096,130 msg/s | 1.4× faster | 2.1× less |
| Produce — acks=all (3 brokers) | 1,022,915 msg/s | 683,032 msg/s | 1.5× faster | 2.0× less |
| Produce — fire-and-forget, idempotent | 1,402,800 msg/s | 1,252,840 msg/s | 1.1× faster | 1.7× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,043,317 msg/s | 762,823 msg/s | 1.4× faster | 1.9× less |
| Produce + consume round-trip | 2,584,165 msg/s | 1,119,135 msg/s | 2.3× faster | 2.4× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,304 msg/s | 173 msg/s | 7.5× faster | 1.3× less |
| Consume — messages | 1,645,613 msg/s | 1,159,617 msg/s | 1.4× faster | 1.5× less |
| Consume — batches | 1,756,178 msg/s | — | — | — |
| Consume — raw bytes | 3,437,092 msg/s | — | — | — |
| Consume — raw byte batches | 3,998,390 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,448,277 | 1,437,162–1,459,478 | 0.76 | 1.23x |
| Confluent | 2 | 1,179,843 | 1,143,509–1,217,332 | 1.47 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.70 | 691.39 | 2,122,265 | 2,139,058 | +7.9% | +0.76% | 2023.95 | 2,122,265 | 0 | 1.48 |
| Dekaf (confluent-first) | 0.76 | 778.41 | 1,439,743 | 1,459,478 | +3.2% | +0.37% | 1373.05 | 1,439,743 | 0 | 1.09 |
| Dekaf (dekaf-first) | 0.77 | 785.48 | 1,425,352 | 1,437,162 | -1.8% | -0.23% | 1359.32 | 1,425,352 | 0 | 1.09 |
| Confluent (dekaf-first) | 1.43 | - | 1,180,690 | 1,217,332 | +5.7% | +0.45% | 1125.99 | 1,180,690 | 0 | 1.69 |
| Confluent (confluent-first) | 1.52 | - | 1,113,147 | 1,143,509 | -3.5% | -0.26% | 1061.58 | 1,113,147 | 0 | 1.69 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.92 | 866.79 | 1,370,566 | 1,365,628 | -2.9% | -0.25% | 1307.07 | 1,370,566 | 0 | 1.26 |
| Dekaf | 1.00 | 948.24 | 1,223,977 | 1,229,034 | -1.4% | -0.06% | 1167.28 | 1,223,977 | 0 | 1.23 |
| Confluent | 1.65 | - | 925,779 | 922,131 | +0.5% | +0.04% | 882.89 | 925,779 | 0 | 1.52 |

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
| Dekaf | 2 | 1,569,689 | 1,566,979–1,572,404 | 0.75 | 1.43x |
| Confluent | 2 | 1,096,130 | 949,886–1,264,890 | 1.57 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 0.78 | 801.68 | 1,512,919 | 1,572,404 | +10.9% | +1.06% | 1442.83 | 1,512,919 | 0 | 1.18 |
| Dekaf (dekaf-first) | 0.71 | 734.31 | 1,538,049 | 1,566,979 | -5.5% | -0.44% | 1466.80 | 1,538,049 | 0 | 1.10 |
| Confluent (dekaf-first) | 1.41 | - | 1,190,624 | 1,264,890 | -6.3% | -0.57% | 1135.47 | 1,190,624 | 0 | 1.68 |
| Confluent (confluent-first) | 1.72 | - | 987,749 | 949,886 | +19.4% | +2.00% | 941.99 | 987,749 | 0 | 1.70 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.08 | 1079.99 | 1,019,788 | 1,022,915 | +6.8% | +0.66% | 972.55 | 1,019,788 | 0 | 1.10 |
| Confluent | 2.20 | - | 698,471 | 683,032 | +6.8% | +1.02% | 666.11 | 698,471 | 0 | 1.54 |

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
| Dekaf | 2 | 1,402,800 | 1,390,543–1,415,166 | 0.83 | 1.12x |
| Confluent | 2 | 1,252,840 | 1,229,301–1,276,829 | 1.41 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.67 | 657.43 | 2,073,081 | 2,112,915 | -3.2% | -0.50% | 1977.04 | 2,073,081 | 0 | 1.39 |
| Dekaf (confluent-first) | 0.81 | 831.39 | 1,399,233 | 1,415,166 | +4.1% | +0.44% | 1334.41 | 1,399,233 | 0 | 1.14 |
| Dekaf (dekaf-first) | 0.84 | 853.33 | 1,322,293 | 1,390,543 | -3.5% | -0.42% | 1261.04 | 1,322,293 | 0 | 1.12 |
| Confluent (dekaf-first) | 1.40 | - | 1,250,586 | 1,276,829 | -2.5% | -0.27% | 1192.65 | 1,250,586 | 0 | 1.75 |
| Confluent (confluent-first) | 1.43 | - | 1,196,048 | 1,229,301 | +7.6% | +0.50% | 1140.64 | 1,196,048 | 0 | 1.71 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.15 | 1169.59 | 1,148,264 | 1,167,945 | +1.3% | +0.11% | 1095.07 | 1,148,264 | 0 | 1.32 |
| Dekaf | 1.12 | 1111.71 | 1,025,674 | 1,043,317 | -3.9% | -0.47% | 978.16 | 1,025,674 | 0 | 1.15 |
| Confluent | 2.16 | - | 747,275 | 762,823 | -0.9% | +0.06% | 712.66 | 747,275 | 0 | 1.62 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.97 | 2991.82 | 1,326,814 | 2,584,165 | +48.2% | +494.78% | 161.96 | 1,326,814 | 0 | 1.29 |
| Confluent | 2.29 | - | 122,260 | 1,119,135 | +6.9% | +54.50% | 14.92 | 122,260 | 0 | 0.28 |

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
| Dekaf | 219.65 | 219.65 | 984 | 1,304 | +2.1% | +0.22% | 0.94 | 1,312 | 0 | 0.29 |
| Confluent | 291.23 | - | 130 | 173 | +2.5% | +0.27% | 0.12 | 173 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 155,400 | 116,600 | 38,800 | 116,600 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 1,180,800 | 885,600 | 295,200 | 885,600 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.81 | - | 1,626,708 | 1,645,613 | +1.7% | +0.43% | 1551.35 | - | 0 | 1.31 |
| Confluent | 1.19 | - | 1,109,727 | 1,159,617 | +6.4% | +0.81% | 1058.32 | - | 0 | 1.32 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.76 | - | 1,751,977 | 1,756,178 | +3.3% | +0.25% | 1670.82 | - | 0 | 1.33 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.46 | - | 3,414,828 | 3,437,092 | -2.1% | -0.19% | 3256.63 | - | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.38 | - | 4,022,360 | 3,998,390 | +3.1% | +0.33% | 3836.02 | - | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 59162 | 0 | 0 | 2269.61 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 262302 | 6 | 1 | 1275.25 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 229704 | 1 | 1 | 1202.23 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 209943 | 0 | 0 | 999.88 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 271797 | 14 | 1 | 1286.00 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 196532 | 1 | 1 | 1066.82 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 150914 | 2 | 2 | 754.39 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 276877 | 4 | 1 | 1350.76 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 252413 | 1 | 1 | 1291.74 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 164332 | 0 | 0 | 807.07 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 4903 | 2 | 2 | 16.31 GB | 885 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 96 | 2 | 1 | 254.63 MB | 1.68 KB |
| Dekaf | Consumer | 72596 | 147 | 5 | 2760.36 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 26166 | 4 | 2 | 2973.17 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 5 | 2 | 1 | 456.03 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 9 | 2 | 1 | 963.54 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 207 | 2 | 1 | 770.56 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 289 | 3 | 2 | 162.56 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 159 | 4 | 3 | 177.32 MB | 0 B |
| Dekaf | Producer (Acks All) | 259 | 3 | 2 | 1010.50 MB | 1 B |
| Dekaf | Producer (Acks All) | 217 | 3 | 2 | 161.33 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 134 | 3 | 2 | 145.72 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 184 | 2 | 1 | 684.09 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 202 | 4 | 2 | 147.28 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 124 | 4 | 3 | 131.47 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 1097 | 3 | 1 | 0 B | 0 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 93 | 1 | 1 | 183.75 MB | 163 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 272 | 2 | 1 | 1.06 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 195 | 3 | 2 | 809.83 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 296 | 3 | 2 | 1.04 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 147 | 2 | 1 | 712.61 MB | 1 B |

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
