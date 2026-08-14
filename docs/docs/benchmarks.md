---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 17:14 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 3.2× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 500× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.8×–10× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.10 | 0.93–1.33 | 36% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.21 | 1.06–1.50 | 36% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.74 | 0.71–1.02 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.79 | 0.98–2.35 | 76% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 24% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.26 | 0.18–0.29 | 41% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.74–1.42 | 76% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.95 | 0.89–1.04 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.79 | 0.73–0.91 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.79 | 0.75–1.05 | 38% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 1.00–1.28 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 40% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 43% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.04–0.06 | 40% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.04–0.06 | 43% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,093.5 μs** |    **85.19 μs** |  **56.35 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,698.6 μs |    21.14 μs |  13.99 μs |  0.44 |    0.00 |        - |       - |    5368 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,621.6 μs** |    **67.02 μs** |  **39.88 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,790.9 μs |   147.03 μs |  87.49 μs |  0.50 |    0.01 |        - |       - |   50063 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,298.8 μs** |    **65.60 μs** |  **43.39 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,749.1 μs |    43.78 μs |  28.96 μs |  0.44 |    0.01 |        - |       - |    6717 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,246.4 μs** |   **126.84 μs** |  **83.89 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,345.1 μs | 1,075.65 μs | 562.58 μs |  1.01 |    0.04 |        - |       - |   55194 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **126.3 μs** |     **1.88 μs** |   **1.25 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    109.8 μs |    16.31 μs |  10.79 μs |  0.87 |    0.08 |        - |       - |      42 B |       0.001 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,285.6 μs** |    **12.50 μs** |   **8.27 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,194.6 μs |   133.79 μs |  88.49 μs |  0.93 |    0.07 |        - |       - |     590 B |       0.002 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,045.2 μs** |     **7.53 μs** |   **4.98 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121525 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    808.4 μs |    95.13 μs |  62.92 μs |  0.77 |    0.06 |        - |       - |     350 B |       0.003 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,404.0 μs** |   **127.19 μs** |  **66.52 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214953 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,337.7 μs |   861.55 μs | 569.86 μs |  0.80 |    0.05 |        - |       - |    2483 B |       0.002 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,508.6 μs** |     **8.89 μs** |   **5.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    317.7 μs |    15.19 μs |  10.05 μs |  0.06 |    0.00 |        - |       - |     480 B |        0.40 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,511.9 μs** |    **15.21 μs** |   **9.05 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    311.4 μs |    15.36 μs |   9.14 μs |  0.06 |    0.00 |        - |       - |     480 B |        0.40 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,518.7 μs** |    **13.92 μs** |   **8.28 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    316.4 μs |    13.86 μs |   9.17 μs |  0.06 |    0.00 |        - |       - |     480 B |        0.23 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,522.4 μs** |    **13.39 μs** |   **7.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    320.6 μs |    16.43 μs |  10.87 μs |  0.06 |    0.00 |        - |       - |     480 B |        0.23 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **134.3 μs** |    **47.47 μs** |  **24.83 μs** |   **119.5 μs** |  **1.03** |    **0.24** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   141.5 μs |     4.78 μs |   2.12 μs |   140.9 μs |  1.08 |    0.17 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **161.5 μs** |    **46.49 μs** |  **24.32 μs** |   **164.4 μs** |  **1.02** |    **0.20** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   175.1 μs |    30.13 μs |  13.38 μs |   175.0 μs |  1.11 |    0.17 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,115.2 μs** |   **583.59 μs** | **305.23 μs** | **1,021.2 μs** |  **1.07** |    **0.38** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   810.4 μs |   211.02 μs |  93.70 μs |   789.6 μs |  0.77 |    0.21 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,402.7 μs** |   **877.56 μs** | **458.98 μs** | **1,117.6 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,796.1 μs | 1,150.49 μs | 510.82 μs | 2,030.3 μs |  1.38 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,573.5 ns** |    **22.18 ns** |    **11.60 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   574.7 ns |   159.70 ns |   105.63 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **4,014.4 ns** | **2,570.03 ns** | **1,699.92 ns** |  **1.27** |    **0.95** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   963.2 ns |    43.16 ns |    22.57 ns |  0.30 |    0.18 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 355.83 ns | 0.495 ns | 0.129 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  23.00 ns | 0.137 ns | 0.036 ns |      - |         - |
| WriteDescribeGroupsV6      |  35.20 ns | 0.263 ns | 0.068 ns |      - |         - |
| WriteListConfigResourcesV1 |  15.08 ns | 0.012 ns | 0.002 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.609 μs** | **0.0025 μs** | **0.0004 μs** |         **-** |
| **WriteRequest** | **1**       | **1.611 μs** | **0.0010 μs** | **0.0003 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.378 μs** | **0.0112 μs** | **0.0029 μs** |         **-** |
| **WriteRequest** | **9**       | **2.402 μs** | **0.0067 μs** | **0.0010 μs** |         **-** |
| **WriteRequest** | **10**      | **2.382 μs** | **0.0324 μs** | **0.0084 μs** |         **-** |
| **WriteRequest** | **11**      | **2.403 μs** | **0.0104 μs** | **0.0016 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **128.51 ns** | **0.408 ns** | **0.063 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  99.16 ns | 0.197 ns | 0.051 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.68 ns** | **0.531 ns** | **0.138 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  85.76 ns | 0.492 ns | 0.076 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,637.2 ns | 4.81 ns | 3.18 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,911.5 ns | 2.00 ns | 1.32 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,290.9 ns | 2.97 ns | 1.77 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,522.6 ns | 1.75 ns | 1.16 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,023.5 ns | 1.62 ns | 0.85 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,002.4 ns | 6.22 ns | 3.25 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,880.5 ns | 6.23 ns | 3.71 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,735.5 ns | 1.47 ns | 0.77 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,116.7 ns | 0.63 ns | 0.33 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,815.1 ns | 2.91 ns | 1.92 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   782.1 ns | 3.90 ns | 2.58 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   838.1 ns | 2.50 ns | 1.66 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   142.9 ns | 0.08 ns | 0.04 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,711.2 ns | 6.07 ns | 4.02 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,271.4 ns | 0.91 ns | 0.48 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,819.61 ns | 36.934 ns | 21.979 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.15 ns |  0.023 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.69 ns |  0.049 ns |  0.026 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.58 ns |  0.068 ns |  0.040 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.18 ns |  0.403 ns |  0.267 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.98 ns |  0.011 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.86 ns |  1.087 ns |  0.647 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.42 ns |  0.073 ns |  0.044 ns |  0.49 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    232.5 ns |   0.22 ns |  0.13 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 81,150.5 ns | 116.58 ns | 77.11 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    160.3 ns |   0.20 ns |  0.11 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 80,151.3 ns |  75.80 ns | 45.11 ns |      - |      80 B |

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