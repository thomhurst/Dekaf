---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 11:03 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18×–19× faster | 3.0× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 250× less | ⚠ Noisy |
| Consume — drain a topic | 1.7× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–10× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.12 | 0.93–1.20 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.21 | 0.97–1.39 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–1.02 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.70 | 0.98–2.40 | 84% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 53% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 58% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.88 | 0.75–1.12 | 43% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.92 | 0.74–1.09 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.76–1.25 | 61% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.69–1.56 | 114% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 0.99–1.52 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 58% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 61% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 64% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,074.3 μs** |  **41.33 μs** |  **21.61 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,681.5 μs |  14.60 μs |   8.69 μs |  0.44 |    0.00 |        - |       - |    5400 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,568.3 μs** |  **84.02 μs** |  **55.58 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,791.0 μs |  90.98 μs |  60.18 μs |  0.50 |    0.01 |        - |       - |   50333 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,447.7 μs** | **101.19 μs** |  **60.22 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,753.2 μs |  51.14 μs |  33.83 μs |  0.43 |    0.01 |        - |       - |    6777 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,847.6 μs** | **212.95 μs** | **126.72 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,177.0 μs | 988.02 μs | 587.95 μs |  1.03 |    0.05 |        - |       - |   55018 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **122.7 μs** |   **5.26 μs** |   **3.48 μs** |  **1.00** |    **0.04** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    103.9 μs |  14.60 μs |   8.69 μs |  0.85 |    0.07 |        - |       - |      75 B |       0.002 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,243.7 μs** |  **10.79 μs** |   **7.14 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,260.5 μs | 207.18 μs | 137.04 μs |  1.01 |    0.11 |        - |       - |     842 B |       0.003 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,012.8 μs** |  **11.22 μs** |   **7.42 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121535 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    784.7 μs | 103.94 μs |  68.75 μs |  0.77 |    0.06 |        - |       - |     682 B |       0.006 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,093.5 μs** | **138.12 μs** |  **91.36 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214797 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,404.7 μs | 802.82 μs | 477.75 μs |  0.73 |    0.05 |        - |       - |    5829 B |       0.005 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,501.1 μs** |   **5.23 μs** |   **3.46 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    300.3 μs |   7.73 μs |   5.11 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,505.5 μs** |  **54.10 μs** |  **35.78 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    294.7 μs |   2.94 μs |   1.75 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,502.8 μs** |  **15.46 μs** |  **10.23 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    302.6 μs |   9.38 μs |   5.58 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.24 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,513.2 μs** |  **26.73 μs** |  **17.68 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    304.6 μs |   9.02 μs |   5.96 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.24 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **111.8 μs** |   **9.68 μs** |   **3.45 μs** |  **1.00** |    **0.04** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   125.6 μs |   2.52 μs |   1.32 μs |  1.12 |    0.03 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **138.8 μs** |  **17.40 μs** |   **7.73 μs** |  **1.00** |    **0.07** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   161.1 μs |   5.13 μs |   2.69 μs |  1.16 |    0.06 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,081.4 μs** | **519.35 μs** | **271.63 μs** |  **1.06** |    **0.35** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   750.6 μs | 132.11 μs |  58.66 μs |  0.73 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,491.4 μs** | **804.79 μs** | **420.92 μs** |  **1.07** |    **0.41** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,890.8 μs | 956.23 μs | 424.57 μs |  1.36 |    0.46 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,578.3 ns** |    **14.06 ns** |     **7.35 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   533.4 ns |   127.99 ns |    84.65 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,156.1 ns** | **2,015.10 ns** | **1,053.94 ns** |  **1.18** |    **0.71** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,000.1 ns |   117.99 ns |    70.21 ns |  0.37 |    0.19 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 384.82 ns | 0.877 ns | 0.136 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  22.76 ns | 0.094 ns | 0.024 ns |      - |         - |
| WriteDescribeGroupsV6      |  35.38 ns | 0.071 ns | 0.019 ns |      - |         - |
| WriteListConfigResourcesV1 |  15.09 ns | 0.102 ns | 0.027 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.626 μs** | **0.0059 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **1**       | **1.607 μs** | **0.0008 μs** | **0.0002 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.387 μs** | **0.0093 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **9**       | **2.395 μs** | **0.0056 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **10**      | **2.479 μs** | **0.0109 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **11**      | **2.407 μs** | **0.0128 μs** | **0.0020 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.96 ns** | **0.451 ns** | **0.117 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.97 ns | 1.041 ns | 0.270 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.56 ns** | **0.393 ns** | **0.102 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 105.38 ns | 0.301 ns | 0.078 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,643.4 ns | 19.58 ns | 12.95 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,960.0 ns |  2.21 ns |  1.31 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,304.4 ns |  2.27 ns |  1.35 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,284.1 ns |  1.09 ns |  0.57 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,070.7 ns |  5.63 ns |  3.35 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,065.0 ns |  6.60 ns |  3.45 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,057.6 ns |  3.97 ns |  2.36 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,733.1 ns |  4.89 ns |  2.91 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,116.7 ns |  0.85 ns |  0.56 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,813.3 ns |  1.87 ns |  1.24 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   769.0 ns |  2.88 ns |  1.71 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   831.8 ns |  2.17 ns |  1.43 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   180.6 ns |  0.70 ns |  0.46 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,654.5 ns |  2.40 ns |  1.43 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,281.8 ns |  0.54 ns |  0.32 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,097.472 ns | 17.6738 ns | 11.6901 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    13.391 ns |  0.1946 ns |  0.1287 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    16.140 ns |  0.0147 ns |  0.0088 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    29.159 ns |  0.0678 ns |  0.0404 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    26.198 ns |  1.1924 ns |  0.7887 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     9.354 ns |  0.0231 ns |  0.0121 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    92.175 ns |  3.0442 ns |  1.8116 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    42.315 ns |  0.0345 ns |  0.0229 ns |  0.46 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |------------:|---------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    228.3 ns |  0.14 ns |  0.07 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 81,031.5 ns | 25.71 ns | 13.45 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    164.5 ns |  0.31 ns |  0.21 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 79,776.0 ns | 89.42 ns | 53.21 ns |      - |      80 B |

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