---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-05 10:14 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.2× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 100× less | Mixed |
| Consume — drain a topic | 1.4× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.6×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.07 | 0.85–1.29 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 0.99–1.50 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.75 | 0.64–0.95 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.44 | 0.80–2.22 | 98% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.14–0.41 | 96% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.96 | 0.87–1.13 | 27% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.00 | 0.58–1.20 | 62% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.84 | 0.76–1.01 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.87 | 0.75–1.39 | 73% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.44 | 11% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.09 | 0.97–1.92 | 87% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.45 | 0.04–0.47 | 95% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.45 | 0.05–0.48 | 95% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.45 | 0.05–0.48 | 96% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.45 | 0.05–0.47 | 95% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,013.9 μs** |   **117.99 μs** |    **78.04 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,554.1 μs |    28.29 μs |    16.84 μs |  0.42 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,319.1 μs** |    **81.01 μs** |    **53.58 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,664.6 μs |    77.73 μs |    46.25 μs |  0.50 |    0.01 |        - |       - |   51805 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,622.6 μs** |    **24.13 μs** |    **12.62 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,713.7 μs |    98.54 μs |    65.18 μs |  0.41 |    0.01 |        - |       - |    7864 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,639.6 μs** |   **275.69 μs** |   **164.06 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,938.3 μs | 2,338.20 μs | 1,546.57 μs |  1.12 |    0.14 |        - |       - |   72136 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **103.6 μs** |     **1.64 μs** |     **1.08 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    104.2 μs |    24.55 μs |    16.24 μs |  1.01 |    0.15 |        - |       - |     324 B |        0.01 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,021.8 μs** |   **145.70 μs** |    **96.37 μs** |  **1.01** |    **0.13** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,155.3 μs |   147.89 μs |    97.82 μs |  1.14 |    0.14 |        - |       - |    2069 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **894.2 μs** |    **21.64 μs** |    **11.32 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121324 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    767.4 μs |   130.25 μs |    77.51 μs |  0.86 |    0.08 |        - |       - |    1760 B |        0.01 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,868.8 μs** |   **132.67 μs** |    **69.39 μs** |  **1.00** |    **0.01** |  **72.2656** |       **-** | **1212726 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,517.1 μs |   546.75 μs |   325.36 μs |  0.85 |    0.04 |        - |       - |   44403 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,473.7 μs** |    **13.90 μs** |     **8.27 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    258.2 μs |     6.16 μs |     4.07 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,474.6 μs** |    **25.88 μs** |    **13.54 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    257.6 μs |     4.96 μs |     3.28 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,483.1 μs** |    **11.82 μs** |     **7.82 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    265.5 μs |     4.86 μs |     3.22 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,480.5 μs** |    **26.04 μs** |    **15.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2097 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    259.5 μs |    10.68 μs |     7.06 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **109.9 μs** |    **36.39 μs** |  **19.04 μs** |  **1.02** |    **0.23** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   118.4 μs |     2.82 μs |   1.25 μs |  1.10 |    0.17 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **139.6 μs** |    **61.75 μs** |  **32.30 μs** |  **1.04** |    **0.30** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   153.4 μs |     4.56 μs |   2.02 μs |  1.14 |    0.21 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **992.2 μs** |   **602.65 μs** | **315.20 μs** |  **1.08** |    **0.45** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   706.0 μs |    89.71 μs |  39.83 μs |  0.77 |    0.21 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,154.0 μs** |   **726.19 μs** | **322.43 μs** |  **1.05** |    **0.35** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,830.3 μs | 1,442.32 μs | 640.40 μs |  1.67 |    0.65 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error      | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|-----------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,566.6 ns** |   **126.7 ns** |    **83.80 ns** |  **1.00** |    **0.02** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   548.1 ns |   114.2 ns |    75.53 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |            |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,164.2 ns** | **2,211.9 ns** | **1,463.03 ns** |  **1.29** |    **0.98** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,006.7 ns |   172.4 ns |   102.57 ns |  0.41 |    0.24 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 470.42 ns | 9.642 ns | 2.504 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  31.19 ns | 0.096 ns | 0.025 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.66 ns | 0.119 ns | 0.031 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.59 ns | 0.647 ns | 0.168 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.085 μs** | **0.0058 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **1**       | **2.072 μs** | **0.0021 μs** | **0.0003 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.402 μs** | **0.0071 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **9**       | **2.529 μs** | **0.0090 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **10**      | **2.398 μs** | **0.0207 μs** | **0.0054 μs** |         **-** |
| **WriteRequest** | **11**      | **2.409 μs** | **0.0161 μs** | **0.0025 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **113.78 ns** | **0.220 ns** | **0.034 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.16 ns | 0.221 ns | 0.034 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.73 ns** | **2.211 ns** | **0.574 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  93.44 ns | 0.191 ns | 0.049 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.9 ns | 1.22 ns | 0.73 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,942.0 ns | 4.33 ns | 2.58 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,773.2 ns | 4.38 ns | 2.60 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,288.2 ns | 2.45 ns | 1.46 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,895.1 ns | 2.31 ns | 1.37 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,979.4 ns | 2.71 ns | 1.61 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,877.8 ns | 3.85 ns | 2.29 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,839.9 ns | 3.03 ns | 1.80 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.3 ns | 5.31 ns | 3.16 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.8 ns | 4.75 ns | 3.14 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   705.3 ns | 5.29 ns | 3.50 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   840.7 ns | 0.54 ns | 0.32 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   173.9 ns | 0.07 ns | 0.03 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,714.2 ns | 3.39 ns | 2.25 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,297.1 ns | 1.21 ns | 0.63 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,341.62 ns | 26.719 ns | 13.975 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |  0.017 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.88 ns |  0.018 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.00 ns |  0.065 ns |  0.039 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.91 ns |  1.683 ns |  1.113 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.010 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    116.54 ns |  1.754 ns |  1.160 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.31 ns |  0.111 ns |  0.073 ns |  0.48 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     311.9 ns |   0.34 ns |  0.18 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,958.7 ns | 149.92 ns | 89.21 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     208.6 ns |   0.30 ns |  0.20 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 102,849.8 ns | 133.33 ns | 88.19 ns |      - |      82 B |

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