---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-08 21:34 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18×–19× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 69× less | ⚠ Noisy |
| Consume — drain a topic | 1.5× slower to on par | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–10× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.09 | 0.94–1.46 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 1.00–1.37 | 30% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.87 | 0.70–1.08 | 43% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.49 | 1.42–2.21 | 53% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 45% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.16–0.32 | 58% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.92 | 0.77–1.26 | 53% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.00 | 0.73–1.11 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.83 | 0.74–1.08 | 40% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.81 | 0.69–1.24 | 68% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.46 | 12% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.05 | 0.99–1.51 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 51% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 41% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 52% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 49% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,208.1 μs** |    **85.90 μs** |  **56.82 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,699.7 μs |    17.63 μs |   9.22 μs |  0.43 |    0.00 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,627.4 μs** |    **60.25 μs** |  **39.85 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,773.5 μs |    75.95 μs |  45.20 μs |  0.49 |    0.01 |        - |       - |   51876 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,298.1 μs** |   **106.26 μs** |  **63.24 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,913.5 μs |    68.98 μs |  45.63 μs |  0.46 |    0.01 |        - |       - |    7825 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,524.6 μs** |   **226.77 μs** | **134.95 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,681.4 μs |   986.39 μs | 586.99 μs |  1.01 |    0.05 |        - |       - |   71236 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.5 μs** |     **1.70 μs** |   **1.12 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    102.5 μs |    14.12 μs |   9.34 μs |  0.82 |    0.07 |        - |       - |     259 B |       0.009 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,290.8 μs** |    **18.94 μs** |  **12.53 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,209.6 μs |   254.28 μs | 133.00 μs |  0.94 |    0.10 |        - |       - |    2142 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,046.8 μs** |    **13.79 μs** |   **9.12 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121528 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    772.5 μs |   114.52 μs |  75.75 μs |  0.74 |    0.07 |        - |       - |    2531 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,402.1 μs** |   **163.89 μs** |  **97.53 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215014 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,556.2 μs | 1,267.35 μs | 838.28 μs |  0.73 |    0.08 |        - |       - |   18914 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,494.6 μs** |    **10.68 μs** |   **7.06 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    310.4 μs |     3.49 μs |   2.07 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,491.2 μs** |     **6.53 μs** |   **3.88 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    305.4 μs |     7.94 μs |   5.25 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,506.9 μs** |    **24.58 μs** |  **16.26 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2097 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    307.8 μs |     9.32 μs |   6.16 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,502.9 μs** |    **14.80 μs** |   **8.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    307.4 μs |    11.25 μs |   7.44 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **135.8 μs** |    **57.08 μs** |  **29.86 μs** |   **118.2 μs** |  **1.04** |    **0.29** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   127.9 μs |     2.66 μs |   1.18 μs |   127.7 μs |  0.98 |    0.18 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **133.2 μs** |     **5.81 μs** |   **2.07 μs** |   **133.0 μs** |  **1.00** |    **0.02** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   168.6 μs |    17.39 μs |   7.72 μs |   169.2 μs |  1.27 |    0.06 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,056.3 μs** |   **488.96 μs** | **255.74 μs** |   **898.6 μs** |  **1.05** |    **0.32** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   785.0 μs |   116.09 μs |  51.54 μs |   812.7 μs |  0.78 |    0.16 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,401.4 μs** |   **906.84 μs** | **474.29 μs** | **1,100.7 μs** |  **1.09** |    **0.45** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,571.3 μs | 1,146.84 μs | 509.20 μs | 1,925.7 μs |  1.22 |    0.49 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,598.6 ns** |    **89.61 ns** |    **59.27 ns** | **5,566.6 ns** |  **1.00** |    **0.01** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   579.8 ns |   153.32 ns |   101.41 ns |   571.5 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,058.6 ns** | **1,671.13 ns** | **1,105.35 ns** | **3,715.9 ns** |  **1.20** |    **0.75** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,027.8 ns |   113.92 ns |    67.79 ns | 1,008.7 ns |  0.40 |    0.20 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 445.75 ns | 6.709 ns | 1.742 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.01 ns | 0.102 ns | 0.016 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.57 ns | 0.183 ns | 0.028 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.50 ns | 0.088 ns | 0.023 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.079 μs** | **0.0019 μs** | **0.0003 μs** |         **-** |
| **WriteRequest** | **1**       | **2.078 μs** | **0.0068 μs** | **0.0010 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.472 μs** | **0.1210 μs** | **0.0314 μs** |         **-** |
| **WriteRequest** | **9**       | **2.597 μs** | **0.0155 μs** | **0.0024 μs** |         **-** |
| **WriteRequest** | **10**      | **2.458 μs** | **0.0214 μs** | **0.0033 μs** |         **-** |
| **WriteRequest** | **11**      | **2.469 μs** | **0.0058 μs** | **0.0015 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **105.55 ns** | **0.470 ns** | **0.122 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.83 ns | 0.388 ns | 0.101 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.94 ns** | **0.110 ns** | **0.029 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  99.15 ns | 0.668 ns | 0.103 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,742.9 ns |  6.61 ns |  4.37 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,244.7 ns |  1.29 ns |  0.77 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,414.6 ns |  9.92 ns |  5.19 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,408.6 ns |  3.89 ns |  2.57 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,175.9 ns |  1.48 ns |  0.88 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,221.0 ns |  4.79 ns |  2.50 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,956.4 ns |  2.94 ns |  1.75 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,900.1 ns |  4.27 ns |  2.82 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,191.6 ns |  0.73 ns |  0.44 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,048.1 ns | 17.57 ns | 10.46 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   780.8 ns |  1.08 ns |  0.64 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   811.7 ns |  1.91 ns |  1.26 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   163.7 ns |  0.15 ns |  0.08 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,707.0 ns |  1.80 ns |  0.94 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,200.5 ns |  1.17 ns |  0.77 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,215.82 ns | 47.044 ns | 31.117 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.23 ns |  0.262 ns |  0.156 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.29 ns |  0.010 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.71 ns |  0.211 ns |  0.126 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.21 ns |  0.365 ns |  0.242 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.97 ns |  0.017 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    104.82 ns |  0.383 ns |  0.253 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.48 ns |  0.144 ns |  0.075 ns |  0.54 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|---------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     299.0 ns |  0.44 ns |  0.26 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 104,758.7 ns | 98.85 ns | 58.83 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     210.5 ns |  0.44 ns |  0.23 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,306.3 ns | 43.69 ns | 26.00 ns |      - |      80 B |

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