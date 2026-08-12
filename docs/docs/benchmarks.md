---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-12 00:32 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 17×–18× faster | 2.7× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 154× less | Mixed |
| Consume — drain a topic | 1.5× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.9× faster | 1.6× less | ⚠ Noisy |

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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–0.89 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.55 | 1.34–1.87 | 34% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 58% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.93 | 0.75–1.02 | 30% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.96 | 0.74–1.20 | 48% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.74–1.25 | 64% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.71–1.56 | 112% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.47 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.99–1.52 | 51% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 60% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 58% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.02–0.06 | 63% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,595.14 μs** |    **74.909 μs** |    **39.179 μs** |  **1.00** |    **0.01** |       **-** |  **105185 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,444.51 μs |    24.835 μs |    12.989 μs |  0.44 |    0.00 |       - |    5465 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,043.12 μs** |    **92.366 μs** |    **48.309 μs** |  **1.00** |    **0.01** |       **-** | **1048372 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,452.07 μs |    92.925 μs |    61.464 μs |  0.49 |    0.01 |       - |   50769 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,053.59 μs** |    **32.742 μs** |    **17.125 μs** |  **1.00** |    **0.00** |       **-** |  **194770 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,476.16 μs |   104.293 μs |    54.548 μs |  0.41 |    0.01 |       - |    7263 B |        0.04 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **7,695.15 μs** |   **400.328 μs** |   **238.229 μs** |  **1.00** |    **0.04** | **15.6250** | **1944375 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,452.47 μs | 3,592.368 μs | 2,376.129 μs |  1.62 |    0.30 |       - |   60432 B |        0.03 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |     **92.39 μs** |     **2.352 μs** |     **1.400 μs** |  **1.00** |    **0.02** |  **0.2441** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.46 μs |     7.724 μs |     4.040 μs |  0.74 |    0.04 |       - |     127 B |       0.004 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |    **914.57 μs** |    **57.984 μs** |    **34.505 μs** |  **1.00** |    **0.05** |  **1.9531** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    807.68 μs |   366.660 μs |   218.194 μs |  0.88 |    0.23 |       - |    1448 B |       0.005 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **550.20 μs** |   **143.162 μs** |    **85.194 μs** |  **1.02** |    **0.21** |  **1.2207** |  **120500 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    678.74 μs |   460.111 μs |   304.335 μs |  1.26 |    0.57 |       - |    1612 B |        0.01 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **5,681.50 μs** | **1,209.644 μs** |   **800.105 μs** |  **1.02** |    **0.19** | **13.6719** | **1205059 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,198.77 μs | 3,887.126 μs | 2,571.093 μs |  1.47 |    0.48 |       - |   10213 B |       0.008 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,293.21 μs** |    **25.042 μs** |    **13.098 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    130.23 μs |    19.230 μs |    10.058 μs |  0.02 |    0.00 |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,293.01 μs** |    **10.454 μs** |     **6.221 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    143.41 μs |    32.356 μs |    19.255 μs |  0.03 |    0.00 |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,294.32 μs** |    **21.171 μs** |    **11.073 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    142.14 μs |    15.863 μs |    10.492 μs |  0.03 |    0.00 |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |              |              |              |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,391.43 μs** |   **206.293 μs** |   **136.450 μs** |  **1.00** |    **0.03** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    130.40 μs |    12.877 μs |     8.517 μs |  0.02 |    0.00 |       - |     575 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **129.3 μs** |  **20.18 μs** |  **10.55 μs** |  **1.01** |    **0.11** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   132.3 μs |  24.60 μs |  12.86 μs |  1.03 |    0.12 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **151.3 μs** |  **18.76 μs** |   **8.33 μs** |  **1.00** |    **0.07** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   150.5 μs |  14.28 μs |   6.34 μs |  1.00 |    0.06 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,082.1 μs** | **456.89 μs** | **238.96 μs** |  **1.04** |    **0.29** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   761.1 μs |  84.23 μs |  37.40 μs |  0.73 |    0.13 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,438.4 μs** | **632.37 μs** | **330.74 μs** |  **1.04** |    **0.30** | **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,470.0 μs | 970.06 μs | 430.71 μs |  1.06 |    0.36 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error     | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------------ |------------ |-----------:|----------:|---------:|------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **400000**            | **100**         | **7,834.7 ns** |  **13.66 ns** |  **7.14 ns** |  **1.00** | **0.0075** |     **654 B** |        **1.00** |
| Dekaf_PollSingle     | 400000            | 100         |   446.6 ns | 102.64 ns | 67.89 ns |  0.06 | 0.0025 |     270 B |        0.41 |
|                      |                   |             |            |           |          |       |        |           |             |
| **Confluent_PollSingle** | **400000**            | **1000**        | **6,011.5 ns** |  **39.01 ns** | **20.40 ns** |  **1.00** | **0.0275** |    **2454 B** |        **1.00** |
| Dekaf_PollSingle     | 400000            | 1000        |   775.0 ns |  72.85 ns | 48.19 ns |  0.13 | 0.0225 |    2074 B |        0.85 |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 506.26 ns | 6.240 ns | 1.621 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.30 ns | 0.113 ns | 0.029 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.96 ns | 0.190 ns | 0.029 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.13 ns | 0.132 ns | 0.034 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.934 μs** | **0.0031 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **1**       | **1.999 μs** | **0.0035 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.443 μs** | **0.0050 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **9**       | **2.459 μs** | **0.0167 μs** | **0.0043 μs** |         **-** |
| **WriteRequest** | **10**      | **2.467 μs** | **0.0111 μs** | **0.0029 μs** |         **-** |
| **WriteRequest** | **11**      | **2.451 μs** | **0.0163 μs** | **0.0042 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **106.74 ns** | **0.669 ns** | **0.174 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 104.44 ns | 0.908 ns | 0.140 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **97.46 ns** | **0.662 ns** | **0.172 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  90.60 ns | 0.961 ns | 0.250 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,739.9 ns | 10.17 ns | 6.05 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,070.3 ns |  7.32 ns | 4.84 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,435.1 ns |  9.65 ns | 5.74 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,393.3 ns |  5.30 ns | 3.51 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,003.5 ns |  3.60 ns | 2.38 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,947.8 ns |  8.55 ns | 5.09 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,953.4 ns |  5.11 ns | 3.38 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,901.0 ns |  3.72 ns | 2.46 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.3 ns |  1.07 ns | 0.71 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,041.7 ns |  8.07 ns | 4.22 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   702.3 ns |  1.74 ns | 1.15 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   793.9 ns |  4.40 ns | 2.91 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   158.9 ns |  0.72 ns | 0.43 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,694.5 ns |  2.85 ns | 1.69 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,187.6 ns |  3.54 ns | 2.11 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,439.71 ns | 11.226 ns | 6.681 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.66 ns |  0.192 ns | 0.114 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns |  0.031 ns | 0.019 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.09 ns |  0.031 ns | 0.018 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.53 ns |  0.533 ns | 0.353 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.79 ns |  0.016 ns | 0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.31 ns |  3.095 ns | 2.047 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.58 ns |  0.110 ns | 0.073 ns |  0.49 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     290.3 ns |   1.26 ns |  0.75 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,206.7 ns |  70.02 ns | 36.62 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     221.1 ns |   0.75 ns |  0.45 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,352.9 ns | 129.80 ns | 77.24 ns |      - |      80 B |

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