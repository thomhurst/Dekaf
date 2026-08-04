---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-04 19:18 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.1× faster | 2.4× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 118× less | ⚠ Noisy |
| Consume — drain a topic | 1.4× slower to 1.4× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.1×–12× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.03 | 0.85–1.13 | 27% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.10 | 0.94–1.31 | 33% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.73 | 0.64–0.86 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.37 | 0.96–1.86 | 66% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 52% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.32 | 0.14–0.41 | 83% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.95 | 0.79–1.13 | 36% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.58–1.14 | 56% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.98 | 0.80–1.14 | 35% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.96 | 0.75–1.39 | 66% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.42–0.44 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.51 | 0.50–0.53 | 6% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.48 | 18% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.03 | 0.97–1.83 | 83% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.47 | 0.44–0.48 | 7% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.47 | 0.44–0.48 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.47 | 0.44–0.48 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.47 | 0.45–0.47 | 5% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,702.89 μs** |   **148.879 μs** |    **88.595 μs** |  **1.00** |    **0.02** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,402.36 μs |   101.582 μs |    60.450 μs |  0.42 |    0.01 |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **6,739.41 μs** |   **190.140 μs** |   **113.150 μs** |  **1.00** |    **0.02** |  **7.8125** | **1048372 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,419.43 μs |   311.107 μs |   185.135 μs |  0.51 |    0.03 |       - |   51725 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,101.99 μs** |   **379.650 μs** |   **225.924 μs** |  **1.00** |    **0.05** |       **-** |  **194770 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,532.22 μs |   283.035 μs |   187.210 μs |  0.42 |    0.03 |       - |    7783 B |        0.04 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **7,352.69 μs** |   **263.336 μs** |   **174.180 μs** |  **1.00** |    **0.03** | **15.6250** | **1944375 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 14,075.68 μs | 5,429.387 μs | 3,591.203 μs |  1.92 |    0.47 |       - |   70162 B |        0.04 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |     **82.26 μs** |     **9.476 μs** |     **6.268 μs** |  **1.01** |    **0.10** |  **0.2441** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     82.04 μs |    38.944 μs |    25.759 μs |  1.00 |    0.31 |       - |     194 B |       0.006 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,093.90 μs** |   **111.027 μs** |    **73.438 μs** |  **1.00** |    **0.09** |  **2.9297** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    649.18 μs |   165.148 μs |   109.235 μs |  0.60 |    0.10 |       - |    2105 B |       0.007 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **488.17 μs** |   **131.300 μs** |    **86.847 μs** |  **1.03** |    **0.24** |  **1.2207** |  **120412 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    497.31 μs |   315.218 μs |   208.497 μs |  1.05 |    0.46 |       - |    1706 B |        0.01 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **5,145.16 μs** |   **760.999 μs** |   **503.354 μs** |  **1.01** |    **0.14** | **13.6719** | **1205582 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,917.51 μs | 2,725.130 μs | 1,621.681 μs |  1.36 |    0.33 |       - |   19651 B |        0.02 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,266.99 μs** |    **10.589 μs** |     **6.301 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,425.82 μs |   228.621 μs |   151.219 μs |  0.46 |    0.03 |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,272.16 μs** |    **34.105 μs** |    **17.838 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,308.43 μs |    12.510 μs |     7.445 μs |  0.44 |    0.00 |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,282.17 μs** |    **33.368 μs** |    **19.857 μs** |  **1.00** |    **0.01** |       **-** |    **2100 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,324.13 μs |    27.312 μs |    16.253 μs |  0.44 |    0.00 |       - |     632 B |        0.30 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,279.26 μs** |    **15.467 μs** |     **8.090 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,378.73 μs |    59.161 μs |    30.942 μs |  0.45 |    0.01 |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **102.8 μs** |  **11.47 μs** |   **5.09 μs** |  **1.00** |    **0.07** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   108.3 μs |  22.42 μs |  11.73 μs |  1.06 |    0.12 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **150.8 μs** |  **48.36 μs** |  **25.29 μs** |  **1.02** |    **0.22** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   141.9 μs |  18.33 μs |   8.14 μs |  0.96 |    0.15 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **884.8 μs** | **233.01 μs** | **103.46 μs** |  **1.01** |    **0.16** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   666.8 μs |  76.29 μs |  33.87 μs |  0.76 |    0.09 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,355.8 μs** | **666.49 μs** | **348.59 μs** |  **1.05** |    **0.35** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,590.0 μs | 851.29 μs | 377.98 μs |  1.23 |    0.39 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,523.4 ns** |    **51.13 ns** |    **30.42 ns** |  **1.00** |    **0.01** | **0.0075** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   374.4 ns |    76.56 ns |    50.64 ns |  0.07 |    0.01 | 0.0025 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **6,687.7 ns** | **1,699.02 ns** | **1,123.79 ns** |  **1.02** |    **0.22** | **0.0275** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        |   835.8 ns |    49.86 ns |    29.67 ns |  0.13 |    0.02 | 0.0225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 576.61 ns | 12.573 ns | 3.265 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.01 ns |  0.095 ns | 0.025 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.02 ns |  0.079 ns | 0.012 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.21 ns |  0.192 ns | 0.030 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.973 μs** | **0.0421 μs** | **0.0109 μs** |         **-** |
| **WriteRequest** | **1**       | **1.971 μs** | **0.0449 μs** | **0.0116 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.224 μs** | **0.0231 μs** | **0.0060 μs** |         **-** |
| **WriteRequest** | **9**       | **1.225 μs** | **0.0048 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **10**      | **1.223 μs** | **0.0187 μs** | **0.0048 μs** |         **-** |
| **WriteRequest** | **11**      | **1.229 μs** | **0.0245 μs** | **0.0038 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **50.17 ns** | **0.604 ns** | **0.157 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 48.84 ns | 0.068 ns | 0.018 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **46.80 ns** | **0.171 ns** | **0.026 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 46.36 ns | 0.970 ns | 0.150 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             |   853.5 ns | 11.13 ns |  7.36 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |   997.3 ns |  1.03 ns |  0.62 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,225.0 ns |  2.12 ns |  1.26 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,236.4 ns |  1.52 ns |  1.00 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,054.3 ns |  1.53 ns |  0.91 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 2,266.9 ns |  2.18 ns |  1.14 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 2,286.4 ns | 30.89 ns | 18.38 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 1,732.0 ns |  8.75 ns |  4.58 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   604.4 ns |  2.13 ns |  1.41 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,066.5 ns |  1.58 ns |  0.94 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   347.4 ns |  5.99 ns |  3.96 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   413.3 ns |  2.51 ns |  1.31 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   101.9 ns |  0.04 ns |  0.02 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            |   957.5 ns |  2.68 ns |  1.77 ns | 0.0181 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   636.6 ns |  1.30 ns |  0.77 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,092.25 ns | 17.103 ns | 10.178 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |  0.007 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns |  0.018 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.09 ns |  0.062 ns |  0.037 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     35.21 ns |  0.797 ns |  0.474 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.011 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    124.55 ns |  3.735 ns |  2.222 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.22 ns |  0.114 ns |  0.068 ns |  0.44 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     302.1 ns |   1.44 ns |   0.95 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,406.1 ns | 166.66 ns |  99.18 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.3 ns |   0.46 ns |   0.30 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,202.4 ns | 414.23 ns | 246.50 ns |      - |      80 B |

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