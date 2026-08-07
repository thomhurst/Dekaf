---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-07 13:39 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 20× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 111× less | ⚠ Noisy |
| Consume — drain a topic | 1.5× slower to 1.2× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.6×–10× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.17 | 0.99–1.46 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.25 | 1.00–1.50 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.70–1.08 | 46% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.48 | 0.80–2.22 | 95% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 46% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.16–0.32 | 58% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.96 | 0.77–1.26 | 51% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.01 | 0.73–1.20 | 46% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.82 | 0.76–1.08 | 40% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.85 | 0.72–1.24 | 62% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.44 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.12 | 0.99–1.92 | 83% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.45 | 842% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.45 | 839% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.44 | 822% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.03–0.45 | 825% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,234.9 μs** |  **86.04 μs** |  **56.91 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,705.6 μs |  17.61 μs |  11.65 μs |  0.43 |    0.00 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,621.1 μs** |  **66.16 μs** |  **43.76 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,806.6 μs | 104.86 μs |  69.36 μs |  0.50 |    0.01 |        - |       - |   51890 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,451.1 μs** |  **93.82 μs** |  **62.06 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,850.7 μs |  42.93 μs |  28.40 μs |  0.44 |    0.01 |        - |       - |    7803 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,298.1 μs** | **294.27 μs** | **153.91 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,530.0 μs | 377.74 μs | 224.79 μs |  1.02 |    0.02 |        - |       - |   70458 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.5 μs** |   **2.01 μs** |   **1.33 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    109.9 μs |   7.15 μs |   3.74 μs |  0.88 |    0.03 |        - |       - |     161 B |       0.005 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,251.0 μs** |  **21.46 μs** |  **14.20 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,204.1 μs | 201.49 μs | 119.90 μs |  0.96 |    0.09 |        - |       - |    2399 B |       0.008 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,004.0 μs** |   **6.80 μs** |   **4.05 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121455 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    790.7 μs | 112.27 μs |  74.26 μs |  0.79 |    0.07 |        - |       - |    2131 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,991.4 μs** | **145.27 μs** |  **96.09 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214358 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,464.2 μs | 760.43 μs | 502.98 μs |  0.85 |    0.05 |        - |       - |   17418 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,503.6 μs** |  **18.14 μs** |  **12.00 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    309.4 μs |  17.17 μs |  11.35 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,501.8 μs** |  **18.17 μs** |  **12.02 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    300.8 μs |   9.20 μs |   6.08 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,482.7 μs** |   **5.63 μs** |   **2.95 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    306.2 μs |  12.99 μs |   8.59 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,488.4 μs** |   **9.01 μs** |   **5.36 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    308.7 μs |  13.72 μs |   9.08 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **131.3 μs** |    **57.91 μs** |  **30.29 μs** |   **115.7 μs** |  **1.04** |    **0.30** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   126.8 μs |     5.11 μs |   2.27 μs |   127.2 μs |  1.00 |    0.18 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **166.7 μs** |    **48.49 μs** |  **25.36 μs** |   **165.8 μs** |  **1.02** |    **0.21** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   164.8 μs |    23.45 μs |   8.36 μs |   166.4 μs |  1.01 |    0.15 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,052.7 μs** |   **529.74 μs** | **277.07 μs** |   **882.8 μs** |  **1.05** |    **0.34** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   806.0 μs |   147.90 μs |  65.67 μs |   792.1 μs |  0.81 |    0.18 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,446.0 μs** |   **899.80 μs** | **470.61 μs** | **1,312.7 μs** |  **1.08** |    **0.45** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,582.0 μs | 1,200.43 μs | 533.00 μs | 1,933.7 μs |  1.19 |    0.50 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,572.4 ns** |    **15.80 ns** |     **8.26 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   577.1 ns |   137.89 ns |    91.21 ns |  0.10 |    0.02 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,504.3 ns** | **3,082.38 ns** | **2,038.80 ns** |  **1.45** |    **1.27** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,112.7 ns |   218.18 ns |   144.31 ns |  0.46 |    0.28 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 566.08 ns | 19.788 ns | 3.062 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.02 ns |  0.069 ns | 0.018 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.20 ns |  0.088 ns | 0.023 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.21 ns |  0.299 ns | 0.046 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.964 μs** | **0.0057 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **1**       | **2.008 μs** | **0.0306 μs** | **0.0047 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.408 μs** | **0.0095 μs** | **0.0025 μs** |         **-** |
| **WriteRequest** | **9**       | **2.406 μs** | **0.0035 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **10**      | **2.418 μs** | **0.0434 μs** | **0.0113 μs** |         **-** |
| **WriteRequest** | **11**      | **2.412 μs** | **0.0189 μs** | **0.0029 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       |  **99.28 ns** | **0.120 ns** | **0.031 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.12 ns | 0.214 ns | 0.056 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **91.93 ns** | **1.053 ns** | **0.163 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  85.09 ns | 0.312 ns | 0.081 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.8 ns |  2.66 ns | 1.58 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,962.3 ns |  7.93 ns | 4.72 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,341.3 ns |  2.16 ns | 1.43 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,251.4 ns |  4.48 ns | 2.34 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,102.6 ns |  1.49 ns | 0.89 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,098.6 ns |  6.32 ns | 3.31 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,993.3 ns |  3.60 ns | 2.14 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,845.2 ns |  1.44 ns | 0.96 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,146.5 ns |  5.35 ns | 3.18 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.4 ns |  6.35 ns | 4.20 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   707.5 ns |  1.29 ns | 0.77 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   870.3 ns |  2.82 ns | 1.47 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.7 ns |  0.11 ns | 0.06 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,751.4 ns | 13.75 ns | 9.10 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,288.1 ns |  0.57 ns | 0.34 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,976.13 ns | 22.203 ns | 13.213 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.55 ns |  0.056 ns |  0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.75 ns |  0.008 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.37 ns |  0.020 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.43 ns |  0.668 ns |  0.398 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.016 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    124.19 ns |  3.988 ns |  2.638 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.44 ns |  0.124 ns |  0.082 ns |  0.45 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     290.7 ns |   1.98 ns |   1.18 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,901.2 ns | 219.27 ns | 114.68 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     222.7 ns |   0.82 ns |   0.54 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,467.3 ns | 195.10 ns | 129.05 ns |      - |      80 B |

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