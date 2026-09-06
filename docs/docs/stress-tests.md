---
sidebar_position: 14
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-09-06 03:46 UTC

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
  items={[{"label": "Produce — fire-and-forget", "dekaf": 1446138.8705, "confluent": 995249.0614, "dekafDisplay": "1.45M msg/s (1.5×)", "confluentDisplay": "995.25K msg/s"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1165880.6401, "confluent": 713583.9356, "dekafDisplay": "1.17M msg/s (1.6×)", "confluentDisplay": "713.58K msg/s"}, {"label": "Produce — acks=all", "dekaf": 1540522.8053, "confluent": 1228208.2808, "dekafDisplay": "1.54M msg/s (1.3×)", "confluentDisplay": "1.23M msg/s"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1129651.0193, "confluent": 751175.5399, "dekafDisplay": "1.13M msg/s (1.5×)", "confluentDisplay": "751.18K msg/s"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1490271.3056, "confluent": 1263552.2071, "dekafDisplay": "1.49M msg/s (1.2×)", "confluentDisplay": "1.26M msg/s"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1116744.3479, "confluent": 803597.2977, "dekafDisplay": "1.12M msg/s (1.4×)", "confluentDisplay": "803.60K msg/s"}, {"label": "Produce + consume round-trip", "dekaf": 2625535.2359, "confluent": 1641424.1751, "dekafDisplay": "2.63M msg/s (1.6×)", "confluentDisplay": "1.64M msg/s"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 1192.6812, "confluent": 168.9303, "dekafDisplay": "1.19K msg/s (7.1×)", "confluentDisplay": "169 msg/s"}, {"label": "Consume — messages", "dekaf": 1751562.9882, "confluent": 1329577.4878, "dekafDisplay": "1.75M msg/s (1.3×)", "confluentDisplay": "1.33M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label": "Produce — fire-and-forget", "dekaf": 0.7192, "confluent": 1.7187, "dekafDisplay": "0.72 μs/msg (2.4× less)", "confluentDisplay": "1.72 μs/msg"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1.0677, "confluent": 2.1089, "dekafDisplay": "1.07 μs/msg (2.0× less)", "confluentDisplay": "2.11 μs/msg"}, {"label": "Produce — acks=all", "dekaf": 0.7012, "confluent": 1.4167, "dekafDisplay": "0.70 μs/msg (2.0× less)", "confluentDisplay": "1.42 μs/msg"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 0.9477, "confluent": 2.0505, "dekafDisplay": "0.95 μs/msg (2.2× less)", "confluentDisplay": "2.05 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 0.6893, "confluent": 1.4075, "dekafDisplay": "0.69 μs/msg (2.0× less)", "confluentDisplay": "1.41 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 0.8781, "confluent": 1.9703, "dekafDisplay": "0.88 μs/msg (2.2× less)", "confluentDisplay": "1.97 μs/msg"}, {"label": "Produce + consume round-trip", "dekaf": 0.8811, "confluent": 1.8544, "dekafDisplay": "0.88 μs/msg (2.1× less)", "confluentDisplay": "1.85 μs/msg"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 228.8411, "confluent": 258.8247, "dekafDisplay": "228.84 μs/msg (1.1× less)", "confluentDisplay": "258.82 μs/msg"}, {"label": "Consume — messages", "dekaf": 0.7576, "confluent": 1.1353, "dekafDisplay": "0.76 μs/msg (1.5× less)", "confluentDisplay": "1.14 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,446,139 msg/s | 995,249 msg/s | 1.5× faster | 2.4× less |
| Produce — fire-and-forget (3 brokers) | 1,165,881 msg/s | 713,584 msg/s | 1.6× faster | 2.0× less |
| Produce — acks=all | 1,540,523 msg/s | 1,228,208 msg/s | 1.3× faster | 2.0× less |
| Produce — acks=all (3 brokers) | 1,129,651 msg/s | 751,176 msg/s | 1.5× faster | 2.2× less |
| Produce — fire-and-forget, idempotent | 1,490,271 msg/s | 1,263,552 msg/s | 1.2× faster | 2.0× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,116,744 msg/s | 803,597 msg/s | 1.4× faster | 2.2× less |
| Produce + consume round-trip | 2,625,535 msg/s | 1,641,424 msg/s | 1.6× faster | 2.1× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,193 msg/s | 169 msg/s | 7.1× faster | 1.1× less |
| Consume — messages | 1,751,563 msg/s | 1,329,577 msg/s | 1.3× faster | 1.5× less |
| Consume — batches | 1,732,327 msg/s | — | — | — |
| Consume — raw bytes | 3,741,807 msg/s | — | — | — |
| Consume — raw byte batches | 4,094,242 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,446,139 | 1,395,659–1,498,445 | 0.72 | 1.45x |
| Confluent | 2 | 995,249 | 917,674–1,079,382 | 1.72 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.62 | 629.06 | 2,653,207 | 2,746,307 | +16.7% | +1.80% | 2530.30 | 2,653,207 | 0 | 1.65 |
| Dekaf (adaptive) | 0.65 | 645.15 | 2,327,972 | 2,309,915 | +14.2% | +1.66% | 2220.13 | 2,327,972 | 0 | 1.51 |
| Dekaf (confluent-first) | 0.69 | 679.68 | 1,487,235 | 1,498,445 | -0.4% | -0.06% | 1418.34 | 1,487,235 | 0 | 1.03 |
| Dekaf (dekaf-first) | 0.75 | 736.54 | 1,394,493 | 1,395,659 | -8.8% | -0.64% | 1329.89 | 1,394,493 | 0 | 1.04 |
| Confluent (dekaf-first) | 1.64 | - | 1,027,057 | 1,079,382 | -4.1% | -0.68% | 979.48 | 1,027,057 | 0 | 1.68 |
| Confluent (confluent-first) | 1.80 | - | 954,617 | 917,674 | +23.4% | +2.35% | 910.39 | 954,617 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (adaptive) | 1.01 | 946.53 | 1,251,558 | 1,211,244 | +17.1% | +1.69% | 1193.58 | 1,251,558 | 0 | 1.27 |
| Dekaf (3conn) | 1.04 | 957.63 | 1,229,414 | 1,188,344 | -19.2% | -1.73% | 1172.46 | 1,229,414 | 0 | 1.27 |
| Dekaf | 1.07 | 1019.22 | 1,151,570 | 1,165,881 | +5.8% | +0.59% | 1098.22 | 1,151,570 | 0 | 1.23 |
| Confluent | 2.11 | - | 723,211 | 713,584 | -9.1% | -0.72% | 689.71 | 723,211 | 0 | 1.53 |

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
| Dekaf | 2 | 1,540,523 | 1,515,172–1,566,298 | 0.70 | 1.25x |
| Confluent | 2 | 1,228,208 | 1,153,504–1,307,751 | 1.42 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 0.70 | 714.82 | 1,557,326 | 1,566,298 | -1.1% | -0.11% | 1485.18 | 1,557,326 | 0 | 1.09 |
| Dekaf (dekaf-first) | 0.70 | 709.76 | 1,486,940 | 1,515,172 | -6.5% | -0.58% | 1418.06 | 1,486,940 | 0 | 1.04 |
| Confluent (confluent-first) | 1.37 | - | 1,288,518 | 1,307,751 | -3.5% | -0.27% | 1228.83 | 1,288,518 | 0 | 1.77 |
| Confluent (dekaf-first) | 1.46 | - | 1,161,401 | 1,153,504 | +9.4% | +0.94% | 1107.60 | 1,161,401 | 0 | 1.70 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.95 | 937.60 | 1,117,845 | 1,129,651 | +7.7% | +0.60% | 1066.06 | 1,117,845 | 0 | 1.06 |
| Confluent | 2.05 | - | 751,552 | 751,176 | +25.0% | +2.03% | 716.74 | 751,552 | 0 | 1.54 |

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
| Dekaf | 2 | 1,490,271 | 1,459,918–1,521,256 | 0.69 | 1.18x |
| Confluent | 2 | 1,263,552 | 1,233,956–1,293,858 | 1.41 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (adaptive) | 0.61 | 611.45 | 2,425,532 | 2,479,418 | +11.5% | +1.13% | 2313.17 | 2,425,532 | 0 | 1.48 |
| Dekaf (3conn) | 0.60 | 587.96 | 2,274,289 | 2,280,285 | -4.5% | -0.39% | 2168.93 | 2,274,289 | 0 | 1.36 |
| Dekaf (confluent-first) | 0.70 | 704.59 | 1,508,119 | 1,521,256 | -1.3% | -0.12% | 1438.25 | 1,508,119 | 0 | 1.06 |
| Dekaf (dekaf-first) | 0.68 | 658.45 | 1,452,680 | 1,459,918 | +0.9% | +0.08% | 1385.38 | 1,452,680 | 0 | 0.99 |
| Confluent (dekaf-first) | 1.40 | - | 1,222,768 | 1,293,858 | +2.1% | -0.08% | 1166.12 | 1,222,768 | 0 | 1.71 |
| Confluent (confluent-first) | 1.42 | - | 1,211,858 | 1,233,956 | +0.2% | -0.02% | 1155.72 | 1,211,858 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (adaptive) | 0.82 | 815.42 | 1,244,391 | 1,255,058 | -1.7% | -0.09% | 1186.74 | 1,244,391 | 0 | 1.02 |
| Dekaf (3conn) | 0.83 | 817.60 | 1,239,171 | 1,250,776 | -0.6% | -0.06% | 1181.77 | 1,239,171 | 0 | 1.02 |
| Dekaf | 0.88 | 864.89 | 1,110,101 | 1,116,744 | +0.9% | +0.09% | 1058.67 | 1,110,101 | 0 | 0.97 |
| Confluent | 1.97 | - | 800,917 | 803,597 | +4.5% | +0.41% | 763.81 | 800,917 | 0 | 1.58 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.88 | 3066.00 | 1,466,307 | 2,625,535 | +59.2% | +571.55% | 178.99 | 1,466,307 | 0 | 1.29 |
| Confluent | 1.85 | - | 127,287 | 1,641,424 | +10.5% | +107.74% | 15.54 | 127,287 | 0 | 0.24 |

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
| Dekaf | 228.84 | 228.84 | 889 | 1,193 | +0.8% | +0.09% | 0.85 | 1,185 | 0 | 0.27 |
| Confluent | 258.82 | - | 126 | 169 | +10.1% | +0.94% | 0.12 | 168 | 0 | 0.04 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 151,100 | 113,400 | 37,700 | 113,400 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 1,066,700 | 800,100 | 266,600 | 800,100 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.76 | - | 1,744,457 | 1,751,563 | -1.1% | -0.10% | 1663.64 | - | 0 | 1.32 |
| Confluent | 1.14 | - | 1,302,386 | 1,329,577 | +5.9% | +0.59% | 1242.05 | - | 0 | 1.48 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.78 | - | 1,740,015 | 1,732,327 | -6.8% | -0.67% | 1659.41 | - | 0 | 1.36 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.42 | - | 3,718,641 | 3,741,807 | +2.6% | +0.25% | 3546.37 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.34 | - | 4,168,381 | 4,094,242 | +0.6% | +0.14% | 3975.28 | - | 0 | 1.44 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 23021 | 93 | 1 | 2663.62 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 231796 | 35 | 1 | 1109.33 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 192267 | 1 | 1 | 1031.03 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 149622 | 0 | 0 | 781.11 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 260614 | 36 | 1 | 1254.39 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 282816 | 1 | 1 | 1391.65 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 165567 | 1 | 1 | 811.72 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 267165 | 1 | 1 | 1308.82 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 270404 | 44 | 1 | 1320.72 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 174194 | 0 | 0 | 865.04 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 6915 | 0 | 0 | 17.56 GB | 953 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 104 | 1 | 0 | 309.75 MB | 2.10 KB |
| Dekaf | Consumer | 26079 | 72 | 4 | 2959.98 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 25986 | 5 | 2 | 2952.68 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 5 | 2 | 1 | 492.08 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 9 | 2 | 1 | 989.48 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 225 | 4 | 2 | 788.32 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget) | 222 | 3 | 2 | 192.89 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 145 | 3 | 2 | 150.72 MB | 0 B |
| Dekaf | Producer (Acks All) | 224 | 3 | 1 | 791.56 MB | 1 B |
| Dekaf | Producer (Acks All) | 227 | 2 | 1 | 164.98 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 132 | 3 | 2 | 232.53 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 207 | 2 | 1 | 152.70 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 203 | 3 | 2 | 755.63 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 151 | 3 | 2 | 135.65 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 1071 | 3 | 1 | 2.81 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 84 | 1 | 1 | 175.07 MB | 172 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 327 | 3 | 1 | 1.27 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 188 | 5 | 2 | 737.70 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 276 | 2 | 1 | 1.14 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 157 | 3 | 2 | 670.81 MB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget) | 307 | 3 | 2 | 1.13 GB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget), 3 Brokers | 170 | 5 | 3 | 723.16 MB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget, Idempotent) | 315 | 3 | 2 | 1.17 GB | 1 B |
| Dekaf (adaptive) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 148 | 3 | 2 | 675.10 MB | 1 B |

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
