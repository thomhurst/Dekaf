---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-06 02:26 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 20× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 118× less | ⚠ Noisy |
| Consume — drain a topic | 1.5× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.6×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.17 | 0.99–1.46 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.25 | 0.99–1.50 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.79 | 0.70–1.08 | 47% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.48 | 0.80–2.22 | 95% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 49% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.14–0.32 | 66% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.96 | 0.77–1.26 | 51% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.01 | 0.58–1.20 | 61% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.86 | 0.76–1.08 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.86 | 0.72–1.39 | 78% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.41–0.44 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.15 | 0.99–1.92 | 81% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.45 | 842% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.45 | 839% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.44 | 822% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.03–0.45 | 830% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,268.9 μs** |   **192.48 μs** | **127.32 μs** |  **1.00** |    **0.03** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,664.0 μs |    29.20 μs |  17.38 μs |  0.43 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,592.2 μs** |    **99.13 μs** |  **65.57 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,809.5 μs |    98.08 μs |  64.87 μs |  0.50 |    0.01 |        - |       - |   51814 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,332.4 μs** |    **82.75 μs** |  **49.24 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,790.2 μs |    62.45 μs |  41.31 μs |  0.44 |    0.01 |        - |       - |    7823 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,111.6 μs** |   **457.20 μs** | **272.07 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,075.4 μs | 1,373.31 μs | 817.23 μs |  1.08 |    0.07 |        - |       - |   70018 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.5 μs** |     **2.71 μs** |   **1.79 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    108.2 μs |    19.30 μs |  12.76 μs |  0.86 |    0.10 |        - |       - |     156 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,265.1 μs** |    **17.18 μs** |  **11.36 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,266.0 μs |   208.43 μs | 137.86 μs |  1.00 |    0.10 |        - |       - |    2094 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,001.2 μs** |    **24.82 μs** |  **16.42 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121426 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    761.2 μs |    95.76 μs |  63.34 μs |  0.76 |    0.06 |        - |       - |    2343 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,908.9 μs** |    **90.64 μs** |  **47.41 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214388 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,363.1 μs | 1,160.74 μs | 767.76 μs |  0.74 |    0.07 |        - |       - |   17118 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,477.9 μs** |     **6.28 μs** |   **4.15 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    305.4 μs |    17.17 μs |  11.35 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,477.3 μs** |     **8.54 μs** |   **4.47 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    298.8 μs |    12.85 μs |   8.50 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,495.9 μs** |    **17.95 μs** |  **11.87 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    308.0 μs |     7.57 μs |   5.01 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,482.5 μs** |     **7.66 μs** |   **5.07 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2106 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    311.4 μs |    13.57 μs |   8.98 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **125.2 μs** |    **29.00 μs** |  **15.17 μs** |   **124.3 μs** |  **1.01** |    **0.16** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   123.8 μs |     4.71 μs |   2.09 μs |   123.1 μs |  1.00 |    0.11 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **154.6 μs** |    **57.04 μs** |  **25.33 μs** |   **140.1 μs** |  **1.02** |    **0.22** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   173.1 μs |    33.01 μs |  17.26 μs |   174.4 μs |  1.14 |    0.20 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,034.6 μs** |   **499.15 μs** | **261.07 μs** |   **861.8 μs** |  **1.05** |    **0.33** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   964.9 μs |   752.55 μs | 334.14 μs |   771.1 μs |  0.98 |    0.38 | 258.48 KB |        0.40 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,431.2 μs** |   **851.50 μs** | **445.35 μs** | **1,314.3 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,697.3 μs | 1,121.92 μs | 498.14 μs | 1,956.6 μs |  1.28 |    0.50 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,568.0 ns** |    **12.75 ns** |     **6.67 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   563.3 ns |    96.72 ns |    63.98 ns |  0.10 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,291.6 ns** | **2,755.05 ns** | **1,822.30 ns** |  **1.37** |    **1.12** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,105.8 ns |   148.17 ns |    88.17 ns |  0.46 |    0.26 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 497.07 ns | 13.974 ns | 2.162 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  18.31 ns |  0.122 ns | 0.019 ns |      - |         - |
| WriteDescribeGroupsV6      |  32.11 ns |  0.185 ns | 0.048 ns |      - |         - |
| WriteListConfigResourcesV1 |  17.06 ns |  0.650 ns | 0.101 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.687 μs** | **0.0138 μs** | **0.0021 μs** |         **-** |
| **WriteRequest** | **1**       | **1.690 μs** | **0.0260 μs** | **0.0040 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.239 μs** | **0.0334 μs** | **0.0087 μs** |         **-** |
| **WriteRequest** | **9**       | **1.225 μs** | **0.0273 μs** | **0.0071 μs** |         **-** |
| **WriteRequest** | **10**      | **1.228 μs** | **0.0290 μs** | **0.0075 μs** |         **-** |
| **WriteRequest** | **11**      | **1.224 μs** | **0.0154 μs** | **0.0024 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **55.29 ns** | **1.139 ns** | **0.296 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 49.10 ns | 1.607 ns | 0.417 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **48.37 ns** | **7.652 ns** | **1.184 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 45.35 ns | 2.974 ns | 0.772 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             |   920.5 ns |  8.52 ns |  4.45 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |   982.8 ns |  4.72 ns |  2.81 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,253.0 ns | 17.69 ns | 10.53 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,287.7 ns | 27.32 ns | 18.07 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,099.8 ns | 33.41 ns | 22.10 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 2,302.3 ns | 23.05 ns | 13.72 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 2,318.4 ns | 24.27 ns | 14.45 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 1,747.5 ns | 20.94 ns | 13.85 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   603.5 ns |  8.84 ns |  5.85 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,066.6 ns |  6.25 ns |  3.72 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   351.8 ns |  3.30 ns |  2.18 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   406.7 ns |  4.64 ns |  3.07 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   107.1 ns |  1.42 ns |  0.94 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            |   989.7 ns | 16.18 ns | 10.70 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   699.5 ns | 15.96 ns | 10.56 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,113.39 ns | 19.113 ns | 12.642 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |  0.021 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns |  0.011 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.06 ns |  0.035 ns |  0.023 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.04 ns |  0.604 ns |  0.400 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.009 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.57 ns |  2.634 ns |  1.567 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.15 ns |  0.320 ns |  0.212 ns |  0.50 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error       | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|------------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     254.0 ns |     0.76 ns |   0.50 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  73,497.3 ns | 1,406.05 ns | 735.39 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     167.6 ns |     0.58 ns |   0.35 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 100,278.9 ns |   532.22 ns | 316.71 ns |      - |      80 B |

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