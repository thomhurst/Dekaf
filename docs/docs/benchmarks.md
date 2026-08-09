---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-09 04:23 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 105× less | ⚠ Noisy |
| Consume — drain a topic | 1.5× slower to on par | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.5×–10× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 0.94–1.46 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.19 | 1.00–1.37 | 30% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.89 | 0.70–1.08 | 42% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.49 | 1.42–1.88 | 30% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 46% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.29 | 0.16–0.32 | 56% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.77–1.26 | 54% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.98 | 0.73–1.11 | 39% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.74–1.08 | 41% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.80 | 0.69–1.24 | 68% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.42–0.46 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.05 | 0.99–1.51 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 52% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 42% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 52% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 49% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,211.5 μs** |    **96.08 μs** |  **50.25 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105185 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,690.9 μs |    20.45 μs |  12.17 μs |  0.43 |    0.00 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,618.0 μs** |   **108.15 μs** |  **71.53 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,802.0 μs |   121.75 μs |  80.53 μs |  0.50 |    0.01 |        - |       - |   51828 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,287.5 μs** |    **77.45 μs** |  **40.51 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194771 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,769.6 μs |   160.07 μs | 105.87 μs |  0.44 |    0.02 |        - |       - |    7825 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,426.9 μs** |   **313.12 μs** | **207.11 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,230.9 μs |   927.37 μs | 551.86 μs |  0.98 |    0.04 |        - |       - |   69950 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **126.4 μs** |     **2.70 μs** |   **1.41 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    112.5 μs |    21.48 μs |  14.21 μs |  0.89 |    0.11 |        - |       - |     288 B |       0.009 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,265.4 μs** |    **17.64 μs** |  **11.67 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,130.7 μs |   203.29 μs | 134.46 μs |  0.89 |    0.10 |        - |       - |    2521 B |       0.008 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,041.0 μs** |     **9.33 μs** |   **5.55 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121513 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    801.5 μs |    83.13 μs |  49.47 μs |  0.77 |    0.05 |        - |       - |    1964 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,342.9 μs** |   **101.64 μs** |  **67.23 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215021 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,185.4 μs | 1,040.67 μs | 688.34 μs |  0.79 |    0.06 |        - |       - |   16968 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,512.2 μs** |     **9.67 μs** |   **6.40 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    318.7 μs |    10.62 μs |   7.02 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,512.3 μs** |     **9.64 μs** |   **6.38 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    316.2 μs |     7.69 μs |   5.09 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,521.6 μs** |    **14.05 μs** |   **9.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    318.8 μs |    10.77 μs |   7.12 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,522.4 μs** |     **6.47 μs** |   **4.28 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    310.5 μs |     9.13 μs |   6.04 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **140.5 μs** |    **58.92 μs** |  **30.82 μs** |   **127.9 μs** |  **1.04** |    **0.29** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   129.1 μs |     1.57 μs |   0.56 μs |   129.3 μs |  0.96 |    0.18 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **150.9 μs** |    **44.78 μs** |  **23.42 μs** |   **144.5 μs** |  **1.02** |    **0.20** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   177.8 μs |    33.45 μs |  14.85 μs |   172.1 μs |  1.20 |    0.19 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,150.0 μs** |   **763.15 μs** | **399.14 μs** |   **888.0 μs** |  **1.10** |    **0.48** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   775.3 μs |   124.23 μs |  55.16 μs |   794.7 μs |  0.74 |    0.21 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,425.4 μs** |   **957.05 μs** | **500.56 μs** | **1,091.9 μs** |  **1.10** |    **0.48** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,736.3 μs | 1,103.14 μs | 489.80 μs | 1,994.9 μs |  1.34 |    0.52 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,569.9 ns** |    **21.73 ns** |  **11.36 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   547.7 ns |   126.26 ns |  83.51 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,228.2 ns** | **1,875.31 ns** | **980.82 ns** |  **1.14** |    **0.61** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,086.3 ns |   104.25 ns |  54.53 ns |  0.38 |    0.17 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 466.99 ns | 5.771 ns | 0.893 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.13 ns | 0.044 ns | 0.007 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.46 ns | 0.147 ns | 0.023 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.45 ns | 0.296 ns | 0.046 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.072 μs** | **0.0013 μs** | **0.0003 μs** |         **-** |
| **WriteRequest** | **1**       | **2.072 μs** | **0.0138 μs** | **0.0021 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.417 μs** | **0.0052 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **9**       | **2.420 μs** | **0.0246 μs** | **0.0038 μs** |         **-** |
| **WriteRequest** | **10**      | **2.404 μs** | **0.0086 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **11**      | **2.420 μs** | **0.0257 μs** | **0.0040 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **105.93 ns** | **1.318 ns** | **0.204 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  96.31 ns | 0.219 ns | 0.057 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **91.73 ns** | **0.359 ns** | **0.093 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  92.19 ns | 0.360 ns | 0.056 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,637.1 ns | 2.03 ns | 1.34 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,146.4 ns | 1.99 ns | 1.32 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,314.6 ns | 5.22 ns | 3.11 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,431.7 ns | 0.98 ns | 0.58 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,095.4 ns | 4.04 ns | 2.67 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,464.2 ns | 4.36 ns | 2.59 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,961.9 ns | 6.17 ns | 3.67 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,845.4 ns | 4.47 ns | 2.66 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.4 ns | 0.79 ns | 0.41 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,818.9 ns | 4.41 ns | 2.63 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   718.4 ns | 1.52 ns | 1.00 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   800.8 ns | 3.92 ns | 2.33 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   170.3 ns | 0.10 ns | 0.07 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,687.8 ns | 6.82 ns | 4.06 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,310.2 ns | 0.93 ns | 0.48 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 8,405.211 ns | 815.4386 ns | 539.3621 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |             |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    10.164 ns |   0.0656 ns |   0.0434 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    14.376 ns |   0.0886 ns |   0.0586 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    28.389 ns |   0.0497 ns |   0.0296 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    26.550 ns |   0.1549 ns |   0.0922 ns |     ? |       ? | 0.0027 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     7.635 ns |   0.9352 ns |   0.6185 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |             |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    83.637 ns |   0.4546 ns |   0.2705 ns |  1.00 |    0.00 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    45.109 ns |   4.4143 ns |   2.9198 ns |  0.54 |    0.03 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     298.8 ns |   0.23 ns |   0.12 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 104,991.8 ns | 121.86 ns |  72.52 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     207.4 ns |   0.52 ns |   0.31 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,271.5 ns | 177.10 ns | 117.14 ns |      - |      80 B |

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