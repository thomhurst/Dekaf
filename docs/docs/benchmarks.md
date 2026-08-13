---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-13 22:42 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.7× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 143× less | Mixed |
| Consume — drain a topic | 1.6× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.9× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 0.93–1.20 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.21 | 0.97–1.39 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–0.90 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.55 | 1.34–2.40 | 69% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 57% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.88 | 0.75–0.95 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.95 | 0.74–1.11 | 39% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.83 | 0.76–1.25 | 60% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.69–1.56 | 114% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.47 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.06 | 0.99–1.52 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 57% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 61% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 66% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,094.2 μs** |   **127.11 μs** |    **84.08 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,667.6 μs |    20.44 μs |    13.52 μs |  0.44 |    0.01 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,574.5 μs** |    **50.19 μs** |    **33.20 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,755.4 μs |    73.02 μs |    43.45 μs |  0.50 |    0.01 |        - |       - |   50922 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,400.4 μs** |    **71.94 μs** |    **37.63 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,002.6 μs |    81.28 μs |    53.76 μs |  0.47 |    0.01 |        - |       - |    7238 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,006.6 μs** |   **338.99 μs** |   **224.22 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,463.7 μs | 2,355.09 μs | 1,231.76 μs |  1.04 |    0.10 |        - |       - |   59166 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.2 μs** |     **2.12 μs** |     **1.40 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    110.4 μs |    23.96 μs |    15.85 μs |  0.87 |    0.12 |        - |       - |     111 B |       0.004 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,275.5 μs** |    **16.40 μs** |    **10.84 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,144.5 μs |   243.38 μs |   160.98 μs |  0.90 |    0.12 |        - |       - |    1436 B |       0.005 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,016.3 μs** |     **5.88 μs** |     **3.50 μs** |  **1.00** |    **0.00** |   **7.0801** |       **-** |  **121480 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    846.5 μs |   119.29 μs |    78.90 μs |  0.83 |    0.07 |        - |       - |    1078 B |       0.009 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,144.8 μs** |   **110.75 μs** |    **65.90 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214854 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,552.0 μs |   397.33 μs |   236.45 μs |  0.74 |    0.02 |        - |       - |   13756 B |        0.01 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,488.3 μs** |     **5.96 μs** |     **3.94 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    300.8 μs |     7.39 μs |     3.86 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,488.6 μs** |    **11.73 μs** |     **6.13 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    311.0 μs |    14.08 μs |     9.31 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,489.7 μs** |     **7.39 μs** |     **4.89 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    305.5 μs |     6.19 μs |     3.68 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,492.1 μs** |    **12.09 μs** |     **7.19 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    307.8 μs |    12.51 μs |     8.27 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **138.7 μs** |    **35.55 μs** |  **18.59 μs** |   **141.2 μs** |  **1.02** |    **0.19** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   130.8 μs |     3.80 μs |   1.69 μs |   131.3 μs |  0.96 |    0.13 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **154.6 μs** |    **54.42 μs** |  **28.46 μs** |   **144.8 μs** |  **1.03** |    **0.24** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   167.2 μs |     9.07 μs |   4.03 μs |   168.2 μs |  1.11 |    0.18 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,065.7 μs** |   **573.21 μs** | **299.80 μs** |   **875.9 μs** |  **1.06** |    **0.37** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   762.2 μs |   115.27 μs |  51.18 μs |   789.3 μs |  0.76 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,369.1 μs** |   **862.91 μs** | **451.32 μs** | **1,082.2 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,562.9 μs | 1,312.97 μs | 582.97 μs | 1,994.0 μs |  1.23 |    0.53 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,582.4 ns** |    **17.03 ns** |     **8.91 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   582.8 ns |   141.79 ns |    93.78 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **4,136.2 ns** | **2,569.18 ns** | **1,699.35 ns** |  **1.23** |    **0.85** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,035.0 ns |    65.24 ns |    34.12 ns |  0.31 |    0.16 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 544.85 ns | 10.277 ns | 1.590 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.06 ns |  0.149 ns | 0.039 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.59 ns |  0.526 ns | 0.137 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.13 ns |  0.068 ns | 0.018 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.000 μs** | **0.0048 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **1**       | **2.004 μs** | **0.0091 μs** | **0.0024 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.391 μs** | **0.0020 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **9**       | **2.388 μs** | **0.0030 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **10**      | **2.416 μs** | **0.0066 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **11**      | **2.415 μs** | **0.0525 μs** | **0.0136 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.48 ns** | **0.276 ns** | **0.072 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  93.47 ns | 0.238 ns | 0.062 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **95.60 ns** | **0.385 ns** | **0.100 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  86.40 ns | 0.320 ns | 0.050 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,638.8 ns |  3.01 ns |  1.79 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,959.1 ns |  1.54 ns |  0.91 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,328.1 ns |  2.52 ns |  1.50 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,266.8 ns |  2.80 ns |  1.67 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,120.2 ns |  1.89 ns |  1.13 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,002.2 ns |  2.53 ns |  1.32 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,970.7 ns | 22.45 ns | 14.85 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,845.4 ns |  6.16 ns |  3.22 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.7 ns |  0.65 ns |  0.34 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,813.9 ns |  2.18 ns |  1.44 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   786.6 ns |  4.06 ns |  2.68 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   918.0 ns |  4.57 ns |  3.02 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   142.3 ns |  0.09 ns |  0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,759.2 ns |  4.64 ns |  2.76 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,264.1 ns |  0.75 ns |  0.45 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,394.539 ns | 13.4915 ns | 8.9238 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    13.357 ns |  0.0105 ns | 0.0070 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    16.140 ns |  0.0132 ns | 0.0069 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    32.129 ns |  0.0694 ns | 0.0459 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    24.237 ns |  0.0508 ns | 0.0336 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     9.464 ns |  0.0120 ns | 0.0072 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    81.925 ns |  0.4499 ns | 0.2976 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    41.994 ns |  0.0447 ns | 0.0266 ns |  0.51 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     288.9 ns |   1.42 ns |   0.94 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,339.0 ns | 240.31 ns | 143.00 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     221.9 ns |   0.37 ns |   0.24 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,114.6 ns | 366.32 ns | 242.30 ns |      - |      80 B |

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