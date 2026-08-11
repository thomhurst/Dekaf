---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-11 22:12 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 17×–18× faster | 2.7× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 154× less | Mixed |
| Consume — drain a topic | 1.7× slower to 1.2× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.8× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 1.01–1.28 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 0.97–1.42 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.80 | 0.70–0.90 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.65 | 1.44–1.87 | 26% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.09–0.11 | 21% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.27–0.29 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.93 | 0.81–1.02 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.97 | 0.88–1.20 | 33% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.79 | 0.74–0.95 | 27% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.69–0.87 | 23% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.47 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.03 | 0.99–1.10 | 11% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 24% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 21% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 20% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,236.5 μs** |   **132.93 μs** |  **87.93 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,706.6 μs |    28.78 μs |  15.05 μs |  0.43 |    0.01 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,615.7 μs** |   **109.02 μs** |  **72.11 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,800.7 μs |    94.17 μs |  56.04 μs |  0.50 |    0.01 |        - |       - |   50930 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,247.5 μs** |    **69.51 μs** |  **41.36 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,935.5 μs |   160.63 μs | 106.25 μs |  0.47 |    0.02 |        - |       - |    7277 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,794.9 μs** |   **509.30 μs** | **266.37 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,622.4 μs |   664.26 μs | 347.42 μs |  0.99 |    0.03 |        - |       - |   59138 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.5 μs** |     **1.22 μs** |   **0.81 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    116.8 μs |    28.50 μs |  18.85 μs |  0.92 |    0.14 |        - |       - |     167 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,290.7 μs** |    **15.98 μs** |  **10.57 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,154.9 μs |   350.88 μs | 208.80 μs |  0.89 |    0.15 |        - |       - |    1434 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,045.7 μs** |     **6.40 μs** |   **3.35 μs** |  **1.00** |    **0.00** |   **7.0801** |       **-** |  **121529 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    798.2 μs |    78.71 μs |  46.84 μs |  0.76 |    0.04 |        - |       - |    1799 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,359.4 μs** |    **94.18 μs** |  **62.29 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215090 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,778.1 μs | 1,228.29 μs | 730.94 μs |  0.75 |    0.07 |        - |       - |    9920 B |       0.008 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,500.3 μs** |     **8.06 μs** |   **4.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    322.6 μs |     9.05 μs |   5.38 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,498.4 μs** |    **13.17 μs** |   **7.84 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    311.5 μs |     9.84 μs |   5.15 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,502.9 μs** |     **8.25 μs** |   **5.45 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    325.9 μs |    14.99 μs |   9.91 μs |  0.06 |    0.00 |        - |       - |     575 B |        0.27 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,512.1 μs** |    **19.05 μs** |  **12.60 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    321.5 μs |     7.51 μs |   4.97 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **139.7 μs** |    **54.30 μs** |  **28.40 μs** |  **1.03** |    **0.27** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   149.4 μs |    26.54 μs |  11.78 μs |  1.11 |    0.22 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **158.7 μs** |    **61.94 μs** |  **32.39 μs** |  **1.03** |    **0.27** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   163.6 μs |    16.95 μs |   8.86 μs |  1.07 |    0.19 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,156.2 μs** |   **565.25 μs** | **295.64 μs** |  **1.06** |    **0.36** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   808.9 μs |   184.50 μs |  81.92 μs |  0.74 |    0.19 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,462.0 μs** |   **881.32 μs** | **460.95 μs** |  **1.08** |    **0.44** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,774.3 μs | 1,185.55 μs | 526.39 μs |  1.32 |    0.52 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,576.9 ns** |     **8.74 ns** |     **5.20 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   572.5 ns |   135.83 ns |    89.84 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,533.5 ns** | **2,591.14 ns** | **1,713.88 ns** |  **1.32** |    **1.03** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,062.9 ns |    82.10 ns |    42.94 ns |  0.40 |    0.23 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 530.16 ns | 7.312 ns | 1.899 ns | 0.0486 |    1224 B |
| WriteFindCoordinatorV6     |  23.17 ns | 0.053 ns | 0.008 ns |      - |         - |
| WriteDescribeGroupsV6      |  41.20 ns | 0.503 ns | 0.131 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.84 ns | 0.047 ns | 0.012 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.842 μs** | **0.0493 μs** | **0.0076 μs** |         **-** |
| **WriteRequest** | **1**       | **1.837 μs** | **0.0107 μs** | **0.0028 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.906 μs** | **0.0341 μs** | **0.0088 μs** |         **-** |
| **WriteRequest** | **9**       | **1.903 μs** | **0.0079 μs** | **0.0020 μs** |         **-** |
| **WriteRequest** | **10**      | **1.906 μs** | **0.0106 μs** | **0.0027 μs** |         **-** |
| **WriteRequest** | **11**      | **1.899 μs** | **0.0022 μs** | **0.0006 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       |  **80.65 ns** | **1.720 ns** | **0.447 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  73.96 ns | 0.541 ns | 0.084 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **316.47 ns** | **7.942 ns** | **1.229 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  71.68 ns | 0.850 ns | 0.221 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,350.3 ns | 2.92 ns | 1.74 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,715.6 ns | 1.58 ns | 0.94 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,971.0 ns | 2.23 ns | 1.32 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,928.5 ns | 0.70 ns | 0.46 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,660.5 ns | 1.02 ns | 0.67 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,451.6 ns | 2.44 ns | 1.62 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,396.2 ns | 7.00 ns | 3.66 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,268.9 ns | 8.41 ns | 5.57 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   928.1 ns | 4.28 ns | 2.24 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,584.0 ns | 1.93 ns | 1.01 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   559.6 ns | 1.55 ns | 1.03 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   606.9 ns | 0.86 ns | 0.51 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   129.7 ns | 0.07 ns | 0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,258.8 ns | 1.93 ns | 1.28 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   906.6 ns | 0.91 ns | 0.54 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,991.95 ns | 25.292 ns | 15.051 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |  0.009 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.71 ns |  0.011 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.93 ns |  0.036 ns |  0.022 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.94 ns |  1.440 ns |  0.952 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |  0.010 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.31 ns |  5.984 ns |  3.958 ns |  1.00 |    0.05 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.14 ns |  1.205 ns |  0.630 ns |  0.50 |    0.02 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    272.8 ns |   0.26 ns |   0.17 ns | 0.0019 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 92,414.3 ns |  85.76 ns |  44.85 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    190.2 ns |   0.62 ns |   0.41 ns | 0.0031 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 99,812.7 ns | 188.75 ns | 124.85 ns |      - |      80 B |

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