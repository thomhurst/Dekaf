---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 14:51 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18×–19× faster | 3.0× less | Stable |
| Produce — batches | on par to 2.3× faster | 25× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 182× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–10× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.10 | 0.93–1.20 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 1.16–1.50 | 27% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.76 | 0.71–1.02 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.80 | 0.98–2.40 | 79% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.09–0.11 | 17% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.25–0.29 | 14% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.88 | 0.74–1.12 | 43% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.95 | 0.89–1.09 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.79 | 0.73–0.86 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.78 | 0.69–0.85 | 20% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.47 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 1.00–1.17 | 16% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.05–0.06 | 23% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 23% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 23% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.05–0.06 | 25% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,116.8 μs** |   **151.26 μs** | **100.05 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,679.4 μs |    13.10 μs |   7.80 μs |  0.44 |    0.01 |        - |       - |    5400 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,602.5 μs** |   **117.97 μs** |  **78.03 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,756.1 μs |   111.57 μs |  73.80 μs |  0.49 |    0.01 |        - |       - |   50359 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,299.3 μs** |    **51.90 μs** |  **34.33 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,881.9 μs |    36.00 μs |  23.81 μs |  0.46 |    0.00 |        - |       - |    6759 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,463.8 μs** |   **189.59 μs** | **112.82 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,171.9 μs | 1,384.60 μs | 915.83 μs |  0.98 |    0.07 |        - |       - |   56169 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.3 μs** |    **15.61 μs** |  **10.33 μs** |  **1.01** |    **0.11** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    110.9 μs |    27.10 μs |  17.92 μs |  0.81 |    0.14 |        - |       - |      65 B |       0.002 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,269.8 μs** |    **13.69 μs** |   **8.15 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,146.1 μs |   187.78 μs | 111.75 μs |  0.90 |    0.08 |        - |       - |    1438 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,031.9 μs** |     **9.42 μs** |   **5.61 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121515 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    826.6 μs |   126.40 μs |  83.61 μs |  0.80 |    0.08 |        - |       - |     770 B |       0.006 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,504.1 μs** |   **175.10 μs** | **104.20 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215197 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,144.4 μs |   780.20 μs | 516.05 μs |  0.78 |    0.05 |        - |       - |    7196 B |       0.006 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,544.1 μs** |    **28.07 μs** |  **18.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    320.1 μs |     7.04 μs |   4.66 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,535.2 μs** |    **13.94 μs** |   **9.22 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    314.5 μs |     6.93 μs |   4.58 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,560.8 μs** |    **23.58 μs** |  **15.59 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    315.5 μs |     7.12 μs |   4.23 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.24 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,541.3 μs** |    **38.88 μs** |  **25.72 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    328.2 μs |    17.69 μs |  11.70 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.24 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **132.7 μs** |    **43.28 μs** |  **22.64 μs** |   **123.2 μs** |  **1.02** |    **0.23** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   127.4 μs |     3.69 μs |   1.64 μs |   127.8 μs |  0.98 |    0.15 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **146.1 μs** |    **47.21 μs** |  **20.96 μs** |   **137.4 μs** |  **1.01** |    **0.18** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   165.6 μs |     7.36 μs |   3.27 μs |   164.1 μs |  1.15 |    0.13 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,135.2 μs** |   **535.41 μs** | **280.03 μs** | **1,061.5 μs** |  **1.05** |    **0.35** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   783.7 μs |   116.79 μs |  51.86 μs |   770.8 μs |  0.73 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,395.9 μs** |   **933.65 μs** | **488.32 μs** | **1,089.8 μs** |  **1.09** |    **0.46** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,683.0 μs | 1,081.80 μs | 480.33 μs | 1,919.3 μs |  1.31 |    0.49 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,569.8 ns** |    **10.66 ns** |     **5.57 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   567.8 ns |   171.56 ns |   113.48 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,368.5 ns** | **2,758.97 ns** | **1,824.89 ns** |  **1.36** |    **1.09** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,030.4 ns |   225.65 ns |   149.25 ns |  0.41 |    0.23 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev    | Gen0   | Allocated |
|--------------------------- |----------:|----------:|----------:|-------:|----------:|
| ReadDescribeGroupsV5       | 536.65 ns | 69.661 ns | 18.091 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.62 ns |  0.206 ns |  0.053 ns |      - |         - |
| WriteDescribeGroupsV6      |  46.47 ns |  0.366 ns |  0.057 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.17 ns |  0.085 ns |  0.022 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.995 μs** | **0.0493 μs** | **0.0128 μs** |         **-** |
| **WriteRequest** | **1**       | **2.000 μs** | **0.0026 μs** | **0.0004 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.392 μs** | **0.0147 μs** | **0.0023 μs** |         **-** |
| **WriteRequest** | **9**       | **2.405 μs** | **0.0129 μs** | **0.0034 μs** |         **-** |
| **WriteRequest** | **10**      | **2.512 μs** | **0.0156 μs** | **0.0024 μs** |         **-** |
| **WriteRequest** | **11**      | **2.394 μs** | **0.0078 μs** | **0.0012 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **107.00 ns** | **0.372 ns** | **0.058 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  96.16 ns | 0.291 ns | 0.045 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.08 ns** | **0.317 ns** | **0.082 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  88.67 ns | 0.370 ns | 0.057 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.8 ns |  1.37 ns | 0.81 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,060.6 ns |  1.03 ns | 0.54 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,272.0 ns |  1.37 ns | 0.72 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 3,185.4 ns |  1.81 ns | 1.20 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,920.8 ns |  2.02 ns | 1.34 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,968.3 ns | 11.46 ns | 7.58 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,970.3 ns |  3.26 ns | 1.94 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,740.0 ns |  2.72 ns | 1.62 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,116.9 ns |  0.71 ns | 0.42 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.1 ns |  3.93 ns | 2.60 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   788.6 ns |  2.92 ns | 1.74 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   843.9 ns |  1.45 ns | 0.96 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   141.0 ns |  0.07 ns | 0.04 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,642.3 ns |  3.09 ns | 1.84 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,294.2 ns |  4.47 ns | 2.95 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,680.19 ns | 5.079 ns | 3.023 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     14.20 ns | 0.056 ns | 0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.71 ns | 0.025 ns | 0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     35.92 ns | 0.024 ns | 0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     39.42 ns | 2.114 ns | 1.398 ns |     ? |       ? | 0.0089 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.08 ns | 0.045 ns | 0.030 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    125.71 ns | 4.818 ns | 3.187 ns |  1.00 |    0.03 | 0.0355 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     59.76 ns | 0.047 ns | 0.028 ns |  0.48 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error       | StdDev      | Gen0   | Allocated |
|------------------------ |-------------:|------------:|------------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     291.8 ns |     0.81 ns |     0.48 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,397.6 ns |   344.27 ns |   204.87 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     224.1 ns |     1.13 ns |     0.68 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 124,301.9 ns | 2,270.61 ns | 1,501.87 ns |      - |      80 B |

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