---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-09 13:18 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 118× less | Mixed |
| Consume — drain a topic | 1.6× slower to on par | 1.6× less | Mixed |
| Consume — poll a single message | 3.5×–10× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 0.94–1.46 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 1.00–1.37 | 30% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.89 | 0.70–1.08 | 42% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.62 | 1.47–1.88 | 25% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 28% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.29 | 0.24–0.32 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.92 | 0.81–1.26 | 49% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.91–1.11 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.80 | 0.74–0.90 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.79 | 0.69–0.85 | 19% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.42–0.46 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.05 | 0.99–1.51 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 41% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 42% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 45% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 43% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,233.7 μs** |   **161.06 μs** | **106.53 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,690.3 μs |    24.15 μs |  14.37 μs |  0.43 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,659.3 μs** |    **94.38 μs** |  **56.16 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,762.8 μs |   118.17 μs |  70.32 μs |  0.49 |    0.01 |        - |       - |   51872 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,382.3 μs** |    **63.60 μs** |  **42.07 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,852.8 μs |    89.78 μs |  59.38 μs |  0.45 |    0.01 |        - |       - |    7801 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,203.3 μs** |   **375.92 μs** | **248.65 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,989.3 μs | 1,115.52 μs | 663.83 μs |  1.06 |    0.06 |        - |       - |   69803 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.1 μs** |     **1.03 μs** |   **0.68 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    122.3 μs |    23.23 μs |  15.37 μs |  0.98 |    0.12 |        - |       - |     198 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,197.5 μs** |   **211.93 μs** | **126.12 μs** |  **1.01** |    **0.16** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,244.2 μs |   124.17 μs |  82.13 μs |  1.05 |    0.14 |        - |       - |    2183 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,014.8 μs** |    **16.43 μs** |  **10.87 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121481 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    741.3 μs |   112.19 μs |  66.76 μs |  0.73 |    0.06 |        - |       - |    1784 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,147.4 μs** |   **132.69 μs** |  **69.40 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214815 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,706.2 μs |   702.02 μs | 417.76 μs |  0.76 |    0.04 |        - |       - |   19322 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,493.5 μs** |    **21.40 μs** |  **14.15 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    306.8 μs |     5.83 μs |   3.86 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,487.0 μs** |     **5.75 μs** |   **3.01 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    302.6 μs |     7.74 μs |   5.12 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,489.9 μs** |     **9.23 μs** |   **6.10 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    318.8 μs |    17.57 μs |  11.62 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,508.3 μs** |    **24.09 μs** |  **15.93 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    310.7 μs |     5.64 μs |   3.35 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **125.6 μs** |    **41.35 μs** |  **21.63 μs** |   **114.6 μs** |  **1.02** |    **0.23** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   151.2 μs |    54.13 μs |  28.31 μs |   135.8 μs |  1.23 |    0.29 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **161.1 μs** |    **74.14 μs** |  **38.78 μs** |   **137.4 μs** |  **1.04** |    **0.31** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   174.3 μs |    29.58 μs |  13.13 μs |   175.2 μs |  1.13 |    0.23 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,102.7 μs** |   **539.15 μs** | **281.99 μs** | **1,026.8 μs** |  **1.06** |    **0.36** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   761.7 μs |    86.27 μs |  38.31 μs |   739.6 μs |  0.73 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,349.6 μs** |   **838.14 μs** | **438.36 μs** | **1,077.2 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,721.2 μs | 1,196.36 μs | 531.19 μs | 2,013.5 μs |  1.37 |    0.52 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,572.9 ns** |    **23.04 ns** |    **12.05 ns** | **5,577.1 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   576.2 ns |   127.19 ns |    84.13 ns |   566.5 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,071.7 ns** | **2,324.88 ns** | **1,537.76 ns** | **3,727.2 ns** |  **1.29** |    **0.95** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,075.5 ns |   146.38 ns |    96.82 ns | 1,017.7 ns |  0.45 |    0.23 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 453.58 ns | 2.603 ns | 0.676 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  28.95 ns | 0.076 ns | 0.012 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.57 ns | 0.081 ns | 0.021 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.47 ns | 0.165 ns | 0.043 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.116 μs** | **0.0057 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **1**       | **2.073 μs** | **0.0034 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.393 μs** | **0.0034 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **9**       | **2.429 μs** | **0.0098 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **10**      | **2.487 μs** | **0.0086 μs** | **0.0022 μs** |         **-** |
| **WriteRequest** | **11**      | **2.519 μs** | **0.0053 μs** | **0.0008 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.49 ns** | **0.365 ns** | **0.057 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  94.31 ns | 0.597 ns | 0.155 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.05 ns** | **0.221 ns** | **0.057 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.16 ns | 0.158 ns | 0.024 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.6 ns | 1.32 ns | 0.69 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,944.9 ns | 1.84 ns | 1.09 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,356.5 ns | 8.56 ns | 5.10 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,248.8 ns | 1.97 ns | 1.30 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,935.7 ns | 2.09 ns | 1.24 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,994.5 ns | 5.53 ns | 3.29 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,941.0 ns | 5.75 ns | 3.42 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,829.8 ns | 3.79 ns | 2.25 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.7 ns | 0.77 ns | 0.40 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.8 ns | 4.95 ns | 3.27 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   796.7 ns | 0.42 ns | 0.22 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   832.2 ns | 4.02 ns | 2.66 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   169.7 ns | 0.07 ns | 0.04 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,730.0 ns | 4.53 ns | 2.70 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,301.6 ns | 1.05 ns | 0.55 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,518.01 ns | 11.817 ns | 7.816 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |  0.022 ns | 0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.74 ns |  0.013 ns | 0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     43.82 ns |  0.453 ns | 0.300 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.69 ns |  0.290 ns | 0.192 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.015 ns | 0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.93 ns |  3.654 ns | 2.417 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.22 ns |  0.228 ns | 0.136 ns |  0.53 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|---------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     296.8 ns |  0.33 ns |  0.17 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 104,721.6 ns | 87.88 ns | 52.30 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     211.8 ns |  0.38 ns |  0.23 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,067.6 ns | 83.06 ns | 43.44 ns |      - |      80 B |

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