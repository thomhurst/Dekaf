---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 15:34 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18×–19× faster | 3.0× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 250× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–10× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.10 | 0.93–1.33 | 36% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 1.16–1.50 | 27% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.74 | 0.71–1.02 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.79 | 0.98–2.40 | 79% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 24% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.18–0.29 | 41% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.89 | 0.74–1.42 | 77% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.95 | 0.89–1.04 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.79 | 0.73–0.91 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.78 | 0.69–1.05 | 45% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 1.00–1.28 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 41% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 43% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 41% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 44% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,738.12 μs** |   **182.208 μs** |   **108.429 μs** |  **1.00** |    **0.03** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,448.67 μs |    29.091 μs |    17.311 μs |  0.43 |    0.01 |        - |       - |    5402 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **6,805.87 μs** |   **100.994 μs** |    **66.801 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,505.98 μs |   108.089 μs |    71.494 μs |  0.52 |    0.01 |        - |       - |   50304 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,330.34 μs** |    **33.062 μs** |    **19.674 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,564.70 μs |    83.423 μs |    49.644 μs |  0.41 |    0.01 |        - |       - |    6797 B |        0.03 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **9,269.07 μs** | **1,837.221 μs** | **1,215.208 μs** |  **1.01** |    **0.17** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,453.97 μs | 1,664.728 μs | 1,101.114 μs |  1.25 |    0.19 |        - |       - |   56109 B |        0.03 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |     **81.57 μs** |     **3.676 μs** |     **2.431 μs** |  **1.00** |    **0.04** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    114.17 μs |    41.801 μs |    27.649 μs |  1.40 |    0.33 |        - |       - |      74 B |       0.002 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |    **807.25 μs** |    **79.239 μs** |    **52.412 μs** |  **1.00** |    **0.10** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    825.16 μs |   245.464 μs |   162.359 μs |  1.03 |    0.21 |        - |       - |     799 B |       0.003 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **734.09 μs** |    **45.283 μs** |    **26.947 μs** |  **1.00** |    **0.05** |   **7.0801** |       **-** |  **121186 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    700.49 μs |   194.994 μs |   128.976 μs |  0.96 |    0.17 |        - |       - |     658 B |       0.005 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,959.04 μs** | **1,847.996 μs** | **1,222.335 μs** |  **1.02** |    **0.20** |  **72.2656** |       **-** | **1209783 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,873.83 μs | 3,355.786 μs | 1,996.974 μs |  1.01 |    0.28 |        - |       - |   14968 B |        0.01 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,426.99 μs** |    **65.178 μs** |    **34.089 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1204 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    191.84 μs |    17.924 μs |     9.375 μs |  0.04 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,531.54 μs** |   **148.902 μs** |    **88.609 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    184.78 μs |     4.259 μs |     2.817 μs |  0.03 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,489.39 μs** |    **28.194 μs** |    **14.746 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    193.27 μs |     9.382 μs |     4.907 μs |  0.04 |    0.00 |        - |       - |     512 B |        0.24 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,459.10 μs** |    **97.188 μs** |    **57.835 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    195.31 μs |    14.080 μs |     9.313 μs |  0.04 |    0.00 |        - |       - |     512 B |        0.24 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean        | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |------------:|-----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |    **87.34 μs** |  **29.137 μs** |  **15.239 μs** |  **1.02** |    **0.23** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   105.99 μs |   7.976 μs |   4.172 μs |  1.24 |    0.19 |  26.45 KB |        0.41 | Stable |
|                      |              |             |             |            |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **111.90 μs** |  **47.053 μs** |  **20.892 μs** |  **1.03** |    **0.24** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   127.13 μs |  18.133 μs |   9.484 μs |  1.17 |    0.19 | 202.23 KB |        0.84 | Stable |
|                      |              |             |             |            |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **790.06 μs** | **484.353 μs** | **253.326 μs** |  **1.08** |    **0.44** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   555.09 μs | 155.043 μs |  68.840 μs |  0.76 |    0.22 | 258.48 KB |        0.40 | Stable |
|                      |              |             |             |            |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,138.57 μs** | **843.344 μs** | **441.085 μs** |  **1.12** |    **0.55** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,679.36 μs | 866.364 μs | 384.671 μs |  1.65 |    0.63 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,408.5 ns** |    **25.84 ns** |    **13.52 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   458.6 ns |   101.04 ns |    66.83 ns |  0.08 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **4,411.6 ns** | **2,454.09 ns** | **1,623.23 ns** |  **1.25** |    **0.98** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   857.7 ns |   108.17 ns |    71.55 ns |  0.24 |    0.16 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 387.80 ns | 15.962 ns | 4.145 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  16.03 ns |  0.362 ns | 0.094 ns |      - |         - |
| WriteDescribeGroupsV6      |  28.62 ns |  0.384 ns | 0.059 ns |      - |         - |
| WriteListConfigResourcesV1 |  15.22 ns |  0.435 ns | 0.113 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.515 μs** | **0.0882 μs** | **0.0229 μs** |         **-** |
| **WriteRequest** | **1**       | **1.534 μs** | **0.2010 μs** | **0.0522 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.387 μs** | **0.0070 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **9**       | **2.398 μs** | **0.0179 μs** | **0.0047 μs** |         **-** |
| **WriteRequest** | **10**      | **2.409 μs** | **0.0120 μs** | **0.0031 μs** |         **-** |
| **WriteRequest** | **11**      | **2.431 μs** | **0.0046 μs** | **0.0012 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **101.93 ns** | **0.398 ns** | **0.062 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  92.45 ns | 0.327 ns | 0.051 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.91 ns** | **4.957 ns** | **1.287 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.78 ns | 0.848 ns | 0.131 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,638.1 ns |  3.17 ns |  1.89 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,085.0 ns |  5.97 ns |  3.56 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,295.9 ns |  3.26 ns |  2.15 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,373.4 ns |  3.34 ns |  1.75 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,061.6 ns |  3.56 ns |  1.86 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,921.1 ns |  2.91 ns |  1.52 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,039.8 ns |  2.43 ns |  1.44 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,734.8 ns |  4.09 ns |  2.44 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,119.2 ns |  3.31 ns |  2.19 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.6 ns |  5.59 ns |  3.33 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   753.9 ns | 10.90 ns |  7.21 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   846.5 ns |  2.67 ns |  1.76 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   153.3 ns |  0.09 ns |  0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,719.5 ns | 16.07 ns | 10.63 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,277.4 ns |  1.07 ns |  0.64 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,700.72 ns | 43.543 ns | 25.912 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.23 ns |  0.011 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.68 ns |  0.019 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.73 ns |  0.032 ns |  0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.21 ns |  0.041 ns |  0.027 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     14.40 ns |  0.028 ns |  0.018 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    103.48 ns |  0.643 ns |  0.336 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.09 ns |  0.040 ns |  0.026 ns |  0.52 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error       | StdDev      | Gen0   | Allocated |
|------------------------ |------------:|------------:|------------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    229.2 ns |     7.98 ns |     4.75 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 65,536.8 ns | 1,184.14 ns |   783.24 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    151.5 ns |     3.47 ns |     2.30 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 86,083.8 ns | 1,761.00 ns | 1,047.94 ns |      - |      80 B |

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