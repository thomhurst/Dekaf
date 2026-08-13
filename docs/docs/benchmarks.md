---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-13 20:10 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.7× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 105× less | ⚠ Noisy |
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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.09 | 1.03–1.28 | 22% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 0.97–1.42 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–0.89 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.55 | 1.34–2.40 | 69% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 57% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.91 | 0.75–1.02 | 31% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.96 | 0.74–1.20 | 48% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.83 | 0.76–1.25 | 60% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.69–1.56 | 114% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.99–1.52 | 51% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 62% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 65% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 61% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 66% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,898.1 μs** | **140.21 μs** |  **92.74 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,540.6 μs |  17.20 μs |  10.23 μs |  0.43 |    0.01 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,281.4 μs** |  **96.51 μs** |  **57.43 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,683.7 μs |  53.91 μs |  32.08 μs |  0.51 |    0.01 |        - |       - |   50888 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,648.8 μs** |  **15.65 μs** |   **8.19 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,895.5 μs |  73.75 μs |  48.78 μs |  0.44 |    0.01 |        - |       - |    7281 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,210.5 μs** | **475.72 μs** | **314.66 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,003.6 μs | 848.43 μs | 504.89 μs |  1.07 |    0.05 |        - |       - |   59227 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **113.5 μs** |   **8.61 μs** |   **5.70 μs** |  **1.00** |    **0.07** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    101.5 μs |  27.52 μs |  18.20 μs |  0.90 |    0.16 |        - |       - |     447 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,024.8 μs** | **156.66 μs** | **103.62 μs** |  **1.01** |    **0.14** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    934.6 μs | 106.95 μs |  70.74 μs |  0.92 |    0.12 |        - |       - |    1507 B |       0.005 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **888.9 μs** |  **12.64 μs** |   **6.61 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121295 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    673.4 μs |  61.43 μs |  36.55 μs |  0.76 |    0.04 |        - |       - |    1044 B |       0.009 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,930.7 μs** |  **83.54 μs** |  **49.71 μs** |  **1.00** |    **0.01** |  **72.2656** |       **-** | **1213245 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,344.1 μs | 719.88 μs | 428.39 μs |  0.71 |    0.05 |        - |       - |   11647 B |       0.010 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,480.4 μs** |  **15.45 μs** |  **10.22 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    257.7 μs |   6.98 μs |   4.16 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,471.7 μs** |  **22.37 μs** |  **13.31 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    260.6 μs |   3.60 μs |   2.38 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,479.5 μs** |  **16.03 μs** |   **8.38 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    257.6 μs |   9.15 μs |   6.05 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,493.9 μs** |   **7.99 μs** |   **4.18 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    265.2 μs |  11.04 μs |   7.30 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **115.3 μs** |    **42.05 μs** |  **18.67 μs** |  **1.02** |    **0.21** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   133.0 μs |     6.02 μs |   2.15 μs |  1.18 |    0.16 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **146.6 μs** |    **67.73 μs** |  **35.42 μs** |  **1.05** |    **0.32** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   166.0 μs |    29.57 μs |  15.46 μs |  1.18 |    0.26 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **988.1 μs** |   **565.73 μs** | **295.89 μs** |  **1.08** |    **0.42** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   743.2 μs |   196.08 μs |  87.06 μs |  0.81 |    0.23 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,155.7 μs** |   **728.91 μs** | **323.64 μs** |  **1.05** |    **0.35** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,028.2 μs | 1,388.68 μs | 616.58 μs |  1.85 |    0.66 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error      | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|-----------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,543.4 ns** |   **131.6 ns** |    **87.04 ns** | **5,497.1 ns** |  **1.00** |    **0.02** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   529.1 ns |   118.4 ns |    78.31 ns |   495.1 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |            |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **2,953.1 ns** | **1,685.2 ns** | **1,114.66 ns** | **3,628.1 ns** |  **1.23** |    **0.81** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,000.6 ns |   111.7 ns |    66.45 ns |   977.3 ns |  0.42 |    0.22 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 568.62 ns | 23.441 ns | 6.088 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.26 ns |  0.391 ns | 0.060 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.77 ns |  0.220 ns | 0.034 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.23 ns |  0.093 ns | 0.024 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.003 μs** | **0.0067 μs** | **0.0010 μs** |         **-** |
| **WriteRequest** | **1**       | **1.999 μs** | **0.0027 μs** | **0.0004 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.436 μs** | **0.0293 μs** | **0.0045 μs** |         **-** |
| **WriteRequest** | **9**       | **2.735 μs** | **0.0071 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **10**      | **2.462 μs** | **0.0129 μs** | **0.0033 μs** |         **-** |
| **WriteRequest** | **11**      | **2.413 μs** | **0.0551 μs** | **0.0143 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.96 ns** | **0.482 ns** | **0.125 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.59 ns | 0.470 ns | 0.122 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **98.69 ns** | **0.378 ns** | **0.098 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  95.04 ns | 0.395 ns | 0.103 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,637.4 ns | 4.90 ns | 2.91 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,926.3 ns | 1.88 ns | 0.98 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,330.5 ns | 6.59 ns | 3.92 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,274.8 ns | 1.71 ns | 1.13 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,099.1 ns | 1.56 ns | 1.03 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,930.4 ns | 3.56 ns | 2.12 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,018.1 ns | 2.35 ns | 1.55 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,843.3 ns | 2.84 ns | 1.88 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,145.3 ns | 3.79 ns | 2.51 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,820.8 ns | 9.24 ns | 5.50 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   718.6 ns | 0.91 ns | 0.54 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   833.5 ns | 3.57 ns | 2.36 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.3 ns | 0.20 ns | 0.12 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,726.6 ns | 9.23 ns | 5.49 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,296.4 ns | 0.60 ns | 0.36 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,441.69 ns | 23.685 ns | 14.095 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.55 ns |  0.011 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.28 ns |  0.016 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.80 ns |  0.253 ns |  0.167 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     40.80 ns |  1.378 ns |  0.911 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.79 ns |  0.008 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    141.64 ns |  2.686 ns |  1.776 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.25 ns |  0.184 ns |  0.122 ns |  0.38 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     291.7 ns |   0.39 ns |   0.23 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,057.2 ns | 140.30 ns |  73.38 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     226.0 ns |   0.25 ns |   0.15 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,595.7 ns | 416.22 ns | 275.30 ns |      - |      80 B |

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