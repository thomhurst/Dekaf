---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-04 16:39 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.1× faster | 2.4× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 69× less | Mixed |
| Consume — drain a topic | on par to 1.4× faster | 1.6× less | Mixed |
| Consume — poll a single message | 2.9×–12× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 0.95 | 0.85–1.09 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.04 | 0.91–1.31 | 38% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.70 | 0.64–0.86 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.19 | 0.96–1.86 | 76% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.07–0.10 | 30% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.35 | 0.16–0.41 | 73% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 1.00 | 0.79–1.13 | 34% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.05 | 0.85–1.14 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 1.00 | 0.80–1.14 | 34% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.96 | 0.76–1.11 | 36% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.51 | 0.50–0.53 | 6% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.48 | 19% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.03 | 0.97–1.48 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,220.3 μs** |   **127.41 μs** |  **75.82 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105185 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,728.4 μs |    15.65 μs |   9.31 μs |  0.44 |    0.01 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,662.2 μs** |    **90.77 μs** |  **60.04 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,782.5 μs |    71.05 μs |  37.16 μs |  0.49 |    0.01 |        - |       - |   51837 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,234.2 μs** |   **129.70 μs** |  **67.83 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,778.8 μs |    69.96 μs |  46.28 μs |  0.45 |    0.01 |        - |       - |    7804 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,802.5 μs** |   **304.24 μs** | **159.12 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944394 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,674.4 μs | 1,391.87 μs | 828.28 μs |  0.99 |    0.06 |        - |       - |   69442 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.9 μs** |     **2.40 μs** |   **1.59 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    111.7 μs |    19.61 μs |  11.67 μs |  0.87 |    0.09 |        - |       - |     210 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,268.9 μs** |    **18.74 μs** |   **9.80 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,121.6 μs |   173.04 μs |  90.50 μs |  0.88 |    0.07 |        - |       - |    2617 B |       0.009 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,066.8 μs** |     **6.59 μs** |   **3.92 μs** |  **1.00** |    **0.00** |   **7.0801** |       **-** |  **121539 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    863.8 μs |    55.38 μs |  32.95 μs |  0.81 |    0.03 |        - |       - |    3368 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,644.3 μs** |    **84.04 μs** |  **50.01 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215601 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,036.4 μs | 1,406.84 μs | 930.54 μs |  0.76 |    0.08 |        - |       - |   19391 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,506.6 μs** |    **13.06 μs** |   **7.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,618.8 μs |    16.42 μs |  10.86 μs |  0.48 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,504.0 μs** |    **10.48 μs** |   **6.24 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,617.3 μs |    16.59 μs |   8.68 μs |  0.48 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,514.1 μs** |     **9.93 μs** |   **6.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,624.2 μs |    17.61 μs |  11.65 μs |  0.48 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,515.9 μs** |    **18.02 μs** |  **11.92 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,613.1 μs |    16.54 μs |  10.94 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **126.2 μs** |    **30.81 μs** |  **16.12 μs** |   **119.9 μs** |  **1.01** |    **0.17** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   130.8 μs |     2.62 μs |   1.37 μs |   130.5 μs |  1.05 |    0.12 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **157.4 μs** |    **57.41 μs** |  **30.03 μs** |   **137.2 μs** |  **1.03** |    **0.25** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   179.0 μs |    18.92 μs |   8.40 μs |   179.2 μs |  1.17 |    0.20 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,133.5 μs** |   **566.15 μs** | **296.11 μs** | **1,046.5 μs** |  **1.06** |    **0.36** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   794.0 μs |   168.36 μs |  74.75 μs |   757.8 μs |  0.74 |    0.19 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,376.9 μs** |   **848.28 μs** | **443.67 μs** | **1,088.6 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,743.7 μs | 1,176.95 μs | 522.57 μs | 2,028.9 μs |  1.36 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,574.0 ns** |    **30.73 ns** |    **16.07 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   569.3 ns |   149.35 ns |    98.79 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,554.8 ns** | **3,027.36 ns** | **2,002.41 ns** |  **1.40** |    **1.18** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,117.2 ns |   148.99 ns |    77.92 ns |  0.44 |    0.25 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 462.04 ns | 3.999 ns | 0.619 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  29.85 ns | 0.238 ns | 0.062 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.67 ns | 0.222 ns | 0.058 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.47 ns | 0.039 ns | 0.010 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.077 μs** | **0.0054 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **1**       | **2.073 μs** | **0.0016 μs** | **0.0004 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.985 μs** | **0.0089 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **9**       | **2.449 μs** | **0.0066 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **10**      | **2.448 μs** | **0.0068 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **11**      | **2.451 μs** | **0.0078 μs** | **0.0020 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **101.79 ns** | **0.074 ns** | **0.012 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.27 ns | 1.545 ns | 0.401 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **101.50 ns** | **0.468 ns** | **0.121 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  96.23 ns | 0.532 ns | 0.082 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,743.0 ns | 9.46 ns | 5.63 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,242.8 ns | 1.92 ns | 1.14 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,414.7 ns | 2.16 ns | 1.43 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,422.6 ns | 1.56 ns | 1.03 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,176.4 ns | 4.93 ns | 2.93 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,138.2 ns | 3.92 ns | 2.05 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,920.6 ns | 3.29 ns | 1.96 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,902.0 ns | 9.91 ns | 6.55 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.8 ns | 1.98 ns | 1.04 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,041.0 ns | 3.39 ns | 2.24 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   775.1 ns | 2.01 ns | 1.33 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   839.3 ns | 4.08 ns | 2.70 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   160.7 ns | 0.09 ns | 0.06 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,678.0 ns | 3.48 ns | 2.07 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,206.0 ns | 2.11 ns | 1.39 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,306.274 ns | 11.4424 ns | 7.5684 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    10.609 ns |  0.0239 ns | 0.0142 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    13.864 ns |  0.0475 ns | 0.0282 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    24.046 ns |  0.0924 ns | 0.0611 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    27.778 ns |  0.0986 ns | 0.0652 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     7.114 ns |  0.0150 ns | 0.0099 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    91.384 ns |  0.8979 ns | 0.5939 ns |  1.00 |    0.01 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    50.067 ns |  0.0084 ns | 0.0044 ns |  0.55 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     299.0 ns |   1.56 ns |  0.93 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 104,722.6 ns |  71.57 ns | 47.34 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     207.4 ns |   0.28 ns |  0.14 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,145.0 ns | 121.51 ns | 72.31 ns |      - |      80 B |

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