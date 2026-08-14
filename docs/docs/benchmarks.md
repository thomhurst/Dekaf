---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 22:44 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 19×–20× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 1000× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.8×–10× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.16 | 1.00–1.33 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.27 | 1.06–1.51 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.76 | 0.71–1.02 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 0.98–2.40 | 78% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.10 | 21% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.26 | 0.18–0.43 | 96% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.92 | 0.74–1.42 | 74% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.01 | 0.89–1.05 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.78 | 0.73–0.91 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.81 | 0.75–1.05 | 37% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.46 | 13% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.10 | 1.00–1.28 | 25% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 43% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 47% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 43% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 47% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,912.8 μs** |    **99.73 μs** |  **65.96 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,587.3 μs |    31.68 μs |  20.95 μs |  0.44 |    0.01 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,309.6 μs** |    **75.92 μs** |  **50.22 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,627.4 μs |   156.68 μs |  93.23 μs |  0.50 |    0.01 |        - |       - |   49871 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,666.4 μs** |    **31.45 μs** |  **18.72 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194787 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,683.4 μs |    33.40 μs |  19.88 μs |  0.40 |    0.00 |        - |       - |    6366 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,416.4 μs** |   **234.98 μs** | **139.83 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,125.5 μs | 1,008.24 μs | 599.99 μs |  1.16 |    0.06 |        - |       - |   51513 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **109.8 μs** |     **8.59 μs** |   **5.68 μs** |  **1.00** |    **0.07** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    105.5 μs |    31.99 μs |  21.16 μs |  0.96 |    0.19 |        - |       - |      22 B |       0.001 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,017.7 μs** |   **124.31 μs** |  **73.98 μs** |  **1.01** |    **0.10** |  **17.5781** |       **-** |  **304004 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,111.1 μs |   177.94 μs | 117.70 μs |  1.10 |    0.14 |        - |       - |     274 B |       0.001 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **889.6 μs** |    **13.11 μs** |   **6.85 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121292 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    675.0 μs |   128.72 μs |  85.14 μs |  0.76 |    0.09 |        - |       - |     401 B |       0.003 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,856.8 μs** |    **89.43 μs** |  **59.15 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1213839 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,864.3 μs |   939.73 μs | 621.57 μs |  0.78 |    0.07 |        - |       - |    1410 B |       0.001 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,481.9 μs** |    **30.34 μs** |  **20.07 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    249.4 μs |     7.33 μs |   4.36 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,499.7 μs** |    **20.11 μs** |  **13.30 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    251.1 μs |     8.73 μs |   5.78 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,496.9 μs** |    **29.32 μs** |  **19.40 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    248.7 μs |     9.44 μs |   6.24 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,489.0 μs** |    **12.80 μs** |   **8.47 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    254.6 μs |     8.00 μs |   5.29 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **112.7 μs** |    **41.19 μs** |  **21.54 μs** |   **102.7 μs** |  **1.03** |    **0.25** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   127.0 μs |    27.34 μs |  14.30 μs |   132.0 μs |  1.16 |    0.23 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **125.4 μs** |    **36.80 μs** |  **16.34 μs** |   **117.6 μs** |  **1.01** |    **0.16** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   181.2 μs |    13.92 μs |   7.28 μs |   177.9 μs |  1.46 |    0.16 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **938.6 μs** |   **597.79 μs** | **312.66 μs** |   **741.8 μs** |  **1.08** |    **0.44** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   738.3 μs |   164.32 μs |  72.96 μs |   716.5 μs |  0.85 |    0.22 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,313.8 μs** | **1,083.81 μs** | **566.85 μs** |   **956.9 μs** |  **1.13** |    **0.59** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,807.0 μs | 1,574.86 μs | 699.25 μs | 2,291.2 μs |  1.56 |    0.75 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,508.0 ns** |    **34.94 ns** |    **18.27 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   570.7 ns |   137.78 ns |    91.14 ns |  0.10 |    0.02 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **2,705.0 ns** | **2,444.90 ns** | **1,617.15 ns** |  **1.37** |    **1.11** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,103.8 ns |   160.84 ns |   106.39 ns |  0.56 |    0.29 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 572.07 ns | 12.893 ns | 1.995 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.49 ns |  0.078 ns | 0.020 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.72 ns |  0.195 ns | 0.030 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.83 ns |  0.115 ns | 0.018 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.000 μs** | **0.0038 μs** | **0.0006 μs** |         **-** |
| **WriteRequest** | **1**       | **2.002 μs** | **0.0018 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.375 μs** | **0.0082 μs** | **0.0021 μs** |         **-** |
| **WriteRequest** | **9**       | **2.388 μs** | **0.0118 μs** | **0.0031 μs** |         **-** |
| **WriteRequest** | **10**      | **2.407 μs** | **0.0316 μs** | **0.0049 μs** |         **-** |
| **WriteRequest** | **11**      | **2.424 μs** | **0.0089 μs** | **0.0023 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.99 ns** | **0.360 ns** | **0.094 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  93.39 ns | 0.240 ns | 0.062 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **97.73 ns** | **0.259 ns** | **0.067 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  85.44 ns | 0.343 ns | 0.089 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,632.9 ns | 1.09 ns | 0.57 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,934.0 ns | 0.51 ns | 0.34 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,294.9 ns | 3.24 ns | 1.93 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,345.5 ns | 9.81 ns | 5.84 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,031.5 ns | 1.56 ns | 0.82 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,021.1 ns | 3.09 ns | 1.62 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,858.3 ns | 9.03 ns | 5.37 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,731.4 ns | 7.32 ns | 4.84 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,117.5 ns | 0.78 ns | 0.46 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,814.3 ns | 2.37 ns | 1.41 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   783.9 ns | 2.29 ns | 1.52 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   917.6 ns | 1.85 ns | 1.10 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   139.3 ns | 0.22 ns | 0.14 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,741.0 ns | 7.35 ns | 4.86 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,256.7 ns | 0.85 ns | 0.50 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,861.02 ns | 48.110 ns | 31.821 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.22 ns |  0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.69 ns |  0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.73 ns |  0.033 ns |  0.022 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.99 ns |  0.046 ns |  0.027 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     14.46 ns |  0.029 ns |  0.015 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    104.97 ns |  1.532 ns |  1.013 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.28 ns |  0.034 ns |  0.018 ns |  0.53 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     295.8 ns |   1.63 ns |   1.08 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,700.6 ns | 187.11 ns | 111.35 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     224.7 ns |   0.47 ns |   0.31 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,913.3 ns | 184.78 ns | 122.22 ns |      - |      80 B |

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