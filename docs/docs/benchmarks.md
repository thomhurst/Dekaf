---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-11 21:10 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.7× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 143× less | Stable |
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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 1.01–1.28 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.21 | 0.97–1.42 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.86 | 0.72–0.91 | 22% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.64 | 1.44–1.87 | 26% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.09–0.11 | 20% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.27–0.29 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.93 | 0.81–1.02 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.91–1.20 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.74–0.95 | 26% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.69–0.87 | 23% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.46 | 13% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.99–1.10 | 11% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 24% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 21% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 20% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|--------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,227.0 μs** |   **105.68 μs** |    **69.90 μs** |  **1.00** |    **0.02** |       **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,699.0 μs |    22.59 μs |    11.82 μs |  0.43 |    0.01 |       - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,628.1 μs** |   **130.37 μs** |    **86.23 μs** |  **1.00** |    **0.02** | **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,830.8 μs |   129.75 μs |    85.82 μs |  0.50 |    0.01 |       - |       - |   50891 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,276.9 μs** |    **37.53 μs** |    **22.33 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,851.5 μs |   150.47 μs |    99.52 μs |  0.45 |    0.02 |       - |       - |    7265 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,939.8 μs** |   **374.26 μs** |   **222.72 μs** |  **1.00** |    **0.02** | **93.7500** | **31.2500** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,635.3 μs | 1,905.27 μs | 1,260.22 μs |  0.97 |    0.10 |       - |       - |   60088 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.6 μs** |    **12.04 μs** |     **7.96 μs** |  **1.00** |    **0.08** |  **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    121.8 μs |    12.62 μs |     8.35 μs |  0.92 |    0.08 |       - |       - |     126 B |       0.004 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,273.7 μs** |     **9.40 μs** |     **6.22 μs** |  **1.00** |    **0.01** | **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,214.1 μs |   198.75 μs |   131.46 μs |  0.95 |    0.10 |       - |       - |    1388 B |       0.005 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,030.6 μs** |    **14.54 μs** |     **8.65 μs** |  **1.00** |    **0.01** |  **7.0801** |       **-** |  **121496 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    850.9 μs |   124.05 μs |    82.05 μs |  0.83 |    0.08 |       - |       - |    1133 B |       0.009 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,312.3 μs** |   **101.95 μs** |    **53.32 μs** |  **1.00** |    **0.01** | **70.3125** |       **-** | **1214960 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,439.2 μs |   930.05 μs |   615.17 μs |  0.72 |    0.06 |       - |       - |   11067 B |       0.009 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,573.7 μs** |    **35.16 μs** |    **23.26 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    312.9 μs |     7.34 μs |     4.85 μs |  0.06 |    0.00 |       - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,504.3 μs** |    **21.81 μs** |    **14.43 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    307.1 μs |     9.77 μs |     6.46 μs |  0.06 |    0.00 |       - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,510.1 μs** |    **19.72 μs** |    **13.04 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    316.0 μs |     7.77 μs |     5.14 μs |  0.06 |    0.00 |       - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |             |             |             |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,509.4 μs** |    **19.95 μs** |    **13.20 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    315.0 μs |     7.39 μs |     4.89 μs |  0.06 |    0.00 |       - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **128.4 μs** |    **42.77 μs** |  **22.37 μs** |   **122.8 μs** |  **1.03** |    **0.24** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   132.9 μs |     7.49 μs |   3.33 μs |   132.5 μs |  1.06 |    0.17 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **162.4 μs** |    **54.00 μs** |  **28.24 μs** |   **167.0 μs** |  **1.03** |    **0.24** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   161.9 μs |     4.81 μs |   2.14 μs |   161.9 μs |  1.02 |    0.17 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,140.5 μs** |   **607.66 μs** | **317.82 μs** | **1,023.3 μs** |  **1.07** |    **0.39** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   866.1 μs |    80.86 μs |  35.90 μs |   865.0 μs |  0.81 |    0.20 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,379.9 μs** |   **893.20 μs** | **467.16 μs** | **1,086.9 μs** |  **1.08** |    **0.45** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,706.1 μs | 1,134.73 μs | 503.83 μs | 1,996.8 μs |  1.34 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,579.8 ns** |    **19.69 ns** |    **10.30 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   551.3 ns |   157.26 ns |   104.02 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **4,256.8 ns** | **2,201.06 ns** | **1,455.86 ns** |  **1.16** |    **0.72** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,104.5 ns |   164.50 ns |   108.81 ns |  0.30 |    0.15 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 566.35 ns | 10.072 ns | 2.616 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.05 ns |  0.141 ns | 0.037 ns |      - |         - |
| WriteDescribeGroupsV6      |  46.25 ns |  0.086 ns | 0.013 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.26 ns |  0.075 ns | 0.012 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.041 μs** | **0.0107 μs** | **0.0028 μs** |         **-** |
| **WriteRequest** | **1**       | **2.001 μs** | **0.0025 μs** | **0.0006 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.410 μs** | **0.0047 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **9**       | **2.696 μs** | **0.0127 μs** | **0.0020 μs** |         **-** |
| **WriteRequest** | **10**      | **2.408 μs** | **0.0593 μs** | **0.0154 μs** |         **-** |
| **WriteRequest** | **11**      | **2.424 μs** | **0.0378 μs** | **0.0059 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **110.99 ns** | **0.232 ns** | **0.060 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.58 ns | 0.860 ns | 0.133 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **93.24 ns** | **0.994 ns** | **0.258 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.46 ns | 0.379 ns | 0.098 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,637.9 ns | 7.72 ns | 4.04 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,933.0 ns | 2.35 ns | 1.40 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,316.1 ns | 5.21 ns | 3.10 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,294.3 ns | 3.42 ns | 2.26 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,921.2 ns | 5.79 ns | 3.03 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,980.0 ns | 4.44 ns | 2.94 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,024.7 ns | 4.34 ns | 2.58 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,829.3 ns | 3.47 ns | 1.82 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,145.9 ns | 1.41 ns | 0.93 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.4 ns | 2.89 ns | 1.72 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   752.0 ns | 2.69 ns | 1.78 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   816.3 ns | 1.81 ns | 1.08 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   169.0 ns | 0.14 ns | 0.07 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,702.3 ns | 6.63 ns | 3.95 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,297.0 ns | 0.83 ns | 0.49 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,699.24 ns | 22.985 ns | 15.203 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.17 ns |  0.026 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.91 ns |  0.031 ns |  0.018 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.64 ns |  0.013 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.31 ns |  0.128 ns |  0.076 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.95 ns |  0.009 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    119.73 ns |  1.211 ns |  0.801 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.71 ns |  0.052 ns |  0.034 ns |  0.47 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     289.7 ns |   1.31 ns |   0.86 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,652.8 ns | 491.07 ns | 256.84 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     222.0 ns |   0.47 ns |   0.24 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,982.1 ns | 254.42 ns | 168.28 ns |      - |      80 B |

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