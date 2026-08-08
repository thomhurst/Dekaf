---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-08 14:38 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 20× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 71× less | ⚠ Noisy |
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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.14 | 0.94–1.46 | 45% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 1.00–1.50 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.70–1.08 | 46% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.48 | 0.80–2.22 | 95% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 45% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.16–0.32 | 58% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.94 | 0.77–1.26 | 52% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.73–1.10 | 37% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.85 | 0.76–1.08 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.83 | 0.72–1.24 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.44 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.09 | 0.99–1.51 | 48% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 56% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 45% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 57% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 54% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,131.8 μs** |  **85.56 μs** |  **56.59 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,647.5 μs |  26.29 μs |  15.64 μs |  0.43 |    0.00 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,584.3 μs** |  **86.85 μs** |  **57.45 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,704.9 μs | 110.70 μs |  73.22 μs |  0.49 |    0.01 |        - |       - |   51808 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,489.6 μs** | **111.07 μs** |  **66.10 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,770.4 μs |  65.16 μs |  43.10 μs |  0.43 |    0.01 |        - |       - |    7828 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,646.4 μs** | **567.29 μs** | **296.70 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,667.1 μs | 925.33 μs | 550.65 μs |  1.09 |    0.05 |        - |       - |   70148 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.1 μs** |   **1.37 μs** |   **0.90 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    115.5 μs |  18.42 μs |  12.19 μs |  0.95 |    0.10 |        - |       - |     235 B |       0.008 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,149.6 μs** | **177.80 μs** | **117.60 μs** |  **1.01** |    **0.15** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,168.6 μs | 153.34 μs |  91.25 μs |  1.03 |    0.13 |        - |       - |    2118 B |       0.007 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **974.9 μs** |  **74.60 μs** |  **49.34 μs** |  **1.00** |    **0.07** |   **7.0801** |       **-** |  **121371 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    810.0 μs |  72.61 μs |  43.21 μs |  0.83 |    0.06 |        - |       - |    4840 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,434.9 μs** |  **83.20 μs** |  **43.51 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214373 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,733.8 μs | 940.81 μs | 622.29 μs |  0.82 |    0.06 |        - |       - |   22869 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,476.4 μs** |  **17.71 μs** |  **11.71 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    296.7 μs |   4.55 μs |   3.01 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,469.0 μs** |  **12.55 μs** |   **6.56 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    290.8 μs |  12.61 μs |   8.34 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,485.1 μs** |  **10.89 μs** |   **7.20 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    299.7 μs |  10.50 μs |   6.95 μs |  0.05 |    0.00 |        - |       - |     625 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,477.9 μs** |   **7.96 μs** |   **4.16 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    298.8 μs |   7.47 μs |   4.44 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **132.1 μs** |  **31.96 μs** |  **16.71 μs** |  **1.01** |    **0.17** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   125.6 μs |   6.70 μs |   2.98 μs |  0.96 |    0.12 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **167.2 μs** |  **68.56 μs** |  **35.86 μs** |  **1.04** |    **0.28** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   172.6 μs |  42.70 μs |  22.33 μs |  1.07 |    0.24 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,100.3 μs** | **536.78 μs** | **280.74 μs** |  **1.06** |    **0.36** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   954.6 μs | 632.88 μs | 281.00 μs |  0.92 |    0.33 | 258.48 KB |        0.40 | ⚠ Low |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,432.7 μs** | **850.21 μs** | **444.68 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,862.2 μs | 990.26 μs | 439.68 μs |  1.40 |    0.48 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error      | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|-----------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,608.5 ns** |   **116.1 ns** |    **69.08 ns** |  **1.00** |    **0.02** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   556.3 ns |   129.4 ns |    85.56 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |            |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,127.5 ns** | **1,841.4 ns** | **1,217.98 ns** |  **1.24** |    **0.83** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,044.1 ns |   103.0 ns |    61.30 ns |  0.41 |    0.22 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 518.90 ns | 25.067 ns | 6.510 ns | 0.0486 |    1224 B |
| WriteFindCoordinatorV6     |  23.11 ns |  0.056 ns | 0.009 ns |      - |         - |
| WriteDescribeGroupsV6      |  42.04 ns |  0.372 ns | 0.097 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.79 ns |  0.387 ns | 0.101 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.825 μs** | **0.0038 μs** | **0.0006 μs** |         **-** |
| **WriteRequest** | **1**       | **1.825 μs** | **0.0080 μs** | **0.0012 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.380 μs** | **0.0057 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **9**       | **2.386 μs** | **0.0446 μs** | **0.0069 μs** |         **-** |
| **WriteRequest** | **10**      | **2.379 μs** | **0.0064 μs** | **0.0010 μs** |         **-** |
| **WriteRequest** | **11**      | **2.404 μs** | **0.0061 μs** | **0.0009 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.77 ns** | **0.357 ns** | **0.055 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  98.04 ns | 0.213 ns | 0.033 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **92.07 ns** | **0.606 ns** | **0.157 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  86.22 ns | 0.245 ns | 0.064 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,643.4 ns |  6.11 ns | 4.04 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,021.8 ns |  1.32 ns | 0.79 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,328.2 ns |  4.11 ns | 2.15 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,265.5 ns |  2.65 ns | 1.39 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,899.2 ns |  1.15 ns | 0.68 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,032.1 ns |  7.04 ns | 3.68 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,028.8 ns |  7.86 ns | 5.20 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,846.1 ns | 11.34 ns | 6.75 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.5 ns |  0.97 ns | 0.58 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.8 ns |  2.03 ns | 1.21 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   709.9 ns |  0.94 ns | 0.62 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   796.2 ns |  3.36 ns | 2.00 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   169.5 ns |  0.66 ns | 0.35 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,687.7 ns |  3.74 ns | 2.22 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,290.6 ns |  0.55 ns | 0.33 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,068.72 ns | 17.616 ns | 10.483 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.86 ns |  0.032 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.74 ns |  0.038 ns |  0.022 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.43 ns |  0.063 ns |  0.038 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.27 ns |  3.572 ns |  2.362 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.023 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    125.35 ns | 13.111 ns |  8.672 ns |  1.00 |    0.09 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.94 ns |  0.142 ns |  0.084 ns |  0.44 |    0.03 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error       | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|------------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    282.8 ns |     1.61 ns |   1.06 ns | 0.0019 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 91,683.1 ns |   471.31 ns | 280.47 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    189.2 ns |     0.22 ns |   0.13 ns | 0.0031 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 91,032.6 ns | 1,262.90 ns | 835.33 ns |      - |      80 B |

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