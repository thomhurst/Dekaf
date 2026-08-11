---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-11 12:07 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 100× less | Mixed |
| Consume — drain a topic | 1.6× slower to on par | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.9× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 0.94–1.28 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 1.00–1.42 | 34% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.89 | 0.72–1.08 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.62 | 1.47–1.87 | 25% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 24% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.24–0.29 | 20% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.91 | 0.81–1.26 | 49% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.91–1.20 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.80 | 0.74–0.95 | 26% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.79 | 0.69–0.85 | 19% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.46 | 13% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.05 | 0.99–1.51 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 41% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 45% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 42% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,056.9 μs** |   **256.61 μs** | **169.73 μs** |  **1.00** |    **0.04** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,572.5 μs |    45.59 μs |  30.15 μs |  0.43 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,297.0 μs** |    **43.00 μs** |  **25.59 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,739.6 μs |   130.46 μs |  86.29 μs |  0.51 |    0.01 |        - |       - |   51828 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,663.7 μs** |    **30.37 μs** |  **18.08 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,711.7 μs |    53.93 μs |  32.10 μs |  0.41 |    0.00 |        - |       - |    7846 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,042.9 μs** |   **341.18 μs** | **225.67 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,455.3 μs | 1,564.41 μs | 818.22 μs |  1.04 |    0.07 |        - |       - |   70010 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **111.1 μs** |     **4.07 μs** |   **2.13 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    108.4 μs |    20.68 μs |  13.68 μs |  0.98 |    0.12 |        - |       - |     156 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,089.4 μs** |   **115.79 μs** |  **76.59 μs** |  **1.00** |    **0.09** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,292.8 μs |   367.39 μs | 243.01 μs |  1.19 |    0.23 |        - |       - |    4519 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **889.8 μs** |     **7.78 μs** |   **4.07 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121291 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    807.3 μs |    99.90 μs |  66.08 μs |  0.91 |    0.07 |        - |       - |    1723 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,035.7 μs** |   **142.10 μs** |  **93.99 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1212764 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,740.4 μs |   807.08 μs | 533.84 μs |  0.75 |    0.06 |        - |       - |   16683 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,493.0 μs** |    **21.28 μs** |  **14.08 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    251.5 μs |    13.45 μs |   8.90 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,481.5 μs** |    **26.89 μs** |  **14.06 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    253.0 μs |     6.57 μs |   4.35 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,485.6 μs** |    **17.40 μs** |  **11.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    258.8 μs |     4.50 μs |   2.98 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,482.1 μs** |    **25.34 μs** |  **16.76 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    265.9 μs |    10.46 μs |   6.22 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **112.4 μs** |    **35.88 μs** |  **18.77 μs** |   **102.5 μs** |  **1.02** |    **0.22** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   130.5 μs |     6.05 μs |   2.69 μs |   130.7 μs |  1.19 |    0.17 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **149.6 μs** |    **73.97 μs** |  **38.69 μs** |   **131.3 μs** |  **1.05** |    **0.34** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   186.8 μs |     6.57 μs |   2.92 μs |   186.5 μs |  1.31 |    0.27 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **955.7 μs** |   **607.41 μs** | **317.69 μs** |   **753.1 μs** |  **1.08** |    **0.44** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   684.0 μs |   142.52 μs |  63.28 μs |   653.6 μs |  0.77 |    0.20 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,423.9 μs** | **1,035.44 μs** | **541.55 μs** | **1,246.9 μs** |  **1.12** |    **0.54** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,949.7 μs | 1,372.32 μs | 609.32 μs | 2,298.0 μs |  1.53 |    0.66 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,493.8 ns** |  **14.99 ns** |  **7.84 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   576.2 ns | 105.36 ns | 69.69 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |           |          |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,662.6 ns** |  **66.16 ns** | **43.76 ns** |  **1.00** |    **0.02** | **0.1450** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        |   974.5 ns |  78.42 ns | 41.02 ns |  0.27 |    0.01 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 439.07 ns | 2.732 ns | 0.423 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.15 ns | 0.369 ns | 0.057 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.72 ns | 0.272 ns | 0.071 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.48 ns | 0.335 ns | 0.052 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.074 μs** | **0.0028 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **1**       | **2.076 μs** | **0.0042 μs** | **0.0011 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.400 μs** | **0.0100 μs** | **0.0026 μs** |         **-** |
| **WriteRequest** | **9**       | **2.386 μs** | **0.0269 μs** | **0.0042 μs** |         **-** |
| **WriteRequest** | **10**      | **2.380 μs** | **0.0043 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **11**      | **2.400 μs** | **0.0357 μs** | **0.0055 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.29 ns** | **0.606 ns** | **0.157 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.48 ns | 0.403 ns | 0.062 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.42 ns** | **0.593 ns** | **0.092 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  89.21 ns | 0.270 ns | 0.070 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.0 ns |  0.76 ns | 0.50 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,149.0 ns |  7.06 ns | 3.69 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,334.5 ns | 12.82 ns | 7.63 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,249.3 ns |  2.48 ns | 1.30 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,097.8 ns |  3.89 ns | 2.32 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,975.6 ns |  5.20 ns | 3.44 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,963.5 ns |  4.48 ns | 2.96 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,733.6 ns |  3.54 ns | 2.11 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.5 ns |  1.19 ns | 0.62 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,815.7 ns |  5.08 ns | 3.02 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   768.4 ns |  0.61 ns | 0.36 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   810.4 ns |  4.11 ns | 2.45 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   169.9 ns |  0.20 ns | 0.13 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,718.0 ns |  3.04 ns | 2.01 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,323.9 ns |  1.01 ns | 0.67 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,737.78 ns | 170.198 ns | 112.575 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.21 ns |   0.222 ns |   0.147 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     16.82 ns |   0.215 ns |   0.128 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     36.08 ns |   0.350 ns |   0.208 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.03 ns |   0.436 ns |   0.260 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.26 ns |   0.186 ns |   0.123 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    104.91 ns |   2.312 ns |   1.529 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     53.99 ns |   0.319 ns |   0.190 ns |  0.51 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     297.9 ns |   1.49 ns |   0.78 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 104,725.8 ns |  68.80 ns |  45.51 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     209.7 ns |   0.30 ns |   0.18 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,223.1 ns | 413.71 ns | 216.38 ns |      - |      80 B |

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