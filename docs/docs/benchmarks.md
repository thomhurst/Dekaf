---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-05 15:05 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.2×–2.3× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 118× less | ⚠ Noisy |
| Consume — drain a topic | 1.4× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.11 | 1.03–1.29 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 0.99–1.50 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.75 | 0.64–0.95 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.44 | 0.80–2.22 | 98% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 52% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.14–0.41 | 98% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.95 | 0.77–1.13 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.95 | 0.58–1.20 | 65% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.84 | 0.76–1.08 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.87 | 0.75–1.39 | 73% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.44 | 11% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.12 | 0.97–1.92 | 84% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.03–0.47 | 100% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.44 | 0.04–0.48 | 99% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.03–0.48 | 101% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.45 | 0.03–0.47 | 99% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,004.07 μs** |   **220.593 μs** |   **145.909 μs** |  **1.00** |    **0.03** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,551.54 μs |   102.823 μs |    53.779 μs |  0.43 |    0.01 |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,229.10 μs** |   **118.802 μs** |    **78.580 μs** |  **1.00** |    **0.01** |       **-** | **1048382 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,850.09 μs |   450.150 μs |   297.747 μs |  0.53 |    0.04 |       - |   50828 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,309.28 μs** |    **24.232 μs** |    **14.420 μs** |  **1.00** |    **0.00** |       **-** |  **194770 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,750.10 μs |   145.011 μs |    95.916 μs |  0.44 |    0.01 |       - |    7808 B |        0.04 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **7,802.20 μs** |   **252.229 μs** |   **166.834 μs** |  **1.00** |    **0.03** | **15.6250** | **1944375 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,780.68 μs | 2,656.298 μs | 1,389.295 μs |  1.38 |    0.17 |       - |   70305 B |        0.04 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **105.29 μs** |     **2.185 μs** |     **1.300 μs** |  **1.00** |    **0.02** |  **0.2441** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     85.92 μs |    21.654 μs |    14.323 μs |  0.82 |    0.13 |       - |     193 B |       0.006 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,228.30 μs** |    **97.779 μs** |    **64.675 μs** |  **1.00** |    **0.07** |  **1.9531** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    874.75 μs |   242.769 μs |   160.577 μs |  0.71 |    0.13 |       - |    2013 B |       0.007 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **561.82 μs** |   **151.504 μs** |   **100.210 μs** |  **1.03** |    **0.24** |  **1.2207** |  **120706 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    577.62 μs |   292.584 μs |   153.027 μs |  1.06 |    0.31 |       - |    3028 B |        0.03 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **5,606.91 μs** | **1,196.629 μs** |   **791.496 μs** |  **1.02** |    **0.19** | **13.6719** | **1205227 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  5,938.61 μs | 2,276.067 μs | 1,505.477 μs |  1.08 |    0.29 |       - |   16687 B |        0.01 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,380.92 μs** |    **24.081 μs** |    **15.928 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    154.99 μs |     5.111 μs |     2.673 μs |  0.03 |    0.00 |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,363.03 μs** |    **15.027 μs** |     **8.943 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    196.24 μs |    50.746 μs |    33.565 μs |  0.04 |    0.01 |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,364.23 μs** |    **18.040 μs** |    **11.932 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    157.39 μs |    12.907 μs |     6.751 μs |  0.03 |    0.00 |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,376.18 μs** |    **15.750 μs** |     **9.373 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    168.72 μs |    10.784 μs |     7.133 μs |  0.03 |    0.00 |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **140.0 μs** |  **25.77 μs** |  **13.48 μs** |  **1.01** |    **0.13** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   185.1 μs |  51.91 μs |  27.15 μs |  1.33 |    0.22 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **177.2 μs** |  **32.77 μs** |  **14.55 μs** |  **1.01** |    **0.11** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   204.5 μs |  58.37 μs |  30.53 μs |  1.16 |    0.19 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,125.1 μs** | **379.57 μs** | **168.53 μs** |  **1.02** |    **0.19** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   880.9 μs | 129.74 μs |  57.60 μs |  0.80 |    0.11 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,635.7 μs** | **793.04 μs** | **414.78 μs** |  **1.05** |    **0.33** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,898.7 μs | 786.99 μs | 349.43 μs |  1.22 |    0.32 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error      | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|-----------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **7,315.1 ns** | **2,029.6 ns** | **1,061.51 ns** |  **1.02** |    **0.22** | **0.0075** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   509.2 ns |   139.2 ns |    92.10 ns |  0.07 |    0.02 | 0.0025 |     270 B |        0.41 | Stable |
|                      |                   |             |            |            |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **6,911.5 ns** | **1,681.6 ns** | **1,112.30 ns** |  **1.02** |    **0.21** | **0.0275** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        | 1,034.6 ns |   162.7 ns |    96.80 ns |  0.15 |    0.02 | 0.0225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 582.88 ns | 6.125 ns | 1.591 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.18 ns | 0.335 ns | 0.087 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.22 ns | 0.582 ns | 0.151 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.31 ns | 0.261 ns | 0.068 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.052 μs** | **0.0622 μs** | **0.0162 μs** |         **-** |
| **WriteRequest** | **1**       | **2.092 μs** | **0.0626 μs** | **0.0163 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.397 μs** | **0.0205 μs** | **0.0032 μs** |         **-** |
| **WriteRequest** | **9**       | **2.430 μs** | **0.0042 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **10**      | **2.380 μs** | **0.0069 μs** | **0.0018 μs** |         **-** |
| **WriteRequest** | **11**      | **2.396 μs** | **0.0190 μs** | **0.0029 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.47 ns** | **3.938 ns** | **1.023 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  98.45 ns | 0.325 ns | 0.084 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.12 ns** | **1.418 ns** | **0.219 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  88.37 ns | 0.492 ns | 0.128 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,638.0 ns | 3.78 ns | 1.98 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,166.3 ns | 4.63 ns | 3.07 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,325.5 ns | 2.84 ns | 1.88 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,270.1 ns | 4.11 ns | 2.15 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,915.5 ns | 3.72 ns | 2.46 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,981.8 ns | 7.57 ns | 5.01 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,143.5 ns | 8.40 ns | 4.39 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,847.3 ns | 4.13 ns | 2.45 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,145.1 ns | 1.18 ns | 0.62 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.9 ns | 3.10 ns | 1.84 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   707.2 ns | 4.01 ns | 2.39 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   842.2 ns | 1.81 ns | 1.08 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   170.0 ns | 0.13 ns | 0.08 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,708.9 ns | 1.85 ns | 0.97 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,299.7 ns | 3.78 ns | 2.25 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,660.05 ns | 27.428 ns | 16.322 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.15 ns |  0.015 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.90 ns |  0.009 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.62 ns |  0.021 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.73 ns |  0.092 ns |  0.061 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.96 ns |  0.012 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.68 ns |  2.660 ns |  1.583 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.63 ns |  0.083 ns |  0.055 ns |  0.51 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     294.2 ns |   2.08 ns |   1.38 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  98,650.8 ns | 483.25 ns | 319.64 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     226.4 ns |   0.54 ns |   0.32 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,017.4 ns | 295.24 ns | 195.28 ns |      - |      80 B |

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