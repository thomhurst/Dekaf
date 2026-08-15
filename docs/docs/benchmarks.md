---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-15 14:59 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 21×–22× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 1000× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.2× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–9.7× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.17 | 1.02–1.33 | 27% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.31 | 1.06–1.51 | 34% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.68–0.97 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 1.17–2.40 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 24% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.18–0.43 | 94% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.81–1.42 | 68% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.98 | 0.93–1.08 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.85 | 0.74–0.97 | 27% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.82 | 0.75–1.05 | 36% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 7% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.53 | 31% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.09 | 1.01–1.28 | 25% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 53% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 56% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 55% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 53% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,131.1 μs** |    **80.04 μs** |  **41.86 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,717.4 μs |    20.23 μs |  13.38 μs |  0.44 |    0.00 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,626.8 μs** |   **104.14 μs** |  **68.88 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,737.5 μs |    77.56 μs |  46.15 μs |  0.49 |    0.01 |        - |       - |   49809 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,217.0 μs** |    **42.80 μs** |  **25.47 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,769.9 μs |    58.60 μs |  38.76 μs |  0.45 |    0.01 |        - |       - |    6292 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,522.0 μs** |   **140.60 μs** |  **83.67 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944427 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,433.8 μs | 1,084.29 μs | 717.19 μs |  0.99 |    0.06 |        - |       - |   51234 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.5 μs** |     **2.21 μs** |   **1.32 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    104.9 μs |    10.31 μs |   6.13 μs |  0.82 |    0.05 |        - |       - |      97 B |       0.003 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,271.9 μs** |    **13.01 μs** |   **8.61 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,208.5 μs |   166.14 μs |  98.87 μs |  0.95 |    0.07 |        - |       - |     288 B |       0.001 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,083.4 μs** |     **8.03 μs** |   **4.78 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121576 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    845.9 μs |    46.12 μs |  27.44 μs |  0.78 |    0.02 |        - |       - |     174 B |       0.001 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,780.5 μs** |    **63.87 μs** |  **42.25 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215807 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,198.9 μs |   888.17 μs | 587.47 μs |  0.76 |    0.05 |        - |       - |    1409 B |       0.001 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,527.6 μs** |    **11.04 μs** |   **7.30 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    327.7 μs |    11.74 μs |   7.76 μs |  0.06 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,533.9 μs** |    **12.96 μs** |   **8.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    325.9 μs |     9.23 μs |   6.11 μs |  0.06 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,529.8 μs** |    **11.67 μs** |   **7.72 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    332.4 μs |     7.56 μs |   4.50 μs |  0.06 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,544.6 μs** |    **14.26 μs** |   **9.43 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    333.4 μs |    15.42 μs |  10.20 μs |  0.06 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **134.8 μs** |    **58.86 μs** |  **30.78 μs** |   **119.0 μs** |  **1.04** |    **0.31** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   137.1 μs |     2.80 μs |   1.24 μs |   137.7 μs |  1.06 |    0.21 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **163.4 μs** |    **78.28 μs** |  **40.94 μs** |   **140.0 μs** |  **1.05** |    **0.32** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   175.2 μs |     5.71 μs |   2.98 μs |   174.5 μs |  1.12 |    0.22 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,150.2 μs** |   **623.45 μs** | **326.07 μs** | **1,023.1 μs** |  **1.07** |    **0.40** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   774.0 μs |    72.08 μs |  32.01 μs |   783.2 μs |  0.72 |    0.18 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,405.6 μs** |   **881.19 μs** | **460.88 μs** | **1,141.0 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,649.0 μs | 1,245.07 μs | 552.82 μs | 2,034.9 μs |  1.27 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,581.6 ns** |    **17.28 ns** |   **9.04 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   576.1 ns |   145.18 ns |  96.03 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,528.8 ns** | **1,489.16 ns** | **778.86 ns** |  **1.09** |    **0.49** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,070.0 ns |   128.99 ns |  85.32 ns |  0.33 |    0.13 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 559.77 ns | 19.071 ns | 4.953 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.22 ns |  0.192 ns | 0.030 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.50 ns |  0.766 ns | 0.199 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.10 ns |  0.142 ns | 0.037 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.963 μs** | **0.0054 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **1**       | **2.018 μs** | **0.0046 μs** | **0.0007 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.508 μs** | **0.1277 μs** | **0.0332 μs** |         **-** |
| **WriteRequest** | **9**       | **2.449 μs** | **0.0222 μs** | **0.0058 μs** |         **-** |
| **WriteRequest** | **10**      | **2.462 μs** | **0.0211 μs** | **0.0055 μs** |         **-** |
| **WriteRequest** | **11**      | **2.460 μs** | **0.0173 μs** | **0.0027 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **110.99 ns** | **0.470 ns** | **0.122 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 100.75 ns | 2.015 ns | 0.312 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.15 ns** | **1.324 ns** | **0.205 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  91.74 ns | 0.458 ns | 0.119 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,741.7 ns |  5.87 ns | 3.49 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,262.1 ns |  4.02 ns | 2.39 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,402.3 ns |  4.65 ns | 3.07 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,418.0 ns |  2.73 ns | 1.81 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,174.0 ns |  1.49 ns | 0.89 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,980.5 ns |  4.54 ns | 2.70 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,977.0 ns | 10.06 ns | 5.98 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,908.2 ns |  4.02 ns | 2.39 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,194.7 ns |  2.07 ns | 1.23 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,041.3 ns |  2.27 ns | 1.35 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   869.4 ns |  1.92 ns | 1.14 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   817.4 ns |  1.67 ns | 1.11 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   135.5 ns |  0.15 ns | 0.08 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,725.4 ns |  2.12 ns | 1.26 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,169.6 ns |  1.24 ns | 0.82 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   3.633 ns | 0.0062 ns | 0.0049 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 236.959 ns | 0.1651 ns | 0.1464 ns | 65.22 |    0.09 |         - |          NA |

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,728.27 ns | 37.323 ns | 24.687 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.27 ns |  0.034 ns |  0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.74 ns |  0.033 ns |  0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.79 ns |  0.088 ns |  0.058 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.36 ns |  0.082 ns |  0.054 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.99 ns |  0.015 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    103.53 ns |  0.291 ns |  0.173 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.87 ns |  0.039 ns |  0.023 ns |  0.53 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     292.7 ns |   1.69 ns |  1.12 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,592.6 ns | 107.10 ns | 63.74 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     224.6 ns |   0.39 ns |  0.26 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,323.8 ns | 146.38 ns | 96.82 ns |      - |      80 B |

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