---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-11 18:44 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.7× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 154× less | Stable |
| Consume — drain a topic | 1.5× slower to on par | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.8× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 0.94–1.28 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.21 | 1.14–1.42 | 23% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.86 | 0.72–0.91 | 22% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.51 | 1.44–1.87 | 29% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.09–0.11 | 18% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.27–0.29 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.93 | 0.81–1.02 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.91–1.20 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.74–0.95 | 26% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.78 | 0.69–0.87 | 22% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.49 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.46 | 13% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.99–1.10 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 24% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 21% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 20% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,246.2 μs** |    **64.47 μs** |    **42.64 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,689.3 μs |    53.17 μs |    31.64 μs |  0.43 |    0.01 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,614.7 μs** |    **94.38 μs** |    **56.16 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,750.1 μs |   112.95 μs |    74.71 μs |  0.49 |    0.01 |        - |       - |   50926 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,475.5 μs** |    **47.21 μs** |    **28.10 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,852.1 μs |    93.52 μs |    61.86 μs |  0.44 |    0.01 |        - |       - |    7265 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,563.4 μs** |   **277.69 μs** |   **165.25 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,330.0 μs | 2,104.21 μs | 1,391.80 μs |  1.07 |    0.12 |        - |       - |   59339 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **122.1 μs** |     **6.24 μs** |     **4.13 μs** |  **1.00** |    **0.05** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    109.9 μs |    22.62 μs |    14.96 μs |  0.90 |    0.12 |        - |       - |     107 B |       0.004 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,221.8 μs** |     **5.73 μs** |     **3.41 μs** |  **1.00** |    **0.00** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,376.1 μs |   308.52 μs |   204.07 μs |  1.13 |    0.16 |        - |       - |    1257 B |       0.004 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **946.4 μs** |     **7.98 μs** |     **4.17 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121388 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    756.4 μs |   128.02 μs |    84.68 μs |  0.80 |    0.09 |        - |       - |    1073 B |       0.009 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,469.9 μs** |   **127.90 μs** |    **76.11 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214854 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,065.7 μs | 1,098.46 μs |   726.56 μs |  0.85 |    0.07 |        - |       - |   10529 B |       0.009 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,471.0 μs** |     **7.97 μs** |     **4.74 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    290.2 μs |     6.80 μs |     4.50 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,476.7 μs** |    **24.82 μs** |    **12.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1217 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    288.7 μs |    10.68 μs |     6.35 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.47 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,477.5 μs** |    **10.07 μs** |     **6.66 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    290.7 μs |    10.04 μs |     6.64 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,479.9 μs** |    **10.66 μs** |     **7.05 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    291.5 μs |    10.15 μs |     6.71 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **134.9 μs** |    **57.34 μs** |  **29.99 μs** |   **122.5 μs** |  **1.04** |    **0.29** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   132.3 μs |     2.21 μs |   0.79 μs |   132.3 μs |  1.02 |    0.19 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **151.4 μs** |    **57.22 μs** |  **29.93 μs** |   **135.4 μs** |  **1.03** |    **0.25** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   171.0 μs |    27.51 μs |  12.21 μs |   166.2 μs |  1.16 |    0.20 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,135.8 μs** |   **620.96 μs** | **324.78 μs** | **1,015.6 μs** |  **1.07** |    **0.40** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   785.1 μs |    91.87 μs |  40.79 μs |   764.8 μs |  0.74 |    0.19 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,444.3 μs** |   **857.25 μs** | **448.36 μs** | **1,313.8 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,533.3 μs | 1,150.64 μs | 510.89 μs | 1,887.4 μs |  1.15 |    0.47 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,625.2 ns** |   **123.60 ns** |    **81.75 ns** |  **1.00** |    **0.02** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   583.3 ns |   143.89 ns |    95.18 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,992.1 ns** | **2,563.33 ns** | **1,695.48 ns** |  **1.27** |    **0.95** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,052.7 ns |    93.91 ns |    55.88 ns |  0.33 |    0.20 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 510.24 ns | 12.157 ns | 3.157 ns | 0.0486 |    1224 B |
| WriteFindCoordinatorV6     |  24.39 ns |  0.090 ns | 0.023 ns |      - |         - |
| WriteDescribeGroupsV6      |  40.40 ns |  0.168 ns | 0.044 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.82 ns |  0.169 ns | 0.044 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.816 μs** | **0.0048 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **1**       | **1.840 μs** | **0.0059 μs** | **0.0015 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.377 μs** | **0.0073 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **9**       | **2.654 μs** | **0.0092 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **10**      | **2.390 μs** | **0.0170 μs** | **0.0026 μs** |         **-** |
| **WriteRequest** | **11**      | **2.400 μs** | **0.0126 μs** | **0.0019 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       |  **99.87 ns** | **0.379 ns** | **0.098 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.83 ns | 0.350 ns | 0.091 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **97.76 ns** | **2.115 ns** | **0.549 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  94.18 ns | 1.200 ns | 0.186 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.8 ns | 1.53 ns | 1.01 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,947.9 ns | 2.45 ns | 1.62 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,368.4 ns | 3.52 ns | 2.33 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,271.0 ns | 1.08 ns | 0.56 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,898.7 ns | 1.16 ns | 0.69 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,972.0 ns | 6.09 ns | 3.63 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,539.6 ns | 5.45 ns | 3.60 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,836.3 ns | 2.39 ns | 1.42 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.5 ns | 1.27 ns | 0.84 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,812.7 ns | 0.35 ns | 0.18 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   709.7 ns | 1.93 ns | 1.15 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   821.7 ns | 2.34 ns | 1.55 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   172.6 ns | 1.87 ns | 1.12 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,680.9 ns | 1.68 ns | 0.88 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,287.3 ns | 1.61 ns | 0.84 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean          | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,846.408 ns | 5.5328 ns | 3.2925 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     12.094 ns | 0.0197 ns | 0.0130 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     15.177 ns | 0.0316 ns | 0.0209 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     26.824 ns | 0.0674 ns | 0.0446 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     41.253 ns | 0.7417 ns | 0.3879 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.284 ns | 0.0119 ns | 0.0079 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    134.455 ns | 5.0525 ns | 3.3419 ns |  1.00 |    0.03 | 0.0105 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     57.248 ns | 0.0257 ns | 0.0153 ns |  0.43 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     276.4 ns |   0.66 ns |   0.44 ns | 0.0019 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  92,521.1 ns | 338.35 ns | 223.80 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     191.9 ns |   0.39 ns |   0.26 ns | 0.0031 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 100,737.1 ns |  84.30 ns |  50.17 ns |      - |      80 B |

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