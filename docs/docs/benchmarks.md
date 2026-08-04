---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-04 18:32 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.1× faster | 2.4× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 100× less | Mixed |
| Consume — drain a topic | 1.3× slower to 1.4× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.0×–12× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 0.99 | 0.85–1.13 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.10 | 0.94–1.31 | 33% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.73 | 0.64–0.86 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.27 | 0.96–1.86 | 71% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.07–0.11 | 41% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.33 | 0.16–0.41 | 77% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.96 | 0.79–1.13 | 35% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.00 | 0.85–1.14 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.98 | 0.80–1.14 | 35% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.96 | 0.75–1.11 | 37% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.51 | 0.50–0.53 | 6% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.48 | 18% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.03 | 0.97–1.48 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.47 | 0.46–0.47 | 4% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,245.0 μs** |   **173.23 μs** | **114.58 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,684.0 μs |    25.52 μs |  16.88 μs |  0.43 |    0.01 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,620.8 μs** |    **83.21 μs** |  **55.04 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,780.7 μs |    51.09 μs |  30.40 μs |  0.50 |    0.01 |        - |       - |   51789 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,397.3 μs** |    **29.44 μs** |  **17.52 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,775.1 μs |    86.97 μs |  57.52 μs |  0.43 |    0.01 |        - |       - |    7792 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,106.2 μs** |   **252.97 μs** | **150.54 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,084.3 μs | 1,567.54 μs | 932.82 μs |  1.00 |    0.07 |        - |       - |   70366 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.0 μs** |     **2.10 μs** |   **1.39 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    109.8 μs |    21.26 μs |  14.06 μs |  0.86 |    0.11 |        - |       - |     289 B |       0.010 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,264.5 μs** |    **22.34 μs** |  **14.78 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,127.9 μs |   143.72 μs |  95.07 μs |  0.89 |    0.07 |        - |       - |    2172 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,004.8 μs** |    **15.45 μs** |   **9.20 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121479 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    829.2 μs |   116.48 μs |  77.04 μs |  0.83 |    0.07 |        - |       - |    1794 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,021.6 μs** |   **137.33 μs** |  **81.72 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214480 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,143.1 μs |   297.77 μs | 177.20 μs |  0.81 |    0.02 |        - |       - |   18332 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,492.4 μs** |     **6.72 μs** |   **4.00 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,600.6 μs |    18.51 μs |  12.24 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,495.0 μs** |    **11.75 μs** |   **7.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,592.9 μs |    13.50 μs |   8.93 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,498.2 μs** |    **16.07 μs** |  **10.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,588.0 μs |     8.01 μs |   4.77 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,506.2 μs** |    **31.92 μs** |  **21.11 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,588.1 μs |    19.04 μs |  12.60 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **129.2 μs** |    **42.60 μs** |  **22.28 μs** |  **1.02** |    **0.23** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   130.8 μs |     1.10 μs |   0.58 μs |  1.04 |    0.16 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **151.5 μs** |    **55.21 μs** |  **28.87 μs** |  **1.03** |    **0.25** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   163.2 μs |    19.48 μs |  10.19 μs |  1.11 |    0.19 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,128.3 μs** |   **567.13 μs** | **296.62 μs** |  **1.06** |    **0.37** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   763.1 μs |    88.25 μs |  31.47 μs |  0.72 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,433.6 μs** |   **864.84 μs** | **452.33 μs** |  **1.08** |    **0.44** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,710.0 μs | 1,094.34 μs | 485.89 μs |  1.29 |    0.49 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,568.3 ns** |    **22.59 ns** |    **11.81 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   581.7 ns |   141.77 ns |    93.77 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,538.4 ns** | **3,043.72 ns** | **2,013.23 ns** |  **1.42** |    **1.21** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,044.7 ns |    93.74 ns |    62.00 ns |  0.42 |    0.24 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 517.79 ns | 6.965 ns | 1.809 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.02 ns | 0.399 ns | 0.062 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.87 ns | 0.313 ns | 0.048 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.18 ns | 0.152 ns | 0.039 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.955 μs** | **0.0803 μs** | **0.0124 μs** |         **-** |
| **WriteRequest** | **1**       | **2.000 μs** | **0.0054 μs** | **0.0014 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.407 μs** | **0.0050 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **9**       | **2.722 μs** | **0.0076 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **10**      | **2.389 μs** | **0.0204 μs** | **0.0032 μs** |         **-** |
| **WriteRequest** | **11**      | **2.401 μs** | **0.0319 μs** | **0.0083 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.63 ns** | **0.870 ns** | **0.226 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 106.80 ns | 0.191 ns | 0.050 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **101.61 ns** | **0.800 ns** | **0.124 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  89.41 ns | 0.731 ns | 0.113 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.7 ns | 2.73 ns | 1.43 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,116.0 ns | 2.02 ns | 1.20 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,349.8 ns | 2.97 ns | 1.96 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,264.3 ns | 4.93 ns | 2.58 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,927.0 ns | 2.18 ns | 1.14 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,958.6 ns | 6.54 ns | 3.89 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,874.8 ns | 2.48 ns | 1.64 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,845.5 ns | 4.13 ns | 2.45 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.1 ns | 1.90 ns | 1.00 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,815.1 ns | 5.32 ns | 3.52 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   709.6 ns | 0.71 ns | 0.47 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   814.3 ns | 3.38 ns | 2.24 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   179.9 ns | 0.09 ns | 0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,744.2 ns | 3.20 ns | 1.67 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,300.8 ns | 1.03 ns | 0.54 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,933.15 ns | 33.008 ns | 21.833 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.03 ns |  0.032 ns |  0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.74 ns |  0.035 ns |  0.018 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.10 ns |  0.075 ns |  0.050 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     36.57 ns |  1.222 ns |  0.809 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.011 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    121.53 ns |  1.834 ns |  1.213 ns |  1.00 |    0.01 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.22 ns |  0.110 ns |  0.058 ns |  0.45 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     359.8 ns |   1.64 ns |   0.98 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,665.1 ns | 122.14 ns |  80.79 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     220.6 ns |   0.73 ns |   0.48 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 125,916.1 ns | 238.40 ns | 141.87 ns |      - |      80 B |

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