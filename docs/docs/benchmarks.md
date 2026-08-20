---
sidebar_position: 13
description: "Dekaf versus Confluent.Kafka on throughput, latency, and allocations, measured with BenchmarkDotNet in automated runs from main."
---

import ComparisonChart, {ComparisonChartGrid} from '@site/src/components/ComparisonChart';

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet in daily or on-demand GitHub Actions runs from main.

**Last Updated:** 2026-08-16 14:42 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 benchmark runs (both clients measured on the same runner), aggregated across its parameter configurations. Memory compares heap allocations per operation from the latest run.

The charts use the representative high-load parameter set from the latest run and show its measured values. Multipliers in brackets are calculated from those displayed figures; the table below summarizes the broader cross-run range.

:::note Reading producer results
The `linger=0` scenario is the matched client comparison. The `linger=5 ms` scenario intentionally measures each client's app-limited batching policy and should not be read as general producer throughput. In the legacy serial-awaited results below, Dekaf sends a sole record immediately while Confluent applies the configured linger; the old benchmark's unused `BatchSize` parameter also duplicated each payload result. In the legacy batch and fire-and-forget results, Dekaf used `acks=all` with idempotence while Confluent used `acks=leader` without idempotence. The page will show the new matched controls after the next run from main.
:::

<ComparisonChartGrid>

<ComparisonChart
  title="Execution time"
  metric="Latest run · representative high-load case"
  description="Measured time per benchmark operation; shorter bars are better."
  better="lower"
  items={[{"label":"Produce — serial awaited (linger=5 ms; legacy)","note":"1000 B · app-limited","dekaf":116.51,"confluent":5277.37,"dekafDisplay":"116.51 μs (45× faster)","confluentDisplay":"5.28 ms"},{"label":"Produce — batches (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent)","note":"1000 B · batch 1000","dekaf":9488.78,"confluent":8016.78,"dekafDisplay":"9.49 ms (1.2× slower)","confluentDisplay":"8.02 ms"},{"label":"Produce — fire-and-forget (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent)","note":"1000 B · batch 1000","dekaf":7098.98,"confluent":5321.78,"dekafDisplay":"7.10 ms (1.3× slower)","confluentDisplay":"5.32 ms"},{"label":"Consume — drain a topic","note":"1000 B · 1000 messages","dekaf":905.32,"confluent":683.96,"dekafDisplay":"905.32 μs (1.3× slower)","confluentDisplay":"683.96 μs"},{"label":"Consume — poll a single message","note":"1000 B","dekaf":0.6131,"confluent":1.5363,"dekafDisplay":"613.1 ns (2.5× faster)","confluentDisplay":"1.54 μs"}]}
/>

<ComparisonChart
  title="Managed allocations"
  metric="Latest run · bytes per operation"
  description="Managed heap bytes allocated per operation; shorter bars are better."
  better="lower"
  items={[{"label":"Produce — serial awaited (linger=5 ms; legacy)","note":"1000 B · app-limited","dekaf":456,"confluent":2113,"dekafDisplay":"456 B (4.6× less)","confluentDisplay":"2.06 KB"},{"label":"Produce — batches (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent)","note":"1000 B · batch 1000","dekaf":51720,"confluent":1944394,"dekafDisplay":"50.51 KB (38× less)","confluentDisplay":"1.85 MB"},{"label":"Produce — fire-and-forget (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent)","note":"1000 B · batch 1000","dekaf":1170,"confluent":1206138,"dekafDisplay":"1.14 KB (1031× less)","confluentDisplay":"1.15 MB"},{"label":"Consume — drain a topic","note":"1000 B · 1000 messages","dekaf":2064691.2,"confluent":2464153.6,"dekafDisplay":"1.97 MB (1.2× less)","confluentDisplay":"2.35 MB"},{"label":"Consume — poll a single message","note":"1000 B","dekaf":2074,"confluent":2454,"dekafDisplay":"2.03 KB (1.2× less)","confluentDisplay":"2.40 KB"}]}
/>

</ComparisonChartGrid>

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — serial awaited (linger=5 ms; legacy) | 21×–22× faster | 3.3× less | ⚠ Noisy |
| Produce — batches (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent) | on par to 2.4× faster | 25× less | Mixed |
| Produce — fire-and-forget (legacy: Dekaf acks=all/idempotent; Confluent acks=leader/non-idempotent) | on par | 1000× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.2× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–9.8× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.14 | 1.01–1.29 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.30 | 1.09–1.51 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.65–0.97 | 39% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.79 | 1.17–2.40 | 69% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.05–0.11 | 56% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.26–0.70 | 160% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.93 | 0.81–1.48 | 72% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.98 | 0.89–1.47 | 59% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.86 | 0.74–0.98 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.83 | 0.75–1.31 | 67% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 6% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.53 | 31% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.09 | 1.01–1.28 | 25% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.02–0.06 | 82% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 81% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.02–0.06 | 83% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 81% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,481.38 μs** |    **65.240 μs** |    **43.152 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 2,344.27 μs |    12.635 μs |     7.519 μs |  0.43 |    0.00 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,384.55 μs** |    **83.715 μs** |    **49.817 μs** |  **1.00** |    **0.01** |  **62.5000** | **23.4375** | **1048384 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,224.80 μs |   120.425 μs |    79.654 μs |  0.51 |    0.01 |        - |       - |   49812 B |        0.05 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **5,865.54 μs** |     **6.133 μs** |     **3.208 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 2,441.00 μs |    47.392 μs |    31.347 μs |  0.42 |    0.01 |        - |       - |    6307 B |        0.03 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,016.78 μs** | **2,456.682 μs** | **1,461.932 μs** |  **1.03** |    **0.23** | **109.3750** | **46.8750** | **1944394 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 9,488.78 μs |   169.661 μs |   100.962 μs |  1.21 |    0.18 |        - |       - |   51720 B |        0.03 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **58.71 μs** |     **2.676 μs** |     **1.770 μs** |  **1.00** |    **0.04** |   **1.7700** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    85.42 μs |    20.084 μs |    13.284 μs |  1.46 |    0.22 |        - |       - |      24 B |       0.001 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **570.86 μs** |    **33.310 μs** |    **22.033 μs** |  **1.00** |    **0.05** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   777.48 μs |   280.071 μs |   185.250 μs |  1.36 |    0.31 |        - |       - |     298 B |       0.001 | ⚠ Low |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **552.50 μs** |   **117.556 μs** |    **61.484 μs** |  **1.01** |    **0.15** |   **7.2021** |       **-** |  **120896 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   523.16 μs |   272.069 μs |   161.904 μs |  0.96 |    0.30 |        - |       - |     237 B |       0.002 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **5,321.78 μs** |   **647.157 μs** |   **428.054 μs** |  **1.01** |    **0.11** |  **70.3125** |       **-** | **1206138 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 7,098.98 μs | 2,794.574 μs | 1,663.006 μs |  1.34 |    0.32 |        - |       - |    1170 B |       0.001 | ⚠ Low |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,271.37 μs** |     **8.384 μs** |     **4.385 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |   116.84 μs |     4.220 μs |     2.791 μs |  0.02 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,290.59 μs** |    **52.814 μs** |    **27.623 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |   115.97 μs |     1.139 μs |     0.753 μs |  0.02 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,276.22 μs** |     **8.543 μs** |     **5.651 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2113 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |   118.39 μs |     2.593 μs |     1.543 μs |  0.02 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |             |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,277.37 μs** |     **5.620 μs** |     **3.344 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2113 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |   116.51 μs |     1.887 μs |     1.248 μs |  0.02 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean      | Error      | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |----------:|-----------:|-----------:|------------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |  **70.61 μs** |  **29.938 μs** |  **15.658 μs** |    **62.65 μs** |  **1.04** |    **0.28** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |  63.39 μs |   2.645 μs |   1.384 μs |    63.77 μs |  0.93 |    0.16 |  26.45 KB |        0.41 | Stable |
|                      |              |             |           |            |            |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |  **68.54 μs** |   **3.923 μs** |   **1.399 μs** |    **68.52 μs** |  **1.00** |    **0.03** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |  79.49 μs |   3.115 μs |   1.383 μs |    79.28 μs |  1.16 |    0.03 | 202.23 KB |        0.84 | Stable |
|                      |              |             |           |            |            |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **609.14 μs** | **316.950 μs** | **165.771 μs** |   **563.88 μs** |  **1.06** |    **0.36** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 378.15 μs |  63.777 μs |  28.317 μs |   367.30 μs |  0.66 |    0.15 | 258.48 KB |        0.40 | Stable |
|                      |              |             |           |            |            |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **683.96 μs** | **385.912 μs** | **171.347 μs** |   **577.48 μs** |  **1.05** |    **0.33** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 905.32 μs | 954.022 μs | 423.592 μs | 1,199.00 μs |  1.39 |    0.68 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,319.8 ns** |    **15.39 ns** |     **8.05 ns** | **5,317.6 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   279.5 ns |    25.09 ns |    13.12 ns |   274.6 ns |  0.05 |    0.00 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **1,536.3 ns** | **1,709.02 ns** | **1,130.41 ns** |   **794.4 ns** |  **1.46** |    **1.32** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   613.1 ns |   205.59 ns |   135.98 ns |   552.5 ns |  0.58 |    0.30 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 580.13 ns | 13.871 ns | 3.602 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.57 ns |  0.253 ns | 0.039 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.52 ns |  0.245 ns | 0.038 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.78 ns |  0.138 ns | 0.036 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.004 μs** | **0.0229 μs** | **0.0035 μs** |         **-** |
| **WriteRequest** | **1**       | **2.002 μs** | **0.0019 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.395 μs** | **0.0058 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **9**       | **2.604 μs** | **0.0548 μs** | **0.0085 μs** |         **-** |
| **WriteRequest** | **10**      | **2.413 μs** | **0.0117 μs** | **0.0030 μs** |         **-** |
| **WriteRequest** | **11**      | **2.411 μs** | **0.0185 μs** | **0.0048 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.10 ns** | **0.465 ns** | **0.072 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 100.43 ns | 0.586 ns | 0.152 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.36 ns** | **0.903 ns** | **0.140 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.07 ns | 0.505 ns | 0.078 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,640.0 ns |  2.93 ns | 1.75 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,059.3 ns |  2.87 ns | 1.71 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,310.4 ns |  4.74 ns | 2.82 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,284.1 ns |  3.77 ns | 2.24 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,028.6 ns |  2.50 ns | 1.49 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,891.2 ns | 10.47 ns | 6.23 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,903.7 ns |  2.65 ns | 1.39 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,735.5 ns |  6.56 ns | 4.34 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,116.7 ns |  0.98 ns | 0.58 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,814.0 ns |  2.47 ns | 1.47 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   774.0 ns |  1.58 ns | 1.05 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   837.2 ns |  8.38 ns | 4.99 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   144.2 ns |  0.48 ns | 0.25 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,734.8 ns | 11.83 ns | 7.82 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,295.9 ns |  5.84 ns | 3.48 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   3.646 ns | 0.0066 ns | 0.0058 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 234.849 ns | 0.1540 ns | 0.1366 ns | 64.41 |    0.11 |         - |          NA |

| Method          | Mean     | Error    | StdDev   | Allocated |
|---------------- |---------:|---------:|---------:|----------:|
| SerializeCached | 41.77 ns | 1.091 ns | 0.060 ns |         - |

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,386.595 ns | 3.7648 ns | 1.9691 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    10.182 ns | 0.0209 ns | 0.0124 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    12.055 ns | 0.0091 ns | 0.0060 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    26.905 ns | 0.2238 ns | 0.1332 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    26.912 ns | 0.4969 ns | 0.3287 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     7.142 ns | 0.0227 ns | 0.0135 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    91.927 ns | 1.7350 ns | 1.1476 ns |  1.00 |    0.02 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    50.440 ns | 0.5176 ns | 0.3080 ns |  0.55 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     287.1 ns |   3.39 ns |  2.24 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,120.8 ns | 103.34 ns | 61.49 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     221.0 ns |   0.79 ns |  0.52 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,700.7 ns |  95.01 ns | 56.54 ns |      - |      80 B |

</details>

<details>
<summary>How to read these tables</summary>

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Dekaf-internals tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **RatioSD**: BenchmarkDotNet's uncertainty for the latest run's ratio
- **Confidence**: `⚠ Low` when latest `RatioSD > 0.30` or rolling run spread exceeds 30%
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

</details>

*Benchmarks automatically run daily at 05:00 UTC from main and can also be run manually.*
