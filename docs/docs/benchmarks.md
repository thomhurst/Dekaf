---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-15 07:36 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 21×–22× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 667× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–9.9× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.16 | 1.00–1.33 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.38 | 1.06–1.51 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.68–0.97 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 1.17–2.40 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 21% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.18–0.43 | 94% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.92 | 0.74–1.42 | 74% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.98 | 0.89–1.08 | 19% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.82 | 0.73–0.97 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.82 | 0.75–1.05 | 36% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.53 | 31% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.13 | 1.00–1.28 | 25% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 48% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 52% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 52% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,847.5 μs** |    **82.85 μs** |    **49.30 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,532.9 μs |    13.05 μs |     7.77 μs |  0.43 |    0.00 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,321.4 μs** |   **101.55 μs** |    **60.43 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,640.3 μs |    95.90 μs |    63.43 μs |  0.50 |    0.01 |        - |       - |   49787 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,632.3 μs** |    **24.99 μs** |    **16.53 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,414.6 μs |   436.94 μs |   289.01 μs |  0.51 |    0.04 |        - |       - |    6823 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,361.8 μs** |   **228.91 μs** |   **151.41 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,101.7 μs | 1,521.71 μs | 1,006.52 μs |  1.07 |    0.09 |        - |       - |   51858 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **102.2 μs** |     **1.18 μs** |     **0.61 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    115.9 μs |    10.76 μs |     7.12 μs |  1.13 |    0.07 |        - |       - |      55 B |       0.002 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,038.2 μs** |    **10.20 μs** |     **6.07 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,109.3 μs |   170.71 μs |   101.58 μs |  1.07 |    0.09 |        - |       - |     283 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **863.5 μs** |    **10.30 μs** |     **6.13 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121264 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    792.8 μs |   191.23 μs |   126.49 μs |  0.92 |    0.14 |        - |       - |     319 B |       0.003 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,584.9 μs** |    **91.02 μs** |    **54.16 μs** |  **1.00** |    **0.01** |  **72.2656** |       **-** | **1212414 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,644.9 μs | 1,146.93 μs |   758.62 μs |  0.77 |    0.08 |        - |       - |     947 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,471.8 μs** |    **23.25 μs** |    **15.38 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    239.3 μs |     8.32 μs |     4.95 μs |  0.04 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,471.0 μs** |    **21.36 μs** |    **11.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    244.2 μs |    10.02 μs |     6.63 μs |  0.04 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,484.9 μs** |    **17.08 μs** |    **11.30 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    248.1 μs |    10.84 μs |     7.17 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,486.8 μs** |    **24.17 μs** |    **15.99 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    242.9 μs |     6.96 μs |     3.64 μs |  0.04 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **111.1 μs** |    **36.62 μs** |  **19.15 μs** |  **1.02** |    **0.23** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   118.3 μs |    10.34 μs |   5.41 μs |  1.09 |    0.17 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **120.9 μs** |     **8.45 μs** |   **3.01 μs** |  **1.00** |    **0.03** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   162.1 μs |    10.28 μs |   4.56 μs |  1.34 |    0.05 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,000.7 μs** |   **578.79 μs** | **302.72 μs** |  **1.08** |    **0.42** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   726.5 μs |   112.46 μs |  49.93 μs |  0.78 |    0.21 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,418.1 μs** | **1,114.70 μs** | **583.01 μs** |  **1.13** |    **0.57** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,098.8 μs | 1,012.15 μs | 449.40 μs |  1.67 |    0.62 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,508.0 ns** |    **12.33 ns** |     **6.45 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   565.8 ns |   111.97 ns |    74.06 ns |  0.10 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,165.7 ns** | **2,214.42 ns** | **1,464.70 ns** |  **1.29** |    **0.98** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,022.4 ns |   122.80 ns |    73.07 ns |  0.42 |    0.24 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 483.85 ns | 4.675 ns | 0.723 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  28.93 ns | 0.185 ns | 0.048 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.80 ns | 0.301 ns | 0.078 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.41 ns | 0.153 ns | 0.040 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.071 μs** | **0.0014 μs** | **0.0004 μs** |         **-** |
| **WriteRequest** | **1**       | **2.075 μs** | **0.0035 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.382 μs** | **0.0081 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **9**       | **2.405 μs** | **0.0066 μs** | **0.0010 μs** |         **-** |
| **WriteRequest** | **10**      | **2.429 μs** | **0.0078 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **11**      | **2.406 μs** | **0.0046 μs** | **0.0012 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **101.41 ns** | **0.514 ns** | **0.080 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  98.01 ns | 0.313 ns | 0.081 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **95.87 ns** | **0.363 ns** | **0.094 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  92.12 ns | 0.070 ns | 0.018 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,637.0 ns | 2.87 ns | 1.71 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,902.1 ns | 1.10 ns | 0.58 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,289.8 ns | 5.34 ns | 3.53 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,336.7 ns | 2.22 ns | 1.32 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,111.7 ns | 7.35 ns | 4.86 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,988.3 ns | 7.46 ns | 4.44 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,911.1 ns | 4.89 ns | 2.56 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,728.9 ns | 8.76 ns | 5.79 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,117.6 ns | 0.61 ns | 0.32 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.8 ns | 3.65 ns | 2.17 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   763.2 ns | 1.16 ns | 0.69 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   811.9 ns | 2.47 ns | 1.47 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   142.5 ns | 0.07 ns | 0.04 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,702.4 ns | 2.36 ns | 1.23 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,267.6 ns | 6.83 ns | 4.52 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   3.421 ns | 0.0115 ns | 0.0102 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 238.360 ns | 0.5091 ns | 0.3975 ns | 69.67 |    0.23 |         - |          NA |

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,823.28 ns | 61.490 ns | 40.672 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.08 ns |  0.024 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.69 ns |  0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.76 ns |  0.037 ns |  0.024 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.93 ns |  0.438 ns |  0.290 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.98 ns |  0.023 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    105.44 ns |  0.406 ns |  0.268 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     69.97 ns |  0.471 ns |  0.311 ns |  0.66 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     300.1 ns |   1.33 ns |   0.88 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,724.8 ns | 325.58 ns | 215.35 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     206.3 ns |   0.31 ns |   0.20 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 102,869.2 ns | 129.53 ns |  77.08 ns |      - |      80 B |

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