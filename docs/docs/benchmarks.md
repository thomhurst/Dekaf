---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-13 18:41 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.7× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 154× less | Mixed |
| Consume — drain a topic | 1.5× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.8× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.09 | 1.03–1.28 | 22% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 0.97–1.42 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–0.89 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.55 | 1.34–2.28 | 61% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 57% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.93 | 0.75–1.02 | 30% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.98 | 0.74–1.20 | 47% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.83 | 0.74–1.25 | 63% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.71–1.56 | 112% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.47 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.99–1.52 | 51% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 60% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 64% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 59% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.02–0.06 | 63% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,859.43 μs** |    **84.025 μs** |  **55.577 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,558.59 μs |    20.149 μs |  10.538 μs |  0.44 |    0.00 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,250.00 μs** |   **120.885 μs** |  **79.958 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,675.37 μs |    69.266 μs |  45.815 μs |  0.51 |    0.01 |        - |       - |   50928 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,617.41 μs** |    **49.878 μs** |  **32.991 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,722.75 μs |   152.848 μs | 101.099 μs |  0.41 |    0.01 |        - |       - |    7314 B |        0.04 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,407.94 μs** |   **199.848 μs** | **132.187 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,338.98 μs |   939.555 μs | 559.114 μs |  1.19 |    0.05 |        - |       - |   60959 B |        0.03 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **102.75 μs** |     **0.382 μs** |   **0.200 μs** |  **1.00** |    **0.00** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     93.74 μs |    20.955 μs |  12.470 μs |  0.91 |    0.12 |        - |       - |     103 B |       0.003 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,021.93 μs** |   **135.971 μs** |  **80.914 μs** |  **1.01** |    **0.12** |  **17.5781** |       **-** |  **304004 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,123.46 μs |   127.221 μs |  66.539 μs |  1.11 |    0.11 |        - |       - |    1534 B |       0.005 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **888.60 μs** |    **14.723 μs** |   **7.700 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121322 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    747.46 μs |   111.322 μs |  73.633 μs |  0.84 |    0.08 |        - |       - |    1090 B |       0.009 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,765.37 μs** |   **164.938 μs** | **109.096 μs** |  **1.00** |    **0.02** |  **72.2656** |       **-** | **1213424 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,975.09 μs | 1,408.609 μs | 931.707 μs |  0.80 |    0.10 |        - |       - |    9666 B |       0.008 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,475.49 μs** |    **19.853 μs** |  **13.132 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    245.53 μs |     6.718 μs |   4.443 μs |  0.04 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,472.00 μs** |    **25.750 μs** |  **17.032 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    250.66 μs |    11.768 μs |   7.784 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,482.98 μs** |    **16.121 μs** |  **10.663 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    256.81 μs |     6.480 μs |   4.286 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,487.44 μs** |    **23.036 μs** |  **15.237 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    252.44 μs |     4.311 μs |   2.852 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **112.7 μs** |    **41.10 μs** |  **21.49 μs** |   **101.8 μs** |  **1.03** |    **0.26** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   117.5 μs |    20.69 μs |  10.82 μs |   121.3 μs |  1.07 |    0.20 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **137.1 μs** |    **61.96 μs** |  **32.40 μs** |   **115.8 μs** |  **1.04** |    **0.31** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   160.8 μs |     7.65 μs |   3.40 μs |   161.0 μs |  1.22 |    0.24 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **932.0 μs** |   **597.56 μs** | **312.53 μs** |   **733.8 μs** |  **1.08** |    **0.45** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   658.0 μs |   145.21 μs |  51.78 μs |   632.8 μs |  0.77 |    0.20 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,156.4 μs** |   **733.81 μs** | **325.81 μs** |   **978.0 μs** |  **1.05** |    **0.36** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,972.8 μs | 1,763.16 μs | 922.17 μs | 2,231.5 μs |  1.80 |    0.89 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,481.6 ns** |    **14.14 ns** |   **7.40 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   553.4 ns |   124.51 ns |  82.35 ns |  0.10 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,142.3 ns** | **1,518.50 ns** | **903.64 ns** |  **1.14** |    **0.61** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,049.8 ns |   203.73 ns | 134.76 ns |  0.38 |    0.18 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 350.11 ns | 32.613 ns | 8.470 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  15.88 ns |  0.373 ns | 0.097 ns |      - |         - |
| WriteDescribeGroupsV6      |  24.53 ns |  0.510 ns | 0.079 ns |      - |         - |
| WriteListConfigResourcesV1 |  13.09 ns |  0.425 ns | 0.066 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.239 μs** | **0.2772 μs** | **0.0720 μs** |         **-** |
| **WriteRequest** | **1**       | **1.244 μs** | **0.3201 μs** | **0.0831 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.456 μs** | **0.0068 μs** | **0.0010 μs** |         **-** |
| **WriteRequest** | **9**       | **2.456 μs** | **0.0036 μs** | **0.0006 μs** |         **-** |
| **WriteRequest** | **10**      | **2.469 μs** | **0.0334 μs** | **0.0087 μs** |         **-** |
| **WriteRequest** | **11**      | **2.471 μs** | **0.0093 μs** | **0.0024 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.26 ns** | **0.090 ns** | **0.014 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.58 ns | 2.410 ns | 0.626 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **100.13 ns** | **0.583 ns** | **0.151 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  96.64 ns | 0.309 ns | 0.048 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,739.6 ns |  2.85 ns | 1.70 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,184.0 ns | 14.90 ns | 8.86 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,404.1 ns |  2.07 ns | 1.23 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,392.3 ns |  1.45 ns | 0.86 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,006.5 ns |  6.24 ns | 4.13 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,019.2 ns | 11.90 ns | 6.22 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,927.2 ns | 10.32 ns | 6.14 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,922.6 ns |  7.21 ns | 4.29 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,193.7 ns |  4.74 ns | 3.13 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,043.0 ns |  5.93 ns | 3.53 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   728.0 ns |  2.49 ns | 1.30 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   783.4 ns |  1.83 ns | 1.09 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   159.9 ns |  0.10 ns | 0.06 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,722.7 ns | 10.66 ns | 7.05 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,216.2 ns |  1.08 ns | 0.57 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,857.97 ns | 25.838 ns | 17.090 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.15 ns |  0.010 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.75 ns |  0.092 ns |  0.061 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.73 ns |  0.020 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.98 ns |  0.427 ns |  0.282 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.99 ns |  0.020 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    118.62 ns |  1.663 ns |  1.100 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.46 ns |  0.053 ns |  0.035 ns |  0.47 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error       | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|------------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    184.7 ns |     0.85 ns |   0.51 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 47,144.4 ns | 1,264.52 ns | 836.40 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    126.6 ns |     1.02 ns |   0.54 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 70,222.2 ns |    75.77 ns |  45.09 ns |      - |      80 B |

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