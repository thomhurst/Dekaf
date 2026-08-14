---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 12:06 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18×–19× faster | 3.0× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 100× less | ⚠ Noisy |
| Consume — drain a topic | 1.7× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–10× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.12 | 0.93–1.20 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 1.01–1.39 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.76 | 0.70–1.02 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.69 | 0.98–2.40 | 84% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 53% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.13–0.28 | 57% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.88 | 0.75–1.12 | 43% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.89 | 0.74–1.09 | 39% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.76–1.25 | 61% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.77 | 0.69–1.56 | 113% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 0.99–1.52 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 58% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 61% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 64% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,070.4 μs** |  **67.46 μs** |  **44.62 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,691.9 μs |  14.15 μs |   9.36 μs |  0.44 |    0.00 |        - |       - |    5403 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,637.8 μs** |  **92.85 μs** |  **55.25 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,767.8 μs | 107.39 μs |  71.03 μs |  0.49 |    0.01 |        - |       - |   50327 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,309.9 μs** | **118.23 μs** |  **70.36 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,711.9 μs | 150.03 μs |  99.23 μs |  0.43 |    0.02 |        - |       - |    6753 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,230.2 μs** | **374.73 μs** | **247.86 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,883.7 μs | 712.87 μs | 471.52 μs |  1.05 |    0.04 |        - |       - |   55169 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **119.6 μs** |  **18.44 μs** |  **12.20 μs** |  **1.01** |    **0.15** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    112.5 μs |  17.15 μs |  10.20 μs |  0.95 |    0.13 |        - |       - |     382 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,269.4 μs** |  **14.39 μs** |   **8.56 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,172.3 μs | 315.22 μs | 208.50 μs |  0.92 |    0.16 |        - |       - |    5788 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,021.0 μs** |   **8.71 μs** |   **5.18 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121483 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    848.9 μs | 132.25 μs |  87.48 μs |  0.83 |    0.08 |        - |       - |     991 B |       0.008 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,347.9 μs** | **100.56 μs** |  **66.51 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214892 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,083.0 μs | 572.01 μs | 299.17 μs |  0.78 |    0.03 |        - |       - |   13223 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,535.8 μs** |  **22.43 μs** |  **14.84 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    313.5 μs |   8.50 μs |   5.06 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,511.6 μs** |  **15.62 μs** |  **10.33 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    306.6 μs |   5.97 μs |   3.55 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,511.6 μs** |   **7.22 μs** |   **4.30 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    316.6 μs |   7.41 μs |   4.90 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.24 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,517.1 μs** |   **6.34 μs** |   **3.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    319.7 μs |   9.98 μs |   6.60 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.24 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **133.7 μs** |    **57.12 μs** |  **29.87 μs** |   **115.7 μs** |  **1.04** |    **0.30** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   123.1 μs |     2.70 μs |   1.20 μs |   123.5 μs |  0.96 |    0.18 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **134.4 μs** |     **3.81 μs** |   **1.69 μs** |   **134.1 μs** |  **1.00** |    **0.02** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   165.1 μs |     6.71 μs |   2.98 μs |   164.3 μs |  1.23 |    0.03 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,108.0 μs** |   **515.71 μs** | **269.73 μs** | **1,016.4 μs** |  **1.05** |    **0.34** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   774.3 μs |   138.05 μs |  61.30 μs |   751.2 μs |  0.74 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,385.1 μs** |   **891.04 μs** | **466.03 μs** | **1,082.2 μs** |  **1.08** |    **0.45** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,686.2 μs | 1,127.78 μs | 500.74 μs | 1,961.7 μs |  1.32 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,572.2 ns** |  **14.12 ns** |   **7.38 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   545.3 ns | 127.35 ns |  84.23 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |           |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,786.9 ns** |  **71.23 ns** |  **37.25 ns** |  **1.00** |    **0.01** | **0.1450** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        | 1,031.3 ns | 181.48 ns | 120.04 ns |  0.27 |    0.03 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 575.47 ns | 27.067 ns | 7.029 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.32 ns |  0.987 ns | 0.256 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.07 ns |  1.324 ns | 0.205 ns |      - |         - |
| WriteListConfigResourcesV1 |  25.14 ns |  0.073 ns | 0.019 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.002 μs** | **0.0107 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **1**       | **2.041 μs** | **0.0029 μs** | **0.0007 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.455 μs** | **0.0100 μs** | **0.0026 μs** |         **-** |
| **WriteRequest** | **9**       | **2.484 μs** | **0.0083 μs** | **0.0021 μs** |         **-** |
| **WriteRequest** | **10**      | **2.455 μs** | **0.0176 μs** | **0.0027 μs** |         **-** |
| **WriteRequest** | **11**      | **2.464 μs** | **0.0126 μs** | **0.0019 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **104.13 ns** | **0.314 ns** | **0.082 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.41 ns | 0.326 ns | 0.050 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **97.11 ns** | **0.981 ns** | **0.152 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  96.14 ns | 0.491 ns | 0.076 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,742.5 ns | 6.27 ns | 4.15 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,068.3 ns | 2.72 ns | 1.42 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,417.5 ns | 1.83 ns | 1.09 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,741.4 ns | 1.81 ns | 1.20 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,180.4 ns | 1.90 ns | 1.26 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,976.8 ns | 4.63 ns | 2.42 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,913.4 ns | 4.99 ns | 3.30 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,897.7 ns | 3.85 ns | 2.54 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,194.1 ns | 0.38 ns | 0.20 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,043.6 ns | 1.29 ns | 0.67 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   788.7 ns | 1.85 ns | 0.97 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   846.3 ns | 2.63 ns | 1.74 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   135.6 ns | 0.08 ns | 0.04 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,724.3 ns | 0.85 ns | 0.51 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,165.8 ns | 1.14 ns | 0.75 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,088.525 ns | 10.9032 ns | 5.7026 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    13.303 ns |  0.0077 ns | 0.0051 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    16.187 ns |  0.0127 ns | 0.0084 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    28.762 ns |  0.0544 ns | 0.0360 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    24.668 ns |  0.0448 ns | 0.0266 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     9.291 ns |  0.0092 ns | 0.0048 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    80.893 ns |  0.3801 ns | 0.2514 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    41.931 ns |  0.0363 ns | 0.0240 ns |  0.52 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     295.0 ns |   1.14 ns |   0.68 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,773.6 ns | 130.80 ns |  68.41 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     226.2 ns |   0.94 ns |   0.56 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,605.9 ns | 396.42 ns | 262.21 ns |      - |      80 B |

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