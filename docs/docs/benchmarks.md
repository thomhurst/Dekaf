---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-05 16:40 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 4.0×–4.1× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 67× less | ⚠ Noisy |
| Consume — drain a topic | 1.5× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.6×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.14 | 1.03–1.46 | 38% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.26 | 0.99–1.50 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.75 | 0.70–0.95 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.46 | 0.80–2.22 | 97% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 49% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.14–0.32 | 65% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.92 | 0.77–1.05 | 31% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.94 | 0.58–1.20 | 66% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.84 | 0.76–1.08 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.86 | 0.74–1.39 | 75% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.41–0.44 | 9% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.12 | 0.97–1.92 | 84% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.25 | 0.03–0.47 | 180% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.25 | 0.04–0.48 | 180% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.25 | 0.03–0.48 | 182% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.25 | 0.03–0.47 | 177% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,327.9 μs** |   **217.82 μs** | **144.07 μs** |  **1.00** |    **0.03** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,719.2 μs |    68.36 μs |  45.22 μs |  0.43 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,499.5 μs** |    **81.87 μs** |  **54.15 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,858.1 μs |   127.37 μs |  84.25 μs |  0.51 |    0.01 |        - |       - |   51707 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,683.1 μs** |    **85.34 μs** |  **56.45 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,776.9 μs |    57.18 μs |  34.03 μs |  0.42 |    0.01 |        - |       - |    7853 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,142.5 μs** |   **278.69 μs** | **165.84 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,858.4 μs | 1,129.72 μs | 590.87 μs |  0.97 |    0.05 |        - |       - |   77449 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **118.6 μs** |     **3.57 μs** |   **2.36 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    102.3 μs |    19.21 μs |  10.05 μs |  0.86 |    0.08 |        - |       - |     582 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,103.3 μs** |   **190.39 μs** | **125.93 μs** |  **1.01** |    **0.17** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,161.4 μs |    83.48 μs |  55.22 μs |  1.07 |    0.14 |        - |       - |    2070 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **891.3 μs** |    **26.09 μs** |  **15.52 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121270 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    756.6 μs |   151.72 μs | 100.35 μs |  0.85 |    0.11 |        - |       - |    3285 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,765.2 μs** |   **211.73 μs** | **126.00 μs** |  **1.00** |    **0.02** |  **72.2656** |       **-** | **1212680 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,607.4 μs |   757.15 μs | 500.81 μs |  0.75 |    0.06 |        - |       - |   16922 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,490.2 μs** |    **14.83 μs** |   **9.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    248.4 μs |     8.06 μs |   5.33 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,528.3 μs** |   **203.79 μs** | **106.59 μs** |  **1.00** |    **0.03** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    250.7 μs |     3.51 μs |   2.09 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,500.3 μs** |     **9.25 μs** |   **6.12 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    255.7 μs |     7.82 μs |   5.18 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,500.8 μs** |    **26.69 μs** |  **17.66 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    250.7 μs |     7.94 μs |   5.25 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **114.4 μs** |    **50.01 μs** |  **26.16 μs** |  **1.04** |    **0.30** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   147.5 μs |    25.98 μs |  13.59 μs |  1.34 |    0.28 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **148.0 μs** |    **72.57 μs** |  **37.95 μs** |  **1.05** |    **0.35** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   182.5 μs |    15.14 μs |   6.72 μs |  1.30 |    0.29 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,028.8 μs** |   **591.97 μs** | **309.61 μs** |  **1.08** |    **0.42** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   707.6 μs |   161.42 μs |  71.67 μs |  0.74 |    0.21 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,454.2 μs** | **1,094.76 μs** | **572.58 μs** |  **1.12** |    **0.55** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,453.9 μs | 1,877.41 μs | 833.58 μs |  1.89 |    0.85 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,504.5 ns** |    **32.05 ns** |    **16.76 ns** | **5,497.4 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   551.1 ns |   129.83 ns |    85.87 ns |   530.6 ns |  0.10 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **2,802.7 ns** | **1,846.74 ns** | **1,221.50 ns** | **3,648.9 ns** |  **1.26** |    **0.85** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,213.6 ns |   182.45 ns |   120.68 ns | 1,169.0 ns |  0.54 |    0.28 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 559.99 ns | 5.819 ns | 0.900 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.01 ns | 0.147 ns | 0.038 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.32 ns | 0.379 ns | 0.059 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.20 ns | 0.354 ns | 0.055 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.964 μs** | **0.0082 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **1**       | **2.006 μs** | **0.0142 μs** | **0.0037 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.454 μs** | **0.0066 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **9**       | **2.455 μs** | **0.0454 μs** | **0.0070 μs** |         **-** |
| **WriteRequest** | **10**      | **2.526 μs** | **0.0100 μs** | **0.0026 μs** |         **-** |
| **WriteRequest** | **11**      | **2.463 μs** | **0.0147 μs** | **0.0038 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **101.19 ns** | **0.373 ns** | **0.058 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.90 ns | 0.998 ns | 0.154 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **95.95 ns** | **0.909 ns** | **0.236 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  91.32 ns | 0.255 ns | 0.066 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,744.3 ns |  8.89 ns |  5.88 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,129.1 ns |  2.44 ns |  1.62 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,428.6 ns |  1.96 ns |  1.16 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,381.7 ns |  3.26 ns |  1.94 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,001.8 ns |  5.55 ns |  3.67 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,975.0 ns | 14.58 ns |  9.64 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,929.4 ns |  9.99 ns |  5.94 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,899.1 ns |  5.41 ns |  3.58 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,191.3 ns |  0.95 ns |  0.50 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,040.1 ns |  2.98 ns |  1.97 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   706.9 ns |  1.65 ns |  0.98 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   794.2 ns |  1.10 ns |  0.73 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   162.6 ns |  0.08 ns |  0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,732.0 ns | 19.34 ns | 12.79 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,182.4 ns |  0.66 ns |  0.44 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,077.33 ns | 35.709 ns | 23.619 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.20 ns |  0.007 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.20 ns |  0.039 ns |  0.023 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.69 ns |  0.147 ns |  0.088 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.93 ns |  0.189 ns |  0.112 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.94 ns |  0.019 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    112.13 ns |  1.829 ns |  1.210 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.91 ns |  0.137 ns |  0.082 ns |  0.49 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     289.8 ns |   1.22 ns |   0.81 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,362.7 ns |  92.08 ns |  54.79 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.7 ns |   0.36 ns |   0.19 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,605.5 ns | 378.37 ns | 250.27 ns |      - |      80 B |

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