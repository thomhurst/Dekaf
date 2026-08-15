---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-15 00:29 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 21×–22× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 25× less | Stable |
| Produce — fire-and-forget | on par to 1.2× faster | 1000× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.2× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.8×–10× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.16 | 1.00–1.33 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.30 | 1.06–1.51 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.73–1.02 | 36% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 1.17–2.40 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.10 | 21% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.26 | 0.18–0.43 | 96% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.92 | 0.74–1.42 | 74% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.01 | 0.89–1.05 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.73–0.93 | 25% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.81 | 0.75–1.05 | 37% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.46 | 14% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.14 | 1.00–1.28 | 25% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 47% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 51% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 47% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 51% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,955.85 μs** |   **148.621 μs** |  **98.304 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,555.42 μs |    23.347 μs |  15.442 μs |  0.43 |    0.01 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,302.21 μs** |    **43.329 μs** |  **25.784 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,613.69 μs |    98.947 μs |  58.882 μs |  0.49 |    0.01 |        - |       - |   49759 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,654.22 μs** |    **14.303 μs** |   **8.512 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,769.11 μs |    73.940 μs |  38.672 μs |  0.42 |    0.01 |        - |       - |    6313 B |        0.03 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,261.15 μs** |   **360.229 μs** | **238.270 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,562.21 μs | 1,494.666 μs | 889.452 μs |  1.13 |    0.09 |        - |       - |   51890 B |        0.03 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **102.88 μs** |     **0.856 μs** |   **0.509 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     89.70 μs |     9.282 μs |   4.855 μs |  0.87 |    0.04 |        - |       - |      37 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,080.12 μs** |   **146.842 μs** |  **97.127 μs** |  **1.01** |    **0.14** |  **17.5781** |       **-** |  **304004 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,031.87 μs |   228.134 μs | 150.896 μs |  0.96 |    0.17 |        - |       - |     423 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **865.86 μs** |    **11.419 μs** |   **5.972 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121258 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    797.06 μs |   134.402 μs |  88.898 μs |  0.92 |    0.10 |        - |       - |     369 B |       0.003 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,580.98 μs** |   **135.906 μs** |  **80.876 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1212299 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,108.99 μs | 1,134.020 μs | 674.837 μs |  0.83 |    0.07 |        - |       - |    1004 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,502.51 μs** |    **31.076 μs** |  **20.555 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    239.41 μs |     8.267 μs |   5.468 μs |  0.04 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,485.97 μs** |    **13.642 μs** |   **8.118 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    241.75 μs |     5.965 μs |   3.945 μs |  0.04 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,490.57 μs** |    **13.917 μs** |   **9.205 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    248.85 μs |     8.177 μs |   5.408 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,494.54 μs** |    **16.071 μs** |  **10.630 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    250.49 μs |     6.776 μs |   4.482 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **121.2 μs** |    **55.94 μs** |  **29.26 μs** |   **114.6 μs** |  **1.05** |    **0.32** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   128.9 μs |     9.85 μs |   4.37 μs |   129.7 μs |  1.11 |    0.22 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **141.0 μs** |    **74.07 μs** |  **38.74 μs** |   **119.0 μs** |  **1.06** |    **0.36** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   171.4 μs |    33.49 μs |  17.51 μs |   169.6 μs |  1.28 |    0.30 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **996.9 μs** |   **571.33 μs** | **298.82 μs** |   **936.8 μs** |  **1.07** |    **0.41** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   759.2 μs |   150.40 μs |  66.78 μs |   787.7 μs |  0.82 |    0.22 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,235.6 μs** |   **772.56 μs** | **343.02 μs** |   **985.0 μs** |  **1.06** |    **0.38** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,676.7 μs | 1,499.40 μs | 665.74 μs | 1,153.4 μs |  1.44 |    0.65 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,493.1 ns** |    **17.24 ns** |     **9.02 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   542.4 ns |   129.15 ns |    85.43 ns |  0.10 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **4,165.3 ns** | **2,211.62 ns** | **1,462.85 ns** |  **1.19** |    **0.82** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,085.1 ns |   206.39 ns |   122.82 ns |  0.31 |    0.18 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 464.10 ns | 8.042 ns | 2.088 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.83 ns | 0.180 ns | 0.047 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.61 ns | 0.175 ns | 0.046 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.49 ns | 0.166 ns | 0.043 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.074 μs** | **0.0079 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **1**       | **2.073 μs** | **0.0052 μs** | **0.0008 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.467 μs** | **0.0145 μs** | **0.0022 μs** |         **-** |
| **WriteRequest** | **9**       | **2.446 μs** | **0.0240 μs** | **0.0037 μs** |         **-** |
| **WriteRequest** | **10**      | **2.582 μs** | **0.0442 μs** | **0.0068 μs** |         **-** |
| **WriteRequest** | **11**      | **2.877 μs** | **0.0121 μs** | **0.0019 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.65 ns** | **1.437 ns** | **0.222 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  96.29 ns | 0.399 ns | 0.104 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **100.24 ns** | **0.922 ns** | **0.239 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  96.08 ns | 0.373 ns | 0.097 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,747.3 ns |  7.37 ns |  4.87 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,078.2 ns |  9.72 ns |  5.79 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,421.8 ns | 27.45 ns | 14.36 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,387.7 ns |  2.83 ns |  1.48 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,206.2 ns |  2.11 ns |  1.26 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,001.4 ns |  3.34 ns |  1.99 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,928.6 ns |  2.27 ns |  1.35 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,894.9 ns | 12.96 ns |  7.71 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,194.6 ns |  1.67 ns |  1.00 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,041.6 ns |  3.03 ns |  1.58 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   768.2 ns |  0.89 ns |  0.59 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   891.2 ns |  1.05 ns |  0.55 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   132.6 ns |  0.10 ns |  0.06 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,768.5 ns | 31.46 ns | 20.81 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,197.7 ns |  1.11 ns |  0.58 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   3.426 ns | 0.0143 ns | 0.0134 ns |  1.00 |    0.01 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 240.491 ns | 0.3125 ns | 0.2609 ns | 70.20 |    0.28 |         - |          NA |

| Method                               | Categories | Mean         | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,671.78 ns | 7.449 ns | 4.433 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     14.26 ns | 0.088 ns | 0.058 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.53 ns | 0.049 ns | 0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     36.71 ns | 0.102 ns | 0.068 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     42.45 ns | 0.862 ns | 0.570 ns |     ? |       ? | 0.0089 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     10.65 ns | 0.049 ns | 0.032 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    149.74 ns | 4.174 ns | 2.761 ns |  1.00 |    0.02 | 0.0355 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     59.22 ns | 0.024 ns | 0.014 ns |  0.40 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|---------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     298.2 ns |  0.74 ns |  0.44 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 104,676.1 ns | 67.21 ns | 44.45 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     210.1 ns |  0.52 ns |  0.31 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,101.5 ns | 91.22 ns | 60.34 ns |      - |      80 B |

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