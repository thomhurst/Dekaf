---
sidebar_position: 14
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-09-05 07:20 UTC

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
  items={[{"label": "Produce — fire-and-forget", "dekaf": 1567107.4843, "confluent": 1404612.2708, "dekafDisplay": "1.57M msg/s (1.1×)", "confluentDisplay": "1.40M msg/s"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 1234552.1528, "confluent": 916032.0956, "dekafDisplay": "1.23M msg/s (1.3×)", "confluentDisplay": "916.03K msg/s"}, {"label": "Produce — acks=all", "dekaf": 1549013.739, "confluent": 1364161.6092, "dekafDisplay": "1.55M msg/s (1.1×)", "confluentDisplay": "1.36M msg/s"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 1053174.7016, "confluent": 792172.2864, "dekafDisplay": "1.05M msg/s (1.3×)", "confluentDisplay": "792.17K msg/s"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 1557585.933, "confluent": 1360148.0288, "dekafDisplay": "1.56M msg/s (1.1×)", "confluentDisplay": "1.36M msg/s"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 1151290.0665, "confluent": 915729.8428, "dekafDisplay": "1.15M msg/s (1.3×)", "confluentDisplay": "915.73K msg/s"}, {"label": "Produce + consume round-trip", "dekaf": 2905720.557, "confluent": 1659285.2411, "dekafDisplay": "2.91M msg/s (1.8×)", "confluentDisplay": "1.66M msg/s"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 1058.0189, "confluent": 165.9586, "dekafDisplay": "1.06K msg/s (6.4×)", "confluentDisplay": "166 msg/s"}, {"label": "Consume — messages", "dekaf": 1716852.7295, "confluent": 1343318.539, "dekafDisplay": "1.72M msg/s (1.3×)", "confluentDisplay": "1.34M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label": "Produce — fire-and-forget", "dekaf": 0.7231, "confluent": 1.2835, "dekafDisplay": "0.72 μs/msg (1.8× less)", "confluentDisplay": "1.28 μs/msg"}, {"label": "Produce — fire-and-forget (3 brokers)", "dekaf": 0.9518, "confluent": 1.6489, "dekafDisplay": "0.95 μs/msg (1.7× less)", "confluentDisplay": "1.65 μs/msg"}, {"label": "Produce — acks=all", "dekaf": 0.7147, "confluent": 1.2829, "dekafDisplay": "0.71 μs/msg (1.8× less)", "confluentDisplay": "1.28 μs/msg"}, {"label": "Produce — acks=all (3 brokers)", "dekaf": 0.9783, "confluent": 1.9572, "dekafDisplay": "0.98 μs/msg (2.0× less)", "confluentDisplay": "1.96 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent", "dekaf": 0.7141, "confluent": 1.2878, "dekafDisplay": "0.71 μs/msg (1.8× less)", "confluentDisplay": "1.29 μs/msg"}, {"label": "Produce — fire-and-forget, idempotent (3 brokers)", "dekaf": 0.9066, "confluent": 1.7114, "dekafDisplay": "0.91 μs/msg (1.9× less)", "confluentDisplay": "1.71 μs/msg"}, {"label": "Produce + consume round-trip", "dekaf": 0.8253, "confluent": 1.8565, "dekafDisplay": "0.83 μs/msg (2.2× less)", "confluentDisplay": "1.86 μs/msg"}, {"label": "Produce — transactional (exactly-once) (3 brokers)", "dekaf": 231.2831, "confluent": 296.9715, "dekafDisplay": "231.28 μs/msg (1.3× less)", "confluentDisplay": "296.97 μs/msg"}, {"label": "Consume — messages", "dekaf": 0.7658, "confluent": 1.1242, "dekafDisplay": "0.77 μs/msg (1.5× less)", "confluentDisplay": "1.12 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,567,107 msg/s | 1,404,612 msg/s | 1.1× faster | 1.8× less |
| Produce — fire-and-forget (3 brokers) | 1,234,552 msg/s | 916,032 msg/s | 1.3× faster | 1.7× less |
| Produce — acks=all | 1,549,014 msg/s | 1,364,162 msg/s | 1.1× faster | 1.8× less |
| Produce — acks=all (3 brokers) | 1,053,175 msg/s | 792,172 msg/s | 1.3× faster | 2.0× less |
| Produce — fire-and-forget, idempotent | 1,557,586 msg/s | 1,360,148 msg/s | 1.1× faster | 1.8× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,151,290 msg/s | 915,730 msg/s | 1.3× faster | 1.9× less |
| Produce + consume round-trip | 2,905,721 msg/s | 1,659,285 msg/s | 1.8× faster | 2.2× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,058 msg/s | 166 msg/s | 6.4× faster | 1.3× less |
| Consume — messages | 1,716,853 msg/s | 1,343,319 msg/s | 1.3× faster | 1.5× less |
| Consume — batches | 2,024,285 msg/s | — | — | — |
| Consume — raw bytes | 3,832,148 msg/s | — | — | — |
| Consume — raw byte batches | 4,019,487 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,567,107 | 1,564,169–1,570,052 | 0.72 | 1.12x |
| Confluent | 2 | 1,404,612 | 1,383,881–1,425,654 | 1.28 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.59 | 604.82 | 2,958,212 | 2,964,629 | +3.3% | +0.31% | 2821.17 | 2,958,212 | 0 | 1.75 |
| Dekaf (dekaf-first) | 0.72 | 735.03 | 1,566,078 | 1,570,052 | -0.3% | -0.02% | 1493.53 | 1,566,078 | 0 | 1.12 |
| Dekaf (confluent-first) | 0.73 | 749.63 | 1,554,934 | 1,564,169 | +0.4% | +0.06% | 1482.90 | 1,554,934 | 0 | 1.14 |
| Confluent (confluent-first) | 1.27 | - | 1,414,503 | 1,425,654 | +1.6% | +0.13% | 1348.98 | 1,414,503 | 0 | 1.80 |
| Confluent (dekaf-first) | 1.30 | - | 1,366,667 | 1,383,881 | +2.4% | +0.29% | 1303.35 | 1,366,667 | 0 | 1.77 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.86 | 844.79 | 1,510,854 | 1,512,454 | +1.6% | +0.17% | 1440.86 | 1,510,854 | 0 | 1.30 |
| Dekaf | 0.95 | 918.84 | 1,231,710 | 1,234,552 | -1.0% | -0.09% | 1174.65 | 1,231,710 | 0 | 1.17 |
| Confluent | 1.65 | - | 913,865 | 916,032 | +2.5% | +0.26% | 871.53 | 913,865 | 0 | 1.51 |

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
| Dekaf | 2 | 1,549,014 | 1,536,949–1,561,174 | 0.71 | 1.14x |
| Confluent | 2 | 1,364,162 | 1,361,169–1,367,161 | 1.28 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (confluent-first) | 0.73 | 747.84 | 1,550,364 | 1,561,174 | +1.0% | +0.10% | 1478.54 | 1,550,364 | 0 | 1.13 |
| Dekaf (dekaf-first) | 0.70 | 713.24 | 1,523,370 | 1,536,949 | +0.1% | +0.02% | 1452.80 | 1,523,370 | 0 | 1.06 |
| Confluent (confluent-first) | 1.30 | - | 1,358,817 | 1,367,161 | +2.2% | +0.21% | 1295.87 | 1,358,817 | 0 | 1.76 |
| Confluent (dekaf-first) | 1.27 | - | 1,352,259 | 1,361,169 | +0.1% | +0.04% | 1289.61 | 1,352,259 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.98 | 978.54 | 1,048,452 | 1,053,175 | -0.5% | -0.04% | 999.88 | 1,048,452 | 0 | 1.03 |
| Confluent | 1.96 | - | 791,050 | 792,172 | +2.5% | +0.32% | 754.40 | 791,050 | 0 | 1.55 |

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
| Dekaf | 2 | 1,557,586 | 1,550,596–1,564,607 | 0.71 | 1.15x |
| Confluent | 2 | 1,360,148 | 1,323,748–1,397,549 | 1.29 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.61 | 618.04 | 2,586,218 | 2,608,776 | +0.6% | +0.06% | 2466.41 | 2,586,218 | 0 | 1.59 |
| Dekaf (confluent-first) | 0.72 | 738.90 | 1,554,777 | 1,564,607 | -0.1% | -0.00% | 1482.75 | 1,554,777 | 0 | 1.12 |
| Dekaf (dekaf-first) | 0.71 | 717.35 | 1,542,240 | 1,550,596 | -1.2% | -0.11% | 1470.79 | 1,542,240 | 0 | 1.09 |
| Confluent (confluent-first) | 1.27 | - | 1,387,018 | 1,397,549 | +2.2% | +0.20% | 1322.76 | 1,387,018 | 0 | 1.76 |
| Confluent (dekaf-first) | 1.31 | - | 1,315,950 | 1,323,748 | -3.3% | -0.24% | 1254.99 | 1,315,950 | 0 | 1.72 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.86 | 865.79 | 1,309,378 | 1,321,287 | +1.0% | +0.13% | 1248.72 | 1,309,378 | 0 | 1.12 |
| Dekaf | 0.91 | 917.66 | 1,142,257 | 1,151,290 | -3.2% | -0.37% | 1089.34 | 1,142,257 | 0 | 1.04 |
| Confluent | 1.71 | - | 912,357 | 915,730 | +1.7% | +0.13% | 870.09 | 912,357 | 0 | 1.56 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.83 | 2942.00 | 1,488,541 | 2,905,721 | +62.1% | +711.26% | 181.71 | 1,488,541 | 0 | 1.23 |
| Confluent | 1.86 | - | 129,085 | 1,659,285 | +15.9% | +137.56% | 15.76 | 129,085 | 0 | 0.24 |

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
| Dekaf | 231.28 | 231.28 | 793 | 1,058 | +1.4% | +0.12% | 0.76 | 1,057 | 0 | 0.24 |
| Confluent | 296.97 | - | 124 | 166 | +4.4% | +0.45% | 0.12 | 165 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 148,200 | 111,200 | 37,000 | 111,200 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 951,300 | 713,500 | 237,800 | 713,500 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.77 | - | 1,717,043 | 1,716,853 | -2.4% | -0.19% | 1637.50 | - | 0 | 1.31 |
| Confluent | 1.12 | - | 1,319,592 | 1,343,319 | +1.8% | +0.20% | 1258.46 | - | 0 | 1.48 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.71 | - | 1,958,130 | 2,024,285 | -3.4% | -0.31% | 1867.42 | - | 0 | 1.39 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.41 | - | 3,816,612 | 3,832,148 | +1.6% | +0.13% | 3639.80 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.36 | - | 4,072,526 | 4,019,487 | -3.6% | -0.38% | 3883.86 | - | 0 | 1.46 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 23319 | 101 | 1 | 2698.80 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 302984 | 1 | 1 | 1527.72 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 310914 | 23 | 1 | 1476.12 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 195311 | 0 | 0 | 987.01 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 261324 | 1 | 1 | 1467.58 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 307220 | 12 | 1 | 1460.53 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 167386 | 1 | 1 | 854.37 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 296954 | 16 | 1 | 1421.37 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 283680 | 1 | 1 | 1497.99 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 207425 | 0 | 0 | 985.40 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 6644 | 2 | 2 | 16.74 GB | 908 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 102 | 1 | 0 | 304.00 MB | 2.10 KB |
| Dekaf | Consumer | 25690 | 54 | 3 | 2913.54 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 87259 | 6 | 2 | 3323.04 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 4 | 2 | 1 | 470.29 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 19 | 4 | 2 | 1002.62 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 226 | 2 | 1 | 170.64 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 218 | 3 | 2 | 822.65 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 164 | 3 | 2 | 141.67 MB | 0 B |
| Dekaf | Producer (Acks All) | 233 | 3 | 2 | 166.99 MB | 0 B |
| Dekaf | Producer (Acks All) | 213 | 3 | 2 | 855.36 MB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 126 | 3 | 2 | 148.99 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 213 | 3 | 2 | 794.44 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 218 | 3 | 2 | 104.68 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 150 | 3 | 2 | 128.13 MB | 0 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 1127 | 3 | 1 | 2.81 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 79 | 1 | 1 | 186.66 MB | 206 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 462 | 13 | 3 | 1.38 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 205 | 3 | 2 | 818.64 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 390 | 7 | 2 | 1.25 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 155 | 3 | 2 | 688.47 MB | 1 B |

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
- **Noise-Aware Trends**: each scenario's throughput, CPU per message and Dekaf delivery-latency p50/p95/p99 are compared with its last 10 matching runs using a median ± 2×MAD band; one adverse excursion warns and two consecutive regressions fail the workflow, and paired lanes fail only when the same-run Dekaf/Confluent ratio regressed too
- **Latency Product Bars**: p95 within 3× the configured delivery-latency target and p50/p99 within 2× the same-run Confluent control are reported as product goals, not gates; a lane that misses a bar shows a warning until the trend band moves it
- **Parallel Execution**: Each scenario runs in its own isolated environment
- **Both Clients**: Direct comparison between Dekaf and Confluent.Kafka
- **Memory Monitoring**: Tracks GC behavior and memory usage over time
- **Error Rates**: Ensures stability under load

</details>
