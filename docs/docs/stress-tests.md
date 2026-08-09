---
sidebar_position: 14
---

# Stress Test Results

Long-running stress tests comparing sustained performance between Dekaf and Confluent.Kafka under real-world load.

**Last Updated:** 2026-08-09 04:00 UTC

:::info
The paired Dekaf vs Confluent comparison runs weekly (Sunday 2 AM UTC) and updates this page. 
Manual dispatches stay Dekaf-only unless full_run explicitly requests the same paired publish path. 
Tests measure sustained performance over 15+ minutes with real Kafka instances.
:::

## At a glance

Each row is a like-for-like comparison: both clients run the same sustained workload sequentially on the same VM, and repeated samples are aggregated with a geometric mean across both run orders.

| Scenario | Dekaf | Confluent | Throughput | CPU per message |
|---|--:|--:|---|---|
| Produce — fire-and-forget | 1,385,796 msg/s | 1,033,061 msg/s | 1.3× faster | 1.6× less |
| Produce — fire-and-forget (3 brokers) | 1,118,512 msg/s | 850,210 msg/s | 1.3× faster | 1.5× less |
| Produce — acks=all | 1,578,023 msg/s | 1,255,021 msg/s | 1.3× faster | 1.6× less |
| Produce — acks=all (3 brokers) | 1,113,542 msg/s | 809,809 msg/s | 1.4× faster | 1.6× less |
| Produce — fire-and-forget, idempotent | 1,333,660 msg/s | 1,143,952 msg/s | 1.2× faster | 1.4× less |
| Produce — fire-and-forget, idempotent (3 brokers) | 956,505 msg/s | 584,025 msg/s | 1.6× faster | 2.0× less |
| Produce + consume round-trip | 2,370,956 msg/s | 1,220,905 msg/s | 1.9× faster | 2.1× less |
| Produce — transactional (exactly-once) (3 brokers) | 1,247 msg/s | 170 msg/s | 7.3× faster | 1.1× less |
| Consume — messages | 1,706,562 msg/s | 1,311,755 msg/s | 1.3× faster | 1.4× less |
| Consume — batches | 2,114,850 msg/s | — | — | — |
| Consume — raw bytes | 3,759,083 msg/s | — | — | — |
| Consume — raw byte batches | 4,000,063 msg/s | — | — | — |

*"On par" means within ±5% — differences that small are run-to-run noise. "CPU per message" compares the client CPU cost of delivering one message; "less" means Dekaf needs less CPU. Rows showing "—" have no Confluent counterpart in this run (for example, batch and raw consume APIs that librdkafka does not expose). The full per-run data is below.*

## Full results

Each section holds the measured per-run data behind the summary: repeated same-VM samples in both client orders, CPU per message and per request, and throughput drift across the run.

<details>
<summary>Producer (Fire-and-Forget) (15 minutes, 1000B messages)</summary>

**Order-Balanced Aggregate**

| Client | Samples | Geomean comparison msg/s | Sample range | Median CPU μs/msg | Comparison Ratio |
|--------|--------:|--------------------------:|--------------|------------------:|-----------------:|
| Dekaf | 2 | 1,385,796 | 1,363,290–1,408,673 | 1.05 | 1.34x |
| Confluent | 2 | 1,033,061 | 1,013,625–1,052,869 | 1.67 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.80 | 760.13 | 1,546,890 | 1,554,185 | -13.1% | -1.41% | 1475.23 | 1,546,890 | 0 | 1.24 |
| Dekaf (dekaf-first) | 1.06 | 1086.80 | 1,398,441 | 1,408,673 | +1.2% | +0.04% | 1333.66 | 1,398,441 | 0 | 1.48 |
| Dekaf (confluent-first) | 1.05 | 1075.49 | 1,354,630 | 1,363,290 | -1.1% | -0.07% | 1291.88 | 1,354,630 | 0 | 1.42 |
| Confluent (dekaf-first) | 1.60 | - | 1,063,654 | 1,052,869 | +5.2% | +0.37% | 1014.38 | 1,063,654 | 0 | 1.70 |
| Confluent (confluent-first) | 1.74 | - | 1,000,313 | 1,013,625 | -6.7% | -0.49% | 953.97 | 1,000,313 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.03 | 966.15 | 1,222,709 | 1,220,569 | -1.5% | -0.25% | 1166.07 | 1,222,709 | 0 | 1.26 |
| Dekaf | 1.17 | 1069.40 | 1,072,060 | 1,118,512 | +36.5% | +3.31% | 1022.40 | 1,072,060 | 0 | 1.26 |
| Confluent | 1.81 | - | 839,748 | 850,210 | +1.8% | +0.15% | 800.85 | 839,748 | 0 | 1.52 |

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
| Dekaf | 2 | 1,578,023 | 1,554,725–1,601,671 | 0.91 | 1.26x |
| Confluent | 2 | 1,255,021 | 1,201,262–1,311,186 | 1.42 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (dekaf-first) | 0.89 | 912.88 | 1,584,209 | 1,601,671 | +1.1% | +0.14% | 1510.82 | 1,584,209 | 0 | 1.41 |
| Dekaf (confluent-first) | 0.93 | 955.40 | 1,532,540 | 1,554,725 | -2.5% | -0.21% | 1461.54 | 1,532,540 | 0 | 1.43 |
| Confluent (confluent-first) | 1.38 | - | 1,292,813 | 1,311,186 | +4.9% | +0.42% | 1232.92 | 1,292,813 | 0 | 1.78 |
| Confluent (dekaf-first) | 1.45 | - | 1,193,669 | 1,201,262 | -15.8% | -1.46% | 1138.37 | 1,193,669 | 0 | 1.73 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Acks All), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 1.17 | 1184.17 | 1,091,246 | 1,113,542 | +0.1% | +0.14% | 1040.69 | 1,091,246 | 0 | 1.27 |
| Confluent | 1.92 | - | 800,772 | 809,809 | -11.2% | -1.02% | 763.68 | 800,772 | 0 | 1.54 |

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
| Dekaf | 2 | 1,333,660 | 1,302,697–1,365,360 | 1.12 | 1.17x |
| Confluent | 2 | 1,143,952 | 1,130,505–1,157,559 | 1.59 | 1.00x |

*The aggregate uses the geometric mean across balanced same-VM samples run in both `dekaf-first` and `confluent-first` order. Raw ordered samples remain below.*

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 0.79 | 748.89 | 1,580,679 | 1,587,949 | +15.0% | +1.40% | 1507.45 | 1,580,679 | 0 | 1.26 |
| Dekaf (confluent-first) | 1.09 | 1113.02 | 1,358,176 | 1,365,360 | +16.7% | +1.44% | 1295.26 | 1,358,176 | 0 | 1.48 |
| Dekaf (dekaf-first) | 1.15 | 1176.60 | 1,307,778 | 1,302,697 | -2.4% | -0.14% | 1247.19 | 1,307,778 | 0 | 1.51 |
| Confluent (confluent-first) | 1.57 | - | 1,097,546 | 1,157,559 | -13.5% | -1.16% | 1046.70 | 1,097,546 | 0 | 1.73 |
| Confluent (dekaf-first) | 1.60 | - | 1,084,231 | 1,130,505 | -22.8% | -2.18% | 1034.00 | 1,084,231 | 0 | 1.74 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer (Fire-and-Forget, Idempotent), 3 Brokers (15 minutes, 1000B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf (3conn) | 1.25 | 1235.53 | 1,039,228 | 1,027,230 | +22.0% | +2.28% | 991.08 | 1,039,228 | 0 | 1.30 |
| Dekaf | 1.37 | 1374.98 | 945,451 | 956,505 | -4.9% | -0.46% | 901.65 | 945,451 | 0 | 1.30 |
| Confluent | 2.74 | - | 609,503 | 584,025 | +16.0% | +1.29% | 581.27 | 609,503 | 0 | 1.67 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

<details>
<summary>Producer → Consumer Round-Trip Steady State (15 minutes, 128B messages)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.99 | 4228.99 | 1,321,288 | 2,370,956 | -7.9% | -92.44% | 161.29 | 1,321,288 | 0 | 1.31 |
| Confluent | 2.08 | - | 125,935 | 1,220,905 | +16.2% | +113.03% | 15.37 | 125,935 | 0 | 0.26 |

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
| Dekaf | 230.64 | 230.64 | 923 | 1,247 | +5.8% | +0.57% | 0.88 | 1,231 | 0 | 0.28 |
| Confluent | 252.56 | - | 127 | 170 | -0.7% | -0.02% | 0.12 | 170 | 0 | 0.04 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

*Messages/sec counts broker-confirmed deliveries (end-offset delta). Accepted msg/s is the client-side append rate — a large gap means messages were buffered or dropped without ever reaching the broker.*

</details>

### Transaction Verification

| Client | Accepted | Committed | Aborted | Delivered | Duplicates | Shortfall | Aborted leaks | Unexpected | Missing sentinels | Status |
|--------|----------|-----------|---------|-----------|------------|-----------|---------------|------------|-------------------|--------|
| Confluent | 152,800 | 114,600 | 38,200 | 114,600 | 0 | 0 | 0 | 0 | 0 | PASS |
| Dekaf | 1,107,800 | 830,900 | 276,900 | 830,900 | 0 | 0 | 0 | 0 | 0 | PASS |

<details>
<summary>Consumer (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.78 | - | 1,701,361 | 1,706,562 | +0.2% | +0.05% | 1622.54 | - | 0 | 1.32 |
| Confluent | 1.12 | - | 1,234,669 | 1,311,755 | +3.4% | +0.24% | 1177.47 | - | 0 | 1.38 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.69 | - | 2,102,433 | 2,114,850 | -0.2% | +0.01% | 2005.04 | - | 0 | 1.46 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Bytes) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.42 | - | 3,744,525 | 3,759,083 | -0.5% | -0.04% | 3571.06 | - | 0 | 1.55 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Consumer (Raw Batch) (15 minutes, 1000B messages, 16,384B seed batches)</summary>

| Client | CPU μs/msg | CPU μs/request | Messages/sec | Median msg/s | Drift | Slope %/min | MB/sec | Accepted msg/s | Errors | Standing cores |
|--------|------------|----------------|--------------|--------------|-------|-------------|--------|----------------|--------|----------------|
| Dekaf | 0.37 | - | 4,026,435 | 4,000,063 | +4.5% | +0.41% | 3839.91 | - | 0 | 1.50 |

*Median msg/s is the median sampled client-side throughput interval; it shows steady-state throughput without letting a short late-run stall dominate the whole-run average.*

*Rows and Comparison Ratio use Median msg/s when available; older result files without interval samples fall back to Messages/sec.*

*Drift compares last-third with first-third average throughput. Slope is the normalized least-squares trend; steady-state below 85% of peak or slope below -1%/min fails the regression gate.*

</details>

<details>
<summary>Memory & GC statistics — latest run</summary>

| Client | Scenario | Gen0 | Gen1 | Gen2 | Total Allocated | Alloc/msg |
|--------|----------|------|------|------|-----------------|-----------|
| Confluent | Consumer | 65846 | 389 | 0 | 2525.14 GB | 2.38 KB |
| Confluent | Producer (Fire-and-Forget) | 169999 | 1 | 1 | 1080.39 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget) | 234872 | 1 | 1 | 1148.86 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget), 3 Brokers | 191485 | 8 | 1 | 907.11 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 270207 | 34 | 1 | 1289.27 GB | 1.26 KB |
| Confluent | Producer (Acks All) | 265606 | 1 | 1 | 1396.28 GB | 1.26 KB |
| Confluent | Producer (Acks All), 3 Brokers | 183489 | 13 | 1 | 864.96 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 243908 | 15 | 1 | 1171.03 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent) | 241867 | 1 | 1 | 1185.39 GB | 1.26 KB |
| Confluent | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 137664 | 1 | 1 | 658.36 GB | 1.26 KB |
| Confluent | Producer → Consumer Round-Trip Steady State | 5282 | 12 | 9 | 15.02 GB | 815 B |
| Confluent | Producer (Transactional EOS), 3 Brokers | 97 | 1 | 1 | 112.76 MB | 774 B |
| Dekaf | Consumer | 75916 | 62 | 1 | 2886.88 GB | 1.98 KB |
| Dekaf | Consumer (Batch) | 31372 | 4 | 2 | 3567.77 GB | 1.98 KB |
| Dekaf | Consumer (Raw Bytes) | 5 | 2 | 1 | 506.80 MB | 0 B |
| Dekaf | Consumer (Raw Batch) | 14 | 1 | 0 | 980.10 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 366 | 2 | 2 | 144.64 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget) | 403 | 2 | 2 | 1.55 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget), 3 Brokers | 193 | 4 | 3 | 785.89 MB | 1 B |
| Dekaf | Producer (Acks All) | 428 | 3 | 2 | 1.62 GB | 1 B |
| Dekaf | Producer (Acks All) | 430 | 2 | 2 | 151.26 MB | 0 B |
| Dekaf | Producer (Acks All), 3 Brokers | 216 | 3 | 2 | 922.83 MB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 335 | 1 | 1 | 1.27 GB | 1 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent) | 353 | 1 | 1 | 189.31 MB | 0 B |
| Dekaf | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 167 | 2 | 2 | 734.36 MB | 1 B |
| Dekaf | Producer → Consumer Round-Trip Steady State | 601 | 3 | 1 | 2.82 GB | 153 B |
| Dekaf | Producer (Transactional EOS), 3 Brokers | 126 | 2 | 1 | 480.40 MB | 455 B |
| Dekaf (3conn) | Producer (Fire-and-Forget) | 321 | 2 | 2 | 1.21 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget), 3 Brokers | 251 | 2 | 1 | 1004.70 MB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent) | 304 | 1 | 1 | 1.27 GB | 1 B |
| Dekaf (3conn) | Producer (Fire-and-Forget, Idempotent), 3 Brokers | 203 | 3 | 2 | 833.85 MB | 1 B |

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
