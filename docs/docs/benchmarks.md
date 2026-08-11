---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-11 14:31 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.7× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 111× less | Stable |
| Consume — drain a topic | 1.5× slower to on par | 1.6× less | Mixed |
| Consume — poll a single message | 3.5×–9.9× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.06 | 0.94–1.28 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.18 | 1.00–1.42 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.88 | 0.72–0.91 | 21% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.51 | 1.47–1.87 | 26% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.09–0.11 | 18% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.29 | 0.27–0.29 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.88 | 0.81–1.02 | 24% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.91–1.20 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.80 | 0.74–0.95 | 26% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.78 | 0.69–0.85 | 20% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.46 | 13% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.99–1.09 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 24% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 27% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 21% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 20% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,282.8 μs** |   **162.30 μs** | **107.35 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,695.9 μs |    32.44 μs |  16.96 μs |  0.43 |    0.01 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,642.7 μs** |   **113.93 μs** |  **67.80 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,839.2 μs |   101.79 μs |  60.57 μs |  0.50 |    0.01 |        - |       - |   50937 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,284.4 μs** |   **111.22 μs** |  **66.19 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,789.2 μs |    48.12 μs |  31.83 μs |  0.44 |    0.01 |        - |       - |    7275 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,839.6 μs** | **1,108.76 μs** | **733.38 μs** |  **1.00** |    **0.08** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,049.8 μs | 1,511.46 μs | 999.74 μs |  1.02 |    0.09 |        - |       - |   59209 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.9 μs** |     **2.14 μs** |   **1.41 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    112.9 μs |    30.01 μs |  19.85 μs |  0.90 |    0.15 |        - |       - |     105 B |       0.003 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,281.8 μs** |    **13.67 μs** |   **9.04 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,285.5 μs |   162.94 μs | 107.77 μs |  1.00 |    0.08 |        - |       - |    6230 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,053.8 μs** |     **6.97 μs** |   **4.15 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121526 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    869.8 μs |   188.52 μs | 124.70 μs |  0.83 |    0.11 |        - |       - |    1398 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,521.9 μs** |   **113.00 μs** |  **59.10 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215189 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,124.7 μs | 1,056.17 μs | 698.59 μs |  0.77 |    0.06 |        - |       - |    9946 B |       0.008 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,503.1 μs** |    **11.88 μs** |   **7.86 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    318.7 μs |    16.31 μs |  10.79 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,511.7 μs** |    **12.93 μs** |   **7.69 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    311.9 μs |     7.82 μs |   5.18 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,522.7 μs** |    **16.94 μs** |  **11.20 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    311.4 μs |     8.18 μs |   5.41 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,511.2 μs** |    **18.25 μs** |  **12.07 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    324.9 μs |    14.94 μs |   9.88 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **132.2 μs** |    **34.09 μs** |  **17.83 μs** |  **1.02** |    **0.18** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   132.8 μs |    11.82 μs |   4.22 μs |  1.02 |    0.13 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **160.2 μs** |    **55.11 μs** |  **28.83 μs** |  **1.03** |    **0.24** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   180.3 μs |    89.40 μs |  39.69 μs |  1.16 |    0.30 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,155.5 μs** |   **618.20 μs** | **323.33 μs** |  **1.07** |    **0.39** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   825.2 μs |   211.91 μs |  94.09 μs |  0.76 |    0.20 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,486.8 μs** |   **856.26 μs** | **447.84 μs** |  **1.08** |    **0.42** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,991.1 μs | 2,058.36 μs | 913.92 μs |  1.44 |    0.74 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,580.4 ns** |    **26.01 ns** |    **13.61 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   562.1 ns |   157.30 ns |   104.04 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,243.0 ns** | **1,998.19 ns** | **1,045.09 ns** |  **1.16** |    **0.67** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,104.7 ns |   164.99 ns |    98.18 ns |  0.40 |    0.19 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 442.96 ns | 4.764 ns | 1.237 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.30 ns | 0.067 ns | 0.010 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.75 ns | 1.139 ns | 0.296 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.49 ns | 0.159 ns | 0.025 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.074 μs** | **0.0051 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **1**       | **2.078 μs** | **0.0184 μs** | **0.0048 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.450 μs** | **0.0039 μs** | **0.0010 μs** |         **-** |
| **WriteRequest** | **9**       | **2.452 μs** | **0.0252 μs** | **0.0039 μs** |         **-** |
| **WriteRequest** | **10**      | **2.462 μs** | **0.0118 μs** | **0.0031 μs** |         **-** |
| **WriteRequest** | **11**      | **2.454 μs** | **0.0070 μs** | **0.0018 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **107.79 ns** | **0.626 ns** | **0.163 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.67 ns | 1.150 ns | 0.299 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **100.80 ns** | **1.428 ns** | **0.221 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  99.64 ns | 0.983 ns | 0.255 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,741.1 ns |  3.98 ns |  2.37 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,070.9 ns |  1.53 ns |  0.91 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,422.2 ns |  4.55 ns |  2.71 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,368.6 ns |  1.86 ns |  0.97 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,007.2 ns |  1.24 ns |  0.65 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,988.6 ns |  9.78 ns |  5.82 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,031.0 ns |  2.00 ns |  1.05 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,901.2 ns |  4.42 ns |  2.63 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.8 ns |  1.91 ns |  1.13 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,043.3 ns |  3.93 ns |  2.34 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   727.9 ns |  0.95 ns |  0.63 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   777.7 ns |  0.73 ns |  0.38 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   160.7 ns |  0.12 ns |  0.07 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,729.6 ns | 23.79 ns | 15.74 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,185.8 ns |  0.74 ns |  0.44 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,839.46 ns | 33.708 ns | 20.059 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.20 ns |  0.020 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.91 ns |  0.022 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.68 ns |  0.268 ns |  0.140 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.71 ns |  0.408 ns |  0.243 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.95 ns |  0.017 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.54 ns |  1.987 ns |  1.314 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.99 ns |  0.050 ns |  0.030 ns |  0.53 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     298.9 ns |   0.44 ns |   0.26 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 109,115.7 ns | 446.36 ns | 265.62 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     217.0 ns |   0.48 ns |   0.29 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,335.1 ns |  72.27 ns |  43.01 ns |      - |      80 B |

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