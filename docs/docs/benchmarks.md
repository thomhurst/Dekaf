---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-15 10:52 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 21×–22× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 1000× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.2× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.8×–9.8× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.16 | 1.02–1.33 | 27% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.31 | 1.06–1.51 | 34% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.68–0.97 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 1.17–2.40 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 24% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.26 | 0.18–0.43 | 95% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.74–1.42 | 75% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.97 | 0.92–1.08 | 17% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.85 | 0.74–0.97 | 27% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.82 | 0.75–1.05 | 36% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 7% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.53 | 31% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.09 | 1.00–1.28 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 48% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 52% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 52% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,021.3 μs** |    **37.86 μs** |    **19.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,678.6 μs |    24.58 μs |    16.26 μs |  0.44 |    0.00 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,551.0 μs** |    **70.45 μs** |    **46.60 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,658.8 μs |   125.55 μs |    83.04 μs |  0.48 |    0.01 |        - |       - |   49873 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,401.9 μs** |    **48.35 μs** |    **28.77 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194790 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,709.9 μs |    42.52 μs |    28.12 μs |  0.42 |    0.00 |        - |       - |    6299 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,665.3 μs** |   **149.87 μs** |    **99.13 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,649.6 μs | 1,758.75 μs | 1,163.31 μs |  1.00 |    0.10 |        - |       - |   51774 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **123.4 μs** |     **0.94 μs** |     **0.62 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    106.5 μs |    11.01 μs |     7.28 μs |  0.86 |    0.06 |        - |       - |      30 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,254.3 μs** |   **157.21 μs** |   **103.99 μs** |  **1.01** |    **0.12** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,114.6 μs |   169.97 μs |   112.42 μs |  0.89 |    0.11 |        - |       - |     250 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,002.0 μs** |    **24.92 μs** |    **13.03 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121517 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    809.2 μs |   168.72 μs |   100.40 μs |  0.81 |    0.10 |        - |       - |     189 B |       0.002 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,799.2 μs** |    **57.37 μs** |    **30.01 μs** |  **1.00** |    **0.00** |  **70.3125** |       **-** | **1215730 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,279.4 μs | 1,003.55 μs |   597.20 μs |  0.84 |    0.06 |        - |       - |     906 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,490.7 μs** |    **18.29 μs** |    **12.10 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    311.9 μs |    13.02 μs |     8.61 μs |  0.06 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,481.5 μs** |    **15.41 μs** |     **9.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    305.9 μs |     9.13 μs |     6.04 μs |  0.06 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,492.2 μs** |     **9.72 μs** |     **5.09 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    308.3 μs |     6.76 μs |     4.47 μs |  0.06 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,493.5 μs** |    **15.96 μs** |    **10.56 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    318.6 μs |    13.62 μs |     9.01 μs |  0.06 |    0.00 |        - |       - |     457 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **121.6 μs** |    **21.32 μs** |  **11.15 μs** |  **1.01** |    **0.12** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   132.3 μs |    10.12 μs |   4.49 μs |  1.10 |    0.10 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **153.5 μs** |    **70.36 μs** |  **36.80 μs** |  **1.04** |    **0.31** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   163.4 μs |     6.85 μs |   3.04 μs |  1.11 |    0.21 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,058.2 μs** |   **567.65 μs** | **296.89 μs** |  **1.06** |    **0.37** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   802.6 μs |   155.05 μs |  68.84 μs |  0.80 |    0.19 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,443.5 μs** |   **882.52 μs** | **461.58 μs** |  **1.08** |    **0.44** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,709.3 μs | 1,202.73 μs | 534.02 μs |  1.28 |    0.52 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,565.4 ns** |    **14.45 ns** |   **7.56 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   566.8 ns |   158.09 ns | 104.57 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,276.2 ns** | **1,538.39 ns** | **915.47 ns** |  **1.12** |    **0.58** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,073.3 ns |   285.51 ns | 188.85 ns |  0.37 |    0.17 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 528.30 ns | 16.455 ns | 4.273 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.70 ns |  0.280 ns | 0.073 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.12 ns |  0.326 ns | 0.085 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.11 ns |  0.216 ns | 0.056 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.004 μs** | **0.0037 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **1**       | **2.000 μs** | **0.0039 μs** | **0.0006 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.412 μs** | **0.0335 μs** | **0.0052 μs** |         **-** |
| **WriteRequest** | **9**       | **2.419 μs** | **0.0174 μs** | **0.0045 μs** |         **-** |
| **WriteRequest** | **10**      | **2.401 μs** | **0.0154 μs** | **0.0040 μs** |         **-** |
| **WriteRequest** | **11**      | **2.569 μs** | **0.0177 μs** | **0.0027 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **99.98 ns** | **0.205 ns** | **0.032 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 99.22 ns | 0.257 ns | 0.067 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **94.69 ns** | **0.265 ns** | **0.069 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 87.48 ns | 0.637 ns | 0.165 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.6 ns |  2.72 ns | 1.62 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,942.9 ns | 10.68 ns | 6.36 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,290.7 ns |  3.80 ns | 2.51 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,307.0 ns |  6.17 ns | 4.08 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,871.8 ns |  3.70 ns | 2.20 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,935.3 ns |  3.79 ns | 1.98 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,885.7 ns |  3.19 ns | 1.67 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,755.4 ns |  8.33 ns | 5.51 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,126.5 ns |  1.10 ns | 0.58 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.2 ns |  4.43 ns | 2.93 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   747.6 ns |  1.10 ns | 0.65 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   812.8 ns |  2.37 ns | 1.41 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   142.1 ns |  0.31 ns | 0.19 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,734.6 ns |  5.33 ns | 2.79 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,303.6 ns |  1.00 ns | 0.59 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   3.632 ns | 0.0039 ns | 0.0033 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 232.570 ns | 0.3498 ns | 0.2921 ns | 64.04 |    0.10 |         - |          NA |

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,943.37 ns | 18.100 ns | 11.972 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.15 ns |  0.015 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.68 ns |  0.017 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.73 ns |  0.038 ns |  0.023 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.38 ns |  0.296 ns |  0.176 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.98 ns |  0.012 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    121.33 ns |  0.372 ns |  0.246 ns |  1.00 |    0.00 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.69 ns |  0.042 ns |  0.022 ns |  0.45 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     291.7 ns |   0.53 ns |   0.31 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,298.0 ns | 116.76 ns |  77.23 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     222.0 ns |   0.67 ns |   0.44 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,881.6 ns | 225.33 ns | 149.04 ns |      - |      80 B |

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