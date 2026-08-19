---
sidebar_position: 14
description: "Long-running throughput and stability comparisons between Dekaf and Confluent.Kafka under sustained real-world load."
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-16 03:47 UTC

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
  items={[{"label":"Produce — fire-and-forget","dekaf":1451115,"confluent":1174152,"dekafDisplay":"1.45M msg/s (1.2×)","confluentDisplay":"1.17M msg/s"},{"label":"Produce — fire-and-forget (3 brokers)","dekaf":1313915,"confluent":968735,"dekafDisplay":"1.31M msg/s (1.4×)","confluentDisplay":"968.74K msg/s"},{"label":"Produce — acks=all","dekaf":1552262,"confluent":1373448,"dekafDisplay":"1.55M msg/s (1.1×)","confluentDisplay":"1.37M msg/s"},{"label":"Produce — acks=all (3 brokers)","dekaf":1091599,"confluent":854849,"dekafDisplay":"1.09M msg/s (1.3×)","confluentDisplay":"854.85K msg/s"},{"label":"Produce — fire-and-forget, idempotent","dekaf":1553605,"confluent":1136261,"dekafDisplay":"1.55M msg/s (1.4×)","confluentDisplay":"1.14M msg/s"},{"label":"Produce — fire-and-forget, idempotent (3 brokers)","dekaf":1135456,"confluent":857910,"dekafDisplay":"1.14M msg/s (1.3×)","confluentDisplay":"857.91K msg/s"},{"label":"Produce + consume round-trip","dekaf":2254179,"confluent":1211189,"dekafDisplay":"2.25M msg/s (1.9×)","confluentDisplay":"1.21M msg/s"},{"label":"Produce — transactional (exactly-once) (3 brokers)","dekaf":1259,"confluent":172,"dekafDisplay":"1.26K msg/s (7.3×)","confluentDisplay":"172 msg/s"},{"label":"Consume — messages","dekaf":1643250,"confluent":1088637,"dekafDisplay":"1.64M msg/s (1.5×)","confluentDisplay":"1.09M msg/s"}]}
/>

<ComparisonChart
  title="CPU cost per message"
  metric="Median client CPU time"
  description="CPU time needed to deliver one message; shorter bars are better."
  better="lower"
  items={[{"label":"Produce — fire-and-forget","dekaf":0.75,"confluent":1.49,"dekafDisplay":"0.75 μs/msg (2.0× less)","confluentDisplay":"1.49 μs/msg"},{"label":"Produce — fire-and-forget (3 brokers)","dekaf":0.9,"confluent":1.55,"dekafDisplay":"0.90 μs/msg (1.7× less)","confluentDisplay":"1.55 μs/msg"},{"label":"Produce — acks=all","dekaf":0.71,"confluent":1.31,"dekafDisplay":"0.71 μs/msg (1.8× less)","confluentDisplay":"1.31 μs/msg"},{"label":"Produce — acks=all (3 brokers)","dekaf":0.92,"confluent":1.8,"dekafDisplay":"0.92 μs/msg (2.0× less)","confluentDisplay":"1.80 μs/msg"},{"label":"Produce — fire-and-forget, idempotent","dekaf":0.71,"confluent":1.48,"dekafDisplay":"0.71 μs/msg (2.1× less)","confluentDisplay":"1.48 μs/msg"},{"label":"Produce — fire-and-forget, idempotent (3 brokers)","dekaf":0.88,"confluent":1.83,"dekafDisplay":"0.88 μs/msg (2.1× less)","confluentDisplay":"1.83 μs/msg"},{"label":"Produce + consume round-trip","dekaf":0.91,"confluent":2.34,"dekafDisplay":"0.91 μs/msg (2.6× less)","confluentDisplay":"2.34 μs/msg"},{"label":"Produce — transactional (exactly-once) (3 brokers)","dekaf":225.52,"confluent":292.34,"dekafDisplay":"225.52 μs/msg (1.3× less)","confluentDisplay":"292.34 μs/msg"},{"label":"Consume — messages","dekaf":0.8,"confluent":1.13,"dekafDisplay":"0.80 μs/msg (1.4× less)","confluentDisplay":"1.13 μs/msg"}]}
/>

</ComparisonChartGrid>

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,451,115 msg/s | 1,174,152 msg/s | 1.2× faster | 2.0× less |
| Produce — fire-and-forget (3 brokers) | 1,313,915 msg/s | 968,735 msg/s | 1.4× faster | 1.7× less |
| Produce — acks=all | 1,552,262 msg/s | 1,373,448 msg/s | 1.1× faster | 1.9× less |
| Produce — acks=all (3 brokers) | 1,091,599 msg/s | 854,849 msg/s | 1.3× faster | 2.0× less |
| Produce — fire-and-forget, idempotent | 1,553,605 msg/s | 1,136,261 msg/s | 1.4× faster | 2.1× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 1,135,456 msg/s | 857,910 msg/s | 1.3× faster | 2.1× less |
| Produce + consume round-trip | 2,254,179 msg/s | 1,211,189 msg/s | 1.9× faster | 2.6× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,259 msg/s | 172 msg/s | 7.3× faster | 1.3× less |
| Consume — messages | 1,643,250 msg/s | 1,088,637 msg/s | 1.5× faster | 1.4× less |
| Consume — batches | 1,879,894 msg/s | — | — | — |
| Consume — raw bytes | 3,770,695 msg/s | — | — | — |
| Consume — raw byte batches | 4,019,713 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,451,115 | 1,429,421–1,473,139 | 0.75 | 1.24x |
| Confluent | 2 | 1,174,152 | 1,120,131–1,230,778 | 1.49 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.71 | 699.13 | 2,006,776 | 2,035,165 | -8.8% | -1.04% | 1913.81 | 2,006,776 | 0 | 1.43 |
| Dekaf (dekaf-first) | 0.76 | 778.08 | 1,420,267 | 1,473,139 | -4.6% | -0.47% | 1354.47 | 1,420,267 | 0 | 1.08 |
| Dekaf (confluent-first) | 0.74 | 741.89 | 1,419,349 | 1,429,421 | +2.8% | +0.17% | 1353.60 | 1,419,349 | 0 | 1.04 |
| Confluent (dekaf-first) | 1.38 | - | 1,203,690 | 1,230,778 | +3.6% | +0.32% | 1147.93 | 1,203,690 | 0 | 1.67 |
| Confluent (confluent-first) | 1.60 | - | 1,049,552 | 1,120,131 | -3.4% | +0.10% | 1000.93 | 1,049,552 | 0 | 1.68 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.90 | 873.25 | 1,450,356 | 1,450,082 | +5.4% | +0.46% | 1383.17 | 1,450,356 | 0 | 1.30 |
| Dekaf | 0.90 | 881.78 | 1,310,142 | 1,313,915 | +1.5% | +0.19% | 1249.45 | 1,310,142 | 0 | 1.18 |
| Confluent | 1.55 | - | 970,698 | 968,735 | -1.0% | -0.08% | 925.73 | 970,698 | 0 | 1.51 |

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
| Dekaf | 2 | 1,552,262 | 1,529,237–1,575,633 | 0.71 | 1.13x |
| Confluent | 2 | 1,373,448 | 1,357,596–1,389,485 | 1.31 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.70 | 722.95 | 1,564,846 | 1,575,633 | +0.7% | +0.09% | 1492.35 | 1,564,846 | 0 | 1.10 |
| Dekaf (confluent-first) | 0.71 | 726.47 | 1,523,023 | 1,529,237 | -0.1% | -0.02% | 1452.47 | 1,523,023 | 0 | 1.08 |
| Confluent (confluent-first) | 1.30 | - | 1,380,375 | 1,389,485 | +1.2% | +0.14% | 1316.43 | 1,380,375 | 0 | 1.79 |
| Confluent (dekaf-first) | 1.31 | - | 1,347,737 | 1,357,596 | +0.3% | +0.02% | 1285.30 | 1,347,737 | 0 | 1.77 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.92 | 924.20 | 1,080,644 | 1,091,599 | +1.6% | +0.17% | 1030.58 | 1,080,644 | 0 | 0.99 |
| Confluent | 1.80 | - | 854,078 | 854,849 | -1.3% | -0.10% | 814.51 | 854,078 | 0 | 1.53 |

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
| Dekaf | 2 | 1,553,605 | 1,540,681–1,566,637 | 0.71 | 1.37x |
| Confluent | 2 | 1,136,261 | 1,131,622–1,140,919 | 1.48 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.59 | 590.78 | 2,573,098 | 2,588,711 | -13.8% | -1.19% | 2453.90 | 2,573,098 | 0 | 1.52 |
| Dekaf (dekaf-first) | 0.72 | 735.57 | 1,555,089 | 1,566,637 | +4.6% | +0.54% | 1483.05 | 1,555,089 | 0 | 1.12 |
| Dekaf (confluent-first) | 0.70 | 706.82 | 1,536,476 | 1,540,681 | +7.1% | +0.70% | 1465.30 | 1,536,476 | 0 | 1.07 |
| Confluent (confluent-first) | 1.50 | - | 1,142,388 | 1,140,919 | -1.7% | +0.18% | 1089.47 | 1,142,388 | 0 | 1.72 |
| Confluent (dekaf-first) | 1.46 | - | 1,145,544 | 1,131,622 | -5.3% | -0.35% | 1092.48 | 1,145,544 | 0 | 1.67 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.88 | 888.20 | 1,233,762 | 1,245,447 | +1.7% | +0.18% | 1176.61 | 1,233,762 | 0 | 1.08 |
| Dekaf | 0.88 | 886.84 | 1,120,890 | 1,135,456 | +2.7% | +0.27% | 1068.96 | 1,120,890 | 0 | 0.99 |
| Confluent | 1.83 | - | 855,995 | 857,910 | +0.1% | +0.04% | 816.34 | 855,995 | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.91 | 3880.22 | 1,390,720 | 2,254,179 | +52.0% | +438.26% | 169.77 | 1,390,720 | 0 | 1.27 |
| Confluent | 2.34 | - | 132,590 | 1,211,189 | -4.8% | -17.67% | 16.19 | 132,590 | 0 | 0.31 |

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
| Dekaf | 225.52 | 225.52 | 923 | 1,259 | +2.3% | +0.25% | 0.88 | 1,230 | 0 | 0.28 |
| Confluent | 292.34 | - | 129 | 172 | -0.5% | -0.03% | 0.12 | 172 | 0 | 0.05 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 154,800 | 116,100 | 38,700 | 116,100 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 1,107,400 | 830,600 | 276,800 | 830,600 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.80 | - | 1,646,257 | 1,643,250 | +6.7% | +0.58% | 1569.99 | - | 0 | 1.32 |
| Confluent | 1.13 | - | 1,045,162 | 1,088,637 | -0.1% | +0.01% | 996.74 | - | 0 | 1.18 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.73 | - | 1,884,498 | 1,879,894 | -3.8% | -0.42% | 1797.20 | - | 0 | 1.37 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.42 | - | 3,761,535 | 3,770,695 | -1.0% | -0.07% | 3587.28 | - | 0 | 1.57 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.38 | - | 4,078,621 | 4,019,713 | +4.2% | +0.47% | 3889.68 | - | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 55737 | 333 | 0 | 2137.56 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 215766 | 1 | 1 | 1133.57 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 266331 | 4 | 1 | 1300.12 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 221878 | 1 | 1 | 1048.51 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 278392 | 1 | 1 | 1490.89 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 301866 | 16 | 1 | 1455.64 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 196350 | 1 | 1 | 922.41 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 227417 | 1 | 1 | 1233.77 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 257009 | 20 | 1 | 1237.30 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 196940 | 1 | 1 | 924.60 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 6643 | 4 | 4 | 17.56 GB | 953 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 110 | 1 | 1 | 81.46 MB | 552 B |
| Dekaf | Consumer | 73468 | 4 | 2 | 2793.34 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 28131 | 4 | 2 | 3198.02 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 6 | 1 | 1 | 509.84 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 9 | 3 | 2 | 974.30 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 222 | 3 | 2 | 95.49 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 214 | 3 | 2 | 791.68 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 163 | 3 | 2 | 696.92 MB | 1 B |
| Dekaf | Producer (Acks All) | 217 | 3 | 1 | 152.02 MB | 0 B |
| Dekaf | Producer (Acks All) | 255 | 3 | 1 | 936.33 MB | 1 B |
| Dekaf | Producer (Acks All), 3 Brokers | 137 | 3 | 2 | 672.34 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 217 | 3 | 2 | 169.81 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 211 | 3 | 2 | 810.13 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 140 | 3 | 2 | 604.63 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 599 | 2 | 0 | 2.81 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 85 | 2 | 1 | 329.09 MB | 312 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 227 | 4 | 2 | 1.02 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 173 | 3 | 2 | 784.46 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 390 | 6 | 1 | 1.25 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 139 | 5 | 4 | 570.00 MB | 1 B |

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
