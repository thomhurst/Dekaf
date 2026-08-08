---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-08 19:06 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 19× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 67× less | ⚠ Noisy |
| Consume — drain a topic | 1.5× slower to 1.2× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.6×–10× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.09 | 0.94–1.46 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.20 | 1.00–1.37 | 30% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.83 | 0.70–1.08 | 45% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.48 | 0.80–2.21 | 95% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 46% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.16–0.32 | 58% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.94 | 0.77–1.26 | 52% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.00 | 0.73–1.11 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.85 | 0.76–1.08 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.83 | 0.72–1.24 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.44 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 0.99–1.51 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 52% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 42% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 54% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 50% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,160.0 μs** |   **108.79 μs** |  **64.74 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,685.9 μs |    15.69 μs |   9.34 μs |  0.44 |    0.00 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,597.0 μs** |    **69.06 μs** |  **45.68 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,717.4 μs |    94.21 μs |  62.31 μs |  0.49 |    0.01 |        - |       - |   51900 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,564.0 μs** |    **55.23 μs** |  **32.87 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,776.7 μs |    73.58 μs |  48.67 μs |  0.42 |    0.01 |        - |       - |    7818 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,517.1 μs** |   **301.54 μs** | **199.45 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,916.2 μs | 1,226.87 μs | 730.09 μs |  1.03 |    0.06 |        - |       - |   70393 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.1 μs** |     **2.52 μs** |   **1.67 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    121.9 μs |     5.45 μs |   3.24 μs |  1.01 |    0.03 |        - |       - |     506 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,234.5 μs** |    **11.24 μs** |   **6.69 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,358.7 μs |   435.38 μs | 287.97 μs |  1.10 |    0.22 |        - |       - |    1661 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **964.5 μs** |    **11.20 μs** |   **5.86 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121411 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    795.0 μs |    91.53 μs |  60.54 μs |  0.82 |    0.06 |        - |       - |    1787 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,657.4 μs** |   **163.67 μs** |  **85.60 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1213893 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,999.2 μs | 1,318.37 μs | 872.02 μs |  0.83 |    0.09 |        - |       - |   20784 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,508.0 μs** |    **26.98 μs** |  **17.84 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    300.3 μs |     8.73 μs |   5.78 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,472.3 μs** |    **15.24 μs** |   **7.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    302.0 μs |     9.60 μs |   6.35 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,479.3 μs** |     **9.59 μs** |   **6.34 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    304.2 μs |     8.38 μs |   5.54 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,482.1 μs** |     **9.28 μs** |   **5.52 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    301.2 μs |    10.54 μs |   6.97 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **129.2 μs** |    **40.92 μs** |  **21.40 μs** |  **1.02** |    **0.22** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   126.7 μs |     3.08 μs |   1.61 μs |  1.00 |    0.15 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **155.9 μs** |    **51.65 μs** |  **27.01 μs** |  **1.03** |    **0.23** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   167.4 μs |    17.68 μs |   7.85 μs |  1.10 |    0.18 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,064.8 μs** |   **585.91 μs** | **306.44 μs** |  **1.06** |    **0.38** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   773.5 μs |   107.10 μs |  47.55 μs |  0.77 |    0.18 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,464.4 μs** |   **920.59 μs** | **481.48 μs** |  **1.09** |    **0.46** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,714.3 μs | 1,142.81 μs | 507.42 μs |  1.28 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,625.2 ns** |   **102.7 ns** |  **67.93 ns** |  **1.00** |    **0.02** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   553.7 ns |   113.1 ns |  74.80 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |            |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,458.5 ns** | **1,504.0 ns** | **786.63 ns** |  **1.10** |    **0.52** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,081.2 ns |   127.3 ns |  75.73 ns |  0.34 |    0.14 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 552.67 ns | 15.354 ns | 3.987 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.27 ns |  0.191 ns | 0.030 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.56 ns |  0.225 ns | 0.058 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.16 ns |  0.173 ns | 0.027 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.032 μs** | **0.0072 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **1**       | **2.014 μs** | **0.0058 μs** | **0.0009 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.649 μs** | **0.0122 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **9**       | **2.407 μs** | **0.0107 μs** | **0.0028 μs** |         **-** |
| **WriteRequest** | **10**      | **2.402 μs** | **0.0054 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **11**      | **2.398 μs** | **0.0540 μs** | **0.0140 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.35 ns** | **0.403 ns** | **0.062 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 104.24 ns | 0.561 ns | 0.087 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.61 ns** | **1.710 ns** | **0.265 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  90.99 ns | 0.312 ns | 0.048 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.5 ns |  1.28 ns | 0.67 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,135.1 ns | 11.21 ns | 7.42 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,322.7 ns |  4.27 ns | 2.54 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,254.1 ns |  6.62 ns | 3.94 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,122.5 ns |  1.84 ns | 0.96 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,986.9 ns |  7.00 ns | 4.16 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,029.2 ns | 11.63 ns | 6.92 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,842.0 ns |  7.59 ns | 4.52 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.0 ns |  1.00 ns | 0.66 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.8 ns |  7.81 ns | 4.65 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   721.7 ns |  2.89 ns | 1.91 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   827.2 ns |  1.92 ns | 1.14 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   167.0 ns |  0.33 ns | 0.19 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,737.9 ns |  7.77 ns | 4.62 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,289.8 ns |  1.59 ns | 0.95 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,161.06 ns | 91.650 ns | 60.621 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |  0.028 ns |  0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.73 ns |  0.010 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.36 ns |  0.048 ns |  0.028 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.89 ns |  0.088 ns |  0.058 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.008 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    107.59 ns |  0.497 ns |  0.329 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.36 ns |  0.093 ns |  0.055 ns |  0.51 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     288.5 ns |   1.79 ns |   0.94 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,030.9 ns |  74.74 ns |  39.09 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     220.4 ns |   0.72 ns |   0.43 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 125,545.3 ns | 265.54 ns | 158.02 ns |      - |      80 B |

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

*Benchmarks are automatically run on every push to main.*