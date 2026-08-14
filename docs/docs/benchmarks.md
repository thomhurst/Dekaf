---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 00:15 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18×–19× faster | 3.0× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 167× less | Mixed |
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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.09 | 0.93–1.20 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 0.97–1.39 | 34% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–0.90 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.55 | 0.98–2.40 | 91% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 57% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.75–0.95 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.92 | 0.74–1.11 | 40% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.80 | 0.76–1.25 | 62% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.77 | 0.69–1.56 | 113% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.47 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 0.99–1.52 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 57% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 61% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 63% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,030.1 μs** |    **21.25 μs** |  **11.12 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **105185 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,662.8 μs |    17.93 μs |  11.86 μs |  0.44 |    0.00 |        - |       - |    5400 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,617.8 μs** |    **69.11 μs** |  **45.71 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,780.2 μs |   122.91 μs |  73.14 μs |  0.50 |    0.01 |        - |       - |   50312 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,409.2 μs** |    **88.84 μs** |  **52.87 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,783.5 μs |    69.56 μs |  46.01 μs |  0.43 |    0.01 |        - |       - |    6786 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,835.6 μs** |   **133.65 μs** |  **88.40 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,807.9 μs | 1,483.50 μs | 882.81 μs |  1.08 |    0.07 |        - |       - |   55042 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **126.3 μs** |     **1.42 μs** |   **0.94 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    116.2 μs |    25.87 μs |  17.11 μs |  0.92 |    0.13 |        - |       - |     429 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,272.7 μs** |    **12.62 μs** |   **8.34 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304004 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,136.0 μs |   222.08 μs | 146.89 μs |  0.89 |    0.11 |        - |       - |     803 B |       0.003 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,015.4 μs** |     **4.32 μs** |   **2.57 μs** |  **1.00** |    **0.00** |   **7.0801** |       **-** |  **121602 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    771.6 μs |    54.85 μs |  32.64 μs |  0.76 |    0.03 |        - |       - |     843 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,001.9 μs** |    **83.57 μs** |  **49.73 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214579 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,128.1 μs | 1,505.77 μs | 995.97 μs |  0.81 |    0.10 |        - |       - |    5854 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,492.0 μs** |    **16.29 μs** |   **8.52 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    300.6 μs |    12.07 μs |   7.99 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,503.5 μs** |    **15.07 μs** |   **9.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    309.7 μs |     5.83 μs |   3.47 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,506.1 μs** |    **36.65 μs** |  **24.24 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    310.9 μs |    14.38 μs |   9.51 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.24 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,489.0 μs** |     **9.38 μs** |   **6.20 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    315.9 μs |    25.34 μs |  16.76 μs |  0.06 |    0.00 |        - |       - |     512 B |        0.24 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **134.3 μs** |    **59.32 μs** |  **31.03 μs** |   **115.9 μs** |  **1.04** |    **0.31** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   135.9 μs |    18.09 μs |   8.03 μs |   132.7 μs |  1.06 |    0.21 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **159.2 μs** |    **83.51 μs** |  **43.68 μs** |   **133.8 μs** |  **1.05** |    **0.35** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   168.2 μs |    21.83 μs |  11.42 μs |   168.3 μs |  1.11 |    0.24 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,134.4 μs** |   **566.80 μs** | **296.45 μs** | **1,048.6 μs** |  **1.06** |    **0.37** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   774.2 μs |   121.93 μs |  54.14 μs |   743.4 μs |  0.72 |    0.18 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,366.4 μs** |   **864.11 μs** | **451.95 μs** | **1,081.3 μs** |  **1.08** |    **0.44** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,516.4 μs | 2,151.67 μs | 955.35 μs | 1,063.2 μs |  1.20 |    0.78 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,574.6 ns** |    **14.88 ns** |   **7.78 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   564.8 ns |   152.01 ns | 100.54 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,494.4 ns** | **1,282.17 ns** | **763.00 ns** |  **1.09** |    **0.52** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,073.8 ns |   129.71 ns |  85.80 ns |  0.34 |    0.14 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean       | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|--------------------------- |-----------:|----------:|----------:|-------:|-------:|----------:|
| ReadDescribeGroupsV5       | 234.741 ns | 8.3855 ns | 2.1777 ns | 0.0730 | 0.0002 |    1224 B |
| WriteFindCoordinatorV6     |  12.680 ns | 0.4576 ns | 0.1188 ns |      - |      - |         - |
| WriteDescribeGroupsV6      |  21.571 ns | 1.5039 ns | 0.3906 ns |      - |      - |         - |
| WriteListConfigResourcesV1 |   9.713 ns | 0.6149 ns | 0.1597 ns |      - |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.013 μs** | **0.0770 μs** | **0.0200 μs** |         **-** |
| **WriteRequest** | **1**       | **1.008 μs** | **0.0072 μs** | **0.0019 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.571 μs** | **0.0652 μs** | **0.0169 μs** |         **-** |
| **WriteRequest** | **9**       | **1.577 μs** | **0.0724 μs** | **0.0112 μs** |         **-** |
| **WriteRequest** | **10**      | **1.584 μs** | **0.0311 μs** | **0.0048 μs** |         **-** |
| **WriteRequest** | **11**      | **1.561 μs** | **0.0119 μs** | **0.0031 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **60.56 ns** | **1.239 ns** | **0.192 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 61.78 ns | 0.646 ns | 0.168 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **55.63 ns** | **2.066 ns** | **0.320 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 53.39 ns | 0.324 ns | 0.084 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,446.0 ns |  7.49 ns |  4.95 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,382.0 ns |  6.01 ns |  3.58 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,599.3 ns | 16.96 ns | 10.09 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,582.7 ns | 16.88 ns | 10.04 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,567.0 ns | 11.17 ns |  6.65 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 2,494.2 ns | 15.39 ns | 10.18 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 2,439.2 ns | 69.52 ns | 41.37 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,165.0 ns | 14.56 ns |  8.67 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   904.9 ns |  4.72 ns |  2.81 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,584.9 ns | 29.51 ns | 17.56 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   518.6 ns | 10.29 ns |  6.12 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   604.7 ns |  2.99 ns |  1.56 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   111.1 ns |  1.12 ns |  0.67 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,267.4 ns |  9.95 ns |  5.92 ns | 0.0019 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   858.7 ns |  5.81 ns |  3.84 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,049.41 ns | 18.527 ns | 9.690 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |  0.014 ns | 0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.29 ns |  0.053 ns | 0.031 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.81 ns |  0.125 ns | 0.083 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.83 ns |  0.591 ns | 0.352 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     13.71 ns |  0.024 ns | 0.014 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    119.58 ns |  3.306 ns | 2.187 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.24 ns |  0.077 ns | 0.051 ns |  0.45 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error       | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|------------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    135.6 ns |     3.78 ns |   2.50 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 44,516.3 ns | 1,207.27 ns | 718.43 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    113.7 ns |     2.16 ns |   1.43 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 56,077.9 ns |   358.97 ns | 213.61 ns |      - |      80 B |

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