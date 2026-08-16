---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-16 23:28 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18×–19× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par | 1000× less | ⚠ Noisy |
| Consume — drain a topic | 1.6× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.8×–9.7× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.12 | 0.87–1.27 | 36% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 0.92–1.35 | 34% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.77 | 0.57–0.98 | 54% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.61 | 0.84–2.31 | 92% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.05–0.11 | 60% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.20–0.70 | 185% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.92 | 0.81–1.48 | 73% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.96 | 0.88–1.47 | 62% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.85 | 0.74–1.07 | 39% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.83 | 0.75–1.31 | 68% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.49 | 0.48–0.51 | 7% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.44 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.05 | 0.98–1.40 | 39% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.02–0.06 | 70% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 68% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.02–0.06 | 71% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 73% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,834.79 μs** |    **86.673 μs** |  **51.578 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,554.60 μs |    13.620 μs |   7.124 μs |  0.44 |    0.00 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,236.82 μs** |    **55.850 μs** |  **29.210 μs** |  **1.00** |    **0.01** |  **62.5000** | **23.4375** | **1048384 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,683.76 μs |    65.647 μs |  43.421 μs |  0.51 |    0.01 |        - |       - |   49834 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,642.63 μs** |    **13.847 μs** |   **7.242 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,657.72 μs |    42.656 μs |  28.214 μs |  0.40 |    0.00 |        - |       - |    6328 B |        0.03 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,844.15 μs** |   **518.789 μs** | **308.723 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,370.75 μs | 1,003.221 μs | 597.001 μs |  1.05 |    0.06 |        - |       - |   51522 B |        0.03 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **111.61 μs** |    **10.612 μs** |   **7.019 μs** |  **1.00** |    **0.09** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     97.65 μs |    23.247 μs |  15.377 μs |  0.88 |    0.14 |        - |       - |      30 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,112.57 μs** |   **138.494 μs** |  **91.605 μs** |  **1.01** |    **0.12** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,024.81 μs |   196.260 μs | 129.814 μs |  0.93 |    0.14 |        - |       - |     300 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **886.09 μs** |    **14.572 μs** |   **7.621 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121308 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    676.42 μs |    77.825 μs |  51.476 μs |  0.76 |    0.06 |        - |       - |     244 B |       0.002 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,700.24 μs** |   **120.922 μs** |  **79.982 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1212520 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,891.79 μs | 1,583.659 μs | 942.410 μs |  0.79 |    0.10 |        - |       - |     924 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,466.31 μs** |    **17.270 μs** |  **10.277 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    256.78 μs |     7.426 μs |   4.912 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,485.86 μs** |    **24.629 μs** |  **16.290 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    253.72 μs |    13.257 μs |   8.769 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,480.31 μs** |    **23.512 μs** |  **15.552 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    254.02 μs |     8.522 μs |   5.637 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,487.59 μs** |    **23.092 μs** |  **15.274 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    254.91 μs |     9.044 μs |   5.982 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **113.9 μs** |    **44.45 μs** |  **23.25 μs** |   **100.2 μs** |  **1.03** |    **0.27** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   126.4 μs |    11.23 μs |   4.99 μs |   127.6 μs |  1.15 |    0.20 |  26.46 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **143.8 μs** |    **65.57 μs** |  **34.29 μs** |   **129.2 μs** |  **1.04** |    **0.31** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   167.7 μs |     6.29 μs |   2.79 μs |   167.3 μs |  1.22 |    0.23 | 202.24 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **933.4 μs** |   **567.99 μs** | **297.07 μs** |   **748.8 μs** |  **1.08** |    **0.42** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   836.2 μs |   780.23 μs | 346.43 μs |   663.9 μs |  0.96 |    0.45 | 258.49 KB |        0.40 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,378.9 μs** | **1,040.21 μs** | **544.05 μs** | **1,209.5 μs** |  **1.12** |    **0.55** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,606.9 μs | 1,479.24 μs | 656.79 μs | 1,147.0 μs |  1.31 |    0.66 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error      | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|-----------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,275.8 ns** | **1,097.1 ns** |   **652.89 ns** |  **1.02** |    **0.21** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   535.4 ns |   101.3 ns |    67.02 ns |  0.10 |    0.02 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |            |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,096.4 ns** | **1,708.6 ns** | **1,016.74 ns** |  **1.20** |    **0.77** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,015.2 ns |   140.1 ns |    92.69 ns |  0.39 |    0.21 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 546.11 ns | 10.307 ns | 2.677 ns | 0.0486 |    1224 B |
| WriteFindCoordinatorV6     |  23.28 ns |  0.051 ns | 0.013 ns |      - |         - |
| WriteDescribeGroupsV6      |  41.86 ns |  0.651 ns | 0.101 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.78 ns |  0.127 ns | 0.033 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.821 μs** | **0.0066 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **1**       | **1.819 μs** | **0.0090 μs** | **0.0014 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.464 μs** | **0.0719 μs** | **0.0111 μs** |         **-** |
| **WriteRequest** | **9**       | **2.472 μs** | **0.0086 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **10**      | **2.447 μs** | **0.0089 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **11**      | **2.455 μs** | **0.0232 μs** | **0.0060 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **104.07 ns** | **1.384 ns** | **0.359 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.19 ns | 0.691 ns | 0.180 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **93.77 ns** | **0.748 ns** | **0.194 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  89.23 ns | 0.242 ns | 0.037 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,761.9 ns | 29.76 ns | 19.68 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,236.9 ns |  1.73 ns |  1.14 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,431.2 ns |  1.34 ns |  0.80 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,378.2 ns | 13.03 ns |  6.81 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,058.8 ns |  4.05 ns |  2.68 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,921.1 ns |  2.56 ns |  1.52 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,886.4 ns |  3.51 ns |  2.09 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,909.5 ns | 22.51 ns | 13.40 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,195.4 ns |  3.85 ns |  2.29 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,040.0 ns |  1.63 ns |  0.97 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   781.0 ns |  1.89 ns |  0.99 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   828.5 ns |  0.60 ns |  0.39 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   137.5 ns |  0.10 ns |  0.06 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,702.7 ns |  8.97 ns |  5.34 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,235.6 ns |  0.87 ns |  0.52 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   2.758 ns | 0.0088 ns | 0.0074 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 212.290 ns | 0.2774 ns | 0.2166 ns | 76.98 |    0.21 |         - |          NA |

| Method          | Mean     | Error    | StdDev   | Allocated |
|---------------- |---------:|---------:|---------:|----------:|
| SerializeCached | 42.97 ns | 3.572 ns | 0.196 ns |         - |

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,240.95 ns | 14.290 ns | 9.452 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.80 ns |  0.042 ns | 0.022 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     22.06 ns |  0.011 ns | 0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.57 ns |  0.056 ns | 0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.51 ns |  0.364 ns | 0.241 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     14.60 ns |  0.087 ns | 0.052 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.98 ns |  2.970 ns | 1.964 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.35 ns |  0.161 ns | 0.107 ns |  0.49 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    276.4 ns |   0.45 ns |   0.27 ns | 0.0019 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 93,351.2 ns | 125.89 ns |  74.91 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    188.1 ns |   0.14 ns |   0.08 ns | 0.0031 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 96,836.6 ns | 191.78 ns | 126.85 ns |      - |      80 B |

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