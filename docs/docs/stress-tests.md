---
sidebar_position: 14
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-30 03:31 UTC

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
  items={[{"label": "Produce — fire-and-forget", "dekaf": 1602860.738, "confluent": 1410484.5451, "dekafDisplay": "1.60M msg/s (1.1×)", "confluentDisplay": "1.41M msg/s"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1283826.9559, "confluent": 853973.6054, "dekafDisplay": "1.28M msg/s (1.5×)", "confluentDisplay": "853.97K msg/s"}, {"label": "Produce — acks=all", "dekaf": 1558420.6547, "confluent": 1189415.343, "dekafDisplay": "1.56M msg/s (1.3×)", "confluentDisplay": "1.19M msg/s"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 988998.8701, "confluent": 731894.1816, "dekafDisplay": "989.00K msg/s (1.4×)", "confluentDisplay": "731.89K msg/s"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1558802.9824, "confluent": 1305845.7863, "dekafDisplay": "1.56M msg/s (1.2×)", "confluentDisplay": "1.31M msg/s"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1033781.7445, "confluent": 729525.2505, "dekafDisplay": "1.03M msg/s (1.4×)", "confluentDisplay": "729.53K msg/s"}, {"label": "Produce + consume round-trip", "dekaf": 3076988.256, "confluent": 1823886.8753, "dekafDisplay": "3.08M msg/s (1.7×)", "confluentDisplay": "1.82M msg/s"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 1047.8306, "confluent": 164.9393, "dekafDisplay": "1.05K msg/s (6.4×)", "confluentDisplay": "165 msg/s"}, {"label": "Consume — messages", "dekaf": 1596488.9618, "confluent": 1255967.9989, "dekafDisplay": "1.60M msg/s (1.3×)", "confluentDisplay": "1.26M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label": "Produce — fire-and-forget", "dekaf": 0.7423, "confluent": 1.2715, "dekafDisplay": "0.74 μs/msg (1.7× less)", "confluentDisplay": "1.27 μs/msg"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 0.9502, "confluent": 1.7849, "dekafDisplay": "0.95 μs/msg (1.9× less)", "confluentDisplay": "1.78 μs/msg"}, {"label": "Produce — acks=all", "dekaf": 0.746, "confluent": 1.4395, "dekafDisplay": "0.75 μs/msg (1.9× less)", "confluentDisplay": "1.44 μs/msg"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1.056, "confluent": 2.1673, "dekafDisplay": "1.06 μs/msg (2.1× less)", "confluentDisplay": "2.17 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 0.7549, "confluent": 1.3528, "dekafDisplay": "0.75 μs/msg (1.8× less)", "confluentDisplay": "1.35 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1.0673, "confluent": 2.188, "dekafDisplay": "1.07 μs/msg (2.1× less)", "confluentDisplay": "2.19 μs/msg"}, {"label": "Produce + consume round-trip", "dekaf": 0.8611, "confluent": 1.7378, "dekafDisplay": "0.86 μs/msg (2.0× less)", "confluentDisplay": "1.74 μs/msg"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 219.9416, "confluent": 304.2308, "dekafDisplay": "219.94 μs/msg (1.4× less)", "confluentDisplay": "304.23 μs/msg"}, {"label": "Consume — messages", "dekaf": 0.8221, "confluent": 1.0976, "dekafDisplay": "0.82 μs/msg (1.3× less)", "confluentDisplay": "1.10 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,602,861 msg/s | 1,410,485 msg/s | 1.1× faster | 1.7× less |
| Produce — fire-and-forget (3 brokers) | 1,283,827 msg/s | 853,974 msg/s | 1.5× faster | 1.9× less |
| Produce — acks=all | 1,558,421 msg/s | 1,189,415 msg/s | 1.3× faster | 1.9× less |
| Produce — acks=all (3 brokers) | 988,999 msg/s | 731,894 msg/s | 1.4× faster | 2.1× less |
| Produce — fire-and-forget, idempotent | 1,558,803 msg/s | 1,305,846 msg/s | 1.2× faster | 1.8× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,033,782 msg/s | 729,525 msg/s | 1.4× faster | 2.1× less |
| Produce + consume round-trip | 3,076,988 msg/s | 1,823,887 msg/s | 1.7× faster | 2.0× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,048 msg/s | 165 msg/s | 6.4× faster | 1.4× less |
| Consume — messages | 1,596,489 msg/s | 1,255,968 msg/s | 1.3× faster | 1.3× less |
| Consume — batches | 1,812,595 msg/s | — | — | — |
| Consume — raw bytes | 3,473,259 msg/s | — | — | — |
| Consume — raw byte batches | 3,999,689 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,602,861 | 1,590,063–1,615,762 | 0.74 | 1.14x |
| Confluent | 2 | 1,410,485 | 1,392,991–1,428,197 | 1.27 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.61 | 608.80 | 2,423,167 | 2,467,238 | -0.7% | +0.03% | 2310.91 | 2,423,167 | 0 | 1.48 |
| Dekaf (dekaf-first) | 0.74 | 761.02 | 1,598,251 | 1,615,762 | -1.5% | -0.10% | 1524.21 | 1,598,251 | 0 | 1.19 |
| Dekaf (confluent-first) | 0.74 | 761.84 | 1,581,603 | 1,590,063 | +0.6% | +0.03% | 1508.33 | 1,581,603 | 0 | 1.17 |
| Confluent (dekaf-first) | 1.26 | - | 1,394,179 | 1,428,197 | +9.4% | +0.67% | 1329.59 | 1,394,179 | 0 | 1.75 |
| Confluent (confluent-first) | 1.29 | - | 1,370,029 | 1,392,991 | +5.3% | +0.45% | 1306.56 | 1,370,029 | 0 | 1.76 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.95 | 917.79 | 1,270,927 | 1,283,827 | +1.4% | +0.19% | 1212.05 | 1,270,927 | 0 | 1.21 |
| Dekaf (3conn) | 1.14 | 1069.44 | 1,186,997 | 1,181,823 | +7.6% | +0.56% | 1132.01 | 1,186,997 | 0 | 1.35 |
| Confluent | 1.78 | - | 848,521 | 853,974 | -5.4% | -0.26% | 809.21 | 848,521 | 0 | 1.51 |

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
| Dekaf | 2 | 1,558,421 | 1,514,146–1,603,990 | 0.75 | 1.31x |
| Confluent | 2 | 1,189,415 | 1,104,038–1,281,395 | 1.44 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.73 | 748.84 | 1,586,540 | 1,603,990 | -1.2% | -0.11% | 1513.04 | 1,586,540 | 0 | 1.16 |
| Dekaf (confluent-first) | 0.76 | 780.86 | 1,484,119 | 1,514,146 | +0.5% | +0.16% | 1415.37 | 1,484,119 | 0 | 1.13 |
| Confluent (confluent-first) | 1.39 | - | 1,257,872 | 1,281,395 | +4.9% | +0.48% | 1199.60 | 1,257,872 | 0 | 1.75 |
| Confluent (dekaf-first) | 1.48 | - | 1,150,986 | 1,104,038 | -21.9% | -1.82% | 1097.67 | 1,150,986 | 0 | 1.71 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.06 | 1057.32 | 954,707 | 988,999 | +23.2% | +2.15% | 910.48 | 954,707 | 0 | 1.01 |
| Confluent | 2.17 | - | 714,752 | 731,894 | -13.4% | -1.17% | 681.64 | 714,752 | 0 | 1.55 |

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
| Dekaf | 2 | 1,558,803 | 1,539,662–1,578,182 | 0.75 | 1.19x |
| Confluent | 2 | 1,305,846 | 1,298,163–1,313,574 | 1.35 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.61 | 609.71 | 2,557,658 | 2,570,343 | +5.4% | +0.43% | 2439.17 | 2,557,658 | 0 | 1.55 |
| Dekaf (dekaf-first) | 0.76 | 782.16 | 1,567,243 | 1,578,182 | -1.1% | -0.06% | 1494.64 | 1,567,243 | 0 | 1.19 |
| Dekaf (confluent-first) | 0.75 | 767.65 | 1,531,594 | 1,539,662 | +0.7% | +0.08% | 1460.64 | 1,531,594 | 0 | 1.15 |
| Confluent (dekaf-first) | 1.32 | - | 1,305,165 | 1,313,574 | -1.4% | -0.05% | 1244.70 | 1,305,165 | 0 | 1.72 |
| Confluent (confluent-first) | 1.39 | - | 1,284,100 | 1,298,163 | -9.0% | -0.66% | 1224.61 | 1,284,100 | 0 | 1.78 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.04 | 1050.00 | 1,114,682 | 1,120,017 | -0.0% | +0.02% | 1063.04 | 1,114,682 | 0 | 1.16 |
| Dekaf | 1.07 | 1082.35 | 1,029,416 | 1,033,782 | +3.5% | +0.44% | 981.73 | 1,029,416 | 0 | 1.10 |
| Confluent | 2.19 | - | 728,743 | 729,525 | +1.6% | +0.10% | 694.98 | 728,743 | 0 | 1.59 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.86 | 3951.73 | 1,481,071 | 3,076,988 | -20.4% | -204.56% | 180.79 | 1,481,071 | 0 | 1.28 |
| Confluent | 1.74 | - | 135,335 | 1,823,887 | +41.7% | +281.91% | 16.52 | 135,335 | 0 | 0.24 |

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
| Dekaf | 219.94 | 219.94 | 775 | 1,048 | +4.7% | +0.51% | 0.74 | 1,033 | 0 | 0.23 |
| Confluent | 304.23 | - | 124 | 165 | +0.7% | +0.08% | 0.12 | 165 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 148,500 | 111,400 | 37,100 | 111,400 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 929,400 | 697,100 | 232,300 | 697,100 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.82 | - | 1,610,299 | 1,596,489 | -7.5% | -0.59% | 1535.70 | - | 0 | 1.32 |
| Confluent | 1.10 | - | 1,166,000 | 1,255,968 | +7.0% | +0.53% | 1111.98 | - | 0 | 1.28 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.73 | - | 1,802,239 | 1,812,595 | -3.6% | -0.35% | 1718.75 | - | 0 | 1.32 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.45 | - | 3,450,749 | 3,473,259 | +1.2% | +0.11% | 3290.89 | - | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.38 | - | 4,026,708 | 3,999,689 | -0.5% | +0.00% | 3840.17 | - | 0 | 1.52 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 62184 | 378 | 0 | 2384.70 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 294507 | 1 | 1 | 1479.63 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 313932 | 47 | 1 | 1505.84 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 194562 | 25 | 1 | 916.62 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 271856 | 1 | 1 | 1358.55 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 260402 | 67 | 1 | 1243.20 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 161775 | 0 | 0 | 772.01 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 281064 | 1 | 1 | 1386.85 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 293796 | 65 | 1 | 1409.66 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 164265 | 1 | 1 | 787.09 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 6750 | 1 | 1 | 15.88 GB | 861 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 100 | 1 | 1 | 111.87 MB | 790 B |
| Dekaf | Consumer | 71872 | 40 | 3 | 2732.36 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 80349 | 7 | 3 | 3058.30 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 3 | 1 | 1 | 458.99 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 19 | 2 | 1 | 959.94 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 227 | 3 | 1 | 164.56 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 223 | 3 | 2 | 844.22 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 147 | 5 | 4 | 624.02 MB | 1 B |
| Dekaf | Producer (Acks All) | 215 | 3 | 2 | 115.71 MB | 0 B |
| Dekaf | Producer (Acks All) | 228 | 3 | 2 | 853.21 MB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 115 | 3 | 2 | 520.15 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 214 | 3 | 2 | 101.04 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 230 | 4 | 2 | 817.64 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 127 | 3 | 2 | 627.28 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 602 | 2 | 1 | 2.81 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 67 | 2 | 1 | 260.44 MB | 294 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 362 | 6 | 2 | 1.21 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 172 | 8 | 2 | 649.95 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 306 | 4 | 2 | 1.25 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 140 | 2 | 1 | 691.49 MB | 1 B |

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
