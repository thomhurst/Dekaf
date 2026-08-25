---
sidebar_position: 14
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-25 20:53 UTC

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
  items={[{"label": "Produce — fire-and-forget", "dekaf": 1464245.7203, "confluent": 1177881.0582, "dekafDisplay": "1.46M msg/s (1.2×)", "confluentDisplay": "1.18M msg/s"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1284310.5522, "confluent": 756055.939, "dekafDisplay": "1.28M msg/s (1.7×)", "confluentDisplay": "756.06K msg/s"}, {"label": "Produce — acks=all", "dekaf": 1473095.0917, "confluent": 1360536.9499, "dekafDisplay": "1.47M msg/s (1.1×)", "confluentDisplay": "1.36M msg/s"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1090347.1779, "confluent": 775189.2165, "dekafDisplay": "1.09M msg/s (1.4×)", "confluentDisplay": "775.19K msg/s"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1424790.5554, "confluent": 1083287.6283, "dekafDisplay": "1.42M msg/s (1.3×)", "confluentDisplay": "1.08M msg/s"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1133938.8872, "confluent": 866678.3304, "dekafDisplay": "1.13M msg/s (1.3×)", "confluentDisplay": "866.68K msg/s"}, {"label": "Produce + consume round-trip", "dekaf": 2625779.0638, "confluent": 1763441.8979, "dekafDisplay": "2.63M msg/s (1.5×)", "confluentDisplay": "1.76M msg/s"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 1272.304, "confluent": 171.9716, "dekafDisplay": "1.27K msg/s (7.4×)", "confluentDisplay": "172 msg/s"}, {"label": "Consume — messages", "dekaf": 1644969.9893, "confluent": 1173883.2875, "dekafDisplay": "1.64M msg/s (1.4×)", "confluentDisplay": "1.17M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label": "Produce — fire-and-forget", "dekaf": 0.7191, "confluent": 1.4281, "dekafDisplay": "0.72 μs/msg (2.0× less)", "confluentDisplay": "1.43 μs/msg"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 0.9574, "confluent": 2.074, "dekafDisplay": "0.96 μs/msg (2.2× less)", "confluentDisplay": "2.07 μs/msg"}, {"label": "Produce — acks=all", "dekaf": 0.7217, "confluent": 1.3278, "dekafDisplay": "0.72 μs/msg (1.8× less)", "confluentDisplay": "1.33 μs/msg"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1.0364, "confluent": 1.9906, "dekafDisplay": "1.04 μs/msg (1.9× less)", "confluentDisplay": "1.99 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 0.797, "confluent": 1.604, "dekafDisplay": "0.80 μs/msg (2.0× less)", "confluentDisplay": "1.60 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 0.9012, "confluent": 1.8281, "dekafDisplay": "0.90 μs/msg (2.0× less)", "confluentDisplay": "1.83 μs/msg"}, {"label": "Produce + consume round-trip", "dekaf": 0.9042, "confluent": 1.7593, "dekafDisplay": "0.90 μs/msg (1.9× less)", "confluentDisplay": "1.76 μs/msg"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 194.5434, "confluent": 265.2239, "dekafDisplay": "194.54 μs/msg (1.4× less)", "confluentDisplay": "265.22 μs/msg"}, {"label": "Consume — messages", "dekaf": 0.7962, "confluent": 1.2249, "dekafDisplay": "0.80 μs/msg (1.5× less)", "confluentDisplay": "1.22 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,464,246 msg/s | 1,177,881 msg/s | 1.2× faster | 2.0× less |
| Produce — fire-and-forget (3 brokers) | 1,284,311 msg/s | 756,056 msg/s | 1.7× faster | 2.2× less |
| Produce — acks=all | 1,473,095 msg/s | 1,360,537 msg/s | 1.1× faster | 1.8× less |
| Produce — acks=all (3 brokers) | 1,090,347 msg/s | 775,189 msg/s | 1.4× faster | 1.9× less |
| Produce — fire-and-forget, idempotent | 1,424,791 msg/s | 1,083,288 msg/s | 1.3× faster | 2.0× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,133,939 msg/s | 866,678 msg/s | 1.3× faster | 2.0× less |
| Produce + consume round-trip | 2,625,779 msg/s | 1,763,442 msg/s | 1.5× faster | 1.9× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,272 msg/s | 172 msg/s | 7.4× faster | 1.4× less |
| Consume — messages | 1,644,970 msg/s | 1,173,883 msg/s | 1.4× faster | 1.5× less |
| Consume — batches | 1,878,183 msg/s | — | — | — |
| Consume — raw bytes | 3,445,061 msg/s | — | — | — |
| Consume — raw byte batches | 4,072,719 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,464,246 | 1,449,454–1,479,189 | 0.72 | 1.24x |
| Confluent | 2 | 1,177,881 | 1,175,264–1,180,504 | 1.43 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.65 | 642.25 | 2,196,495 | 2,290,828 | -17.5% | -1.44% | 2094.74 | 2,196,495 | 0 | 1.42 |
| Dekaf (dekaf-first) | 0.74 | 751.55 | 1,466,928 | 1,479,189 | +1.6% | +0.10% | 1398.97 | 1,466,928 | 0 | 1.09 |
| Dekaf (confluent-first) | 0.70 | 678.78 | 1,442,264 | 1,449,454 | +0.0% | -0.01% | 1375.45 | 1,442,264 | 0 | 1.00 |
| Confluent (confluent-first) | 1.42 | - | 1,171,569 | 1,180,504 | +4.2% | +0.32% | 1117.30 | 1,171,569 | 0 | 1.66 |
| Confluent (dekaf-first) | 1.44 | - | 1,139,625 | 1,175,264 | -12.5% | -1.10% | 1086.83 | 1,139,625 | 0 | 1.64 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.96 | 921.90 | 1,277,423 | 1,284,311 | +3.9% | +0.52% | 1218.25 | 1,277,423 | 0 | 1.22 |
| Dekaf (3conn) | 1.20 | 1105.55 | 1,093,656 | 1,117,668 | +24.9% | +2.16% | 1042.99 | 1,093,656 | 0 | 1.32 |
| Confluent | 2.07 | - | 735,604 | 756,056 | -44.1% | -3.98% | 701.53 | 735,604 | 0 | 1.53 |

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
| Dekaf | 2 | 1,473,095 | 1,468,540–1,477,665 | 0.72 | 1.08x |
| Confluent | 2 | 1,360,537 | 1,354,703–1,366,396 | 1.33 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.76 | 774.63 | 1,459,168 | 1,477,665 | +8.7% | +0.68% | 1391.57 | 1,459,168 | 0 | 1.10 |
| Dekaf (confluent-first) | 0.69 | 678.92 | 1,453,553 | 1,468,540 | +0.6% | -0.05% | 1386.22 | 1,453,553 | 0 | 1.00 |
| Confluent (confluent-first) | 1.32 | - | 1,333,676 | 1,366,396 | -0.1% | -0.02% | 1271.89 | 1,333,676 | 0 | 1.76 |
| Confluent (dekaf-first) | 1.34 | - | 1,310,550 | 1,354,703 | +3.7% | +0.34% | 1249.84 | 1,310,550 | 0 | 1.75 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.04 | 1048.25 | 1,079,242 | 1,090,347 | +0.2% | +0.08% | 1029.24 | 1,079,242 | 0 | 1.12 |
| Confluent | 1.99 | - | 769,235 | 775,189 | +3.9% | +0.41% | 733.60 | 769,235 | 0 | 1.53 |

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
| Dekaf | 2 | 1,424,791 | 1,385,539–1,465,154 | 0.80 | 1.32x |
| Confluent | 2 | 1,083,288 | 1,050,607–1,116,985 | 1.60 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.73 | 718.44 | 1,988,613 | 1,999,584 | -7.6% | -0.48% | 1896.49 | 1,988,613 | 0 | 1.46 |
| Dekaf (confluent-first) | 0.76 | 784.79 | 1,443,251 | 1,465,154 | +3.0% | +0.16% | 1376.39 | 1,443,251 | 0 | 1.10 |
| Dekaf (dekaf-first) | 0.83 | 827.72 | 1,374,294 | 1,385,539 | +13.8% | +1.27% | 1310.63 | 1,374,294 | 0 | 1.14 |
| Confluent (dekaf-first) | 1.55 | - | 1,113,208 | 1,116,985 | -11.5% | -1.20% | 1061.64 | 1,113,208 | 0 | 1.73 |
| Confluent (confluent-first) | 1.66 | - | 1,050,849 | 1,050,607 | +28.4% | +2.54% | 1002.17 | 1,050,849 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.89 | 892.14 | 1,230,449 | 1,239,669 | +2.8% | +0.30% | 1173.45 | 1,230,449 | 0 | 1.09 |
| Dekaf | 0.90 | 908.80 | 1,123,257 | 1,133,939 | +2.3% | +0.22% | 1071.22 | 1,123,257 | 0 | 1.01 |
| Confluent | 1.83 | - | 863,643 | 866,678 | +2.7% | +0.28% | 823.63 | 863,643 | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.90 | 3000.11 | 1,347,820 | 2,625,779 | +82.5% | +769.66% | 164.53 | 1,347,820 | 0 | 1.22 |
| Confluent | 1.76 | - | 126,986 | 1,763,442 | +12.4% | +112.41% | 15.50 | 126,986 | 0 | 0.22 |

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
| Dekaf | 194.54 | 194.54 | 935 | 1,272 | +3.2% | +0.43% | 0.89 | 1,247 | 0 | 0.24 |
| Confluent | 265.22 | - | 129 | 172 | +0.3% | +0.02% | 0.12 | 172 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 155,200 | 116,400 | 38,800 | 116,400 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 1,122,400 | 841,800 | 280,600 | 841,800 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.80 | - | 1,651,394 | 1,644,970 | +0.9% | +0.05% | 1574.89 | - | 0 | 1.31 |
| Confluent | 1.22 | - | 1,134,929 | 1,173,883 | -1.7% | -0.20% | 1082.35 | - | 0 | 1.39 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.72 | - | 1,885,116 | 1,878,183 | -7.8% | -0.79% | 1797.79 | - | 0 | 1.35 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.46 | - | 3,430,040 | 3,445,061 | +7.1% | +0.73% | 3271.14 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.39 | - | 4,099,426 | 4,072,719 | -7.8% | -0.68% | 3909.52 | - | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 60533 | 364 | 0 | 2321.16 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 246332 | 11 | 1 | 1230.91 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 235348 | 1 | 1 | 1265.35 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 166941 | 1 | 1 | 794.60 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 294312 | 24 | 1 | 1415.47 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 275166 | 1 | 1 | 1440.38 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 176326 | 27 | 1 | 830.91 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 249948 | 5 | 1 | 1202.34 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 212576 | 1 | 1 | 1134.97 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 198408 | 1 | 1 | 932.86 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 4750 | 2 | 1 | 11.61 GB | 630 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 113 | 1 | 1 | 83.52 MB | 564 B |
| Dekaf | Consumer | 73723 | 139 | 5 | 2802.11 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 28138 | 4 | 2 | 3198.97 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 5 | 2 | 1 | 455.12 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 15 | 4 | 1 | 1000.94 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 211 | 3 | 2 | 793.68 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 214 | 2 | 1 | 134.99 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 162 | 4 | 2 | 716.34 MB | 1 B |
| Dekaf | Producer (Acks All) | 234 | 3 | 1 | 831.96 MB | 1 B |
| Dekaf | Producer (Acks All) | 229 | 3 | 2 | 143.31 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 140 | 4 | 2 | 585.58 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 194 | 3 | 2 | 715.70 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 216 | 6 | 2 | 184.67 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 141 | 4 | 3 | 608.80 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 592 | 3 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 87 | 2 | 1 | 341.48 MB | 319 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 292 | 3 | 2 | 1.11 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 142 | 6 | 2 | 632.62 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 244 | 3 | 2 | 1.01 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 152 | 4 | 2 | 664.72 MB | 1 B |

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
