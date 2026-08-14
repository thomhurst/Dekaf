---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 21:52 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 3.2× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Stable |
| Produce — fire-and-forget | on par to 1.2× faster | 500× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.8×–10× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.14 | 0.93–1.33 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 1.06–1.50 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.76 | 0.71–1.02 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 0.98–2.40 | 78% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 23% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.26 | 0.18–0.29 | 41% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.91 | 0.74–1.42 | 75% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.97 | 0.89–1.04 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.73–0.91 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.81 | 0.75–1.05 | 37% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.07 | 1.00–1.28 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 40% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 43% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.04–0.06 | 40% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.04–0.06 | 43% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,869.7 μs** |   **122.96 μs** |    **73.17 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,522.1 μs |    12.08 μs |     6.32 μs |  0.43 |    0.01 |        - |       - |    5368 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,280.7 μs** |    **73.05 μs** |    **38.21 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,639.8 μs |    86.78 μs |    51.64 μs |  0.50 |    0.01 |        - |       - |   50003 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,664.6 μs** |    **42.23 μs** |    **25.13 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,675.5 μs |    82.72 μs |    54.72 μs |  0.40 |    0.01 |        - |       - |    6550 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,206.3 μs** |   **404.27 μs** |   **267.40 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,798.1 μs | 1,472.34 μs |   770.06 μs |  1.16 |    0.08 |        - |       - |   53103 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **103.4 μs** |     **0.79 μs** |     **0.47 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    100.4 μs |    22.87 μs |    15.13 μs |  0.97 |    0.14 |        - |       - |      57 B |       0.002 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,109.9 μs** |   **102.20 μs** |    **67.60 μs** |  **1.00** |    **0.08** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,096.9 μs |   207.34 μs |   137.14 μs |  0.99 |    0.13 |        - |       - |     426 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **874.0 μs** |    **10.92 μs** |     **5.71 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121268 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    763.5 μs |   173.07 μs |   102.99 μs |  0.87 |    0.11 |        - |       - |     690 B |       0.006 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,465.7 μs** |    **99.38 μs** |    **65.73 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1212101 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,075.3 μs | 1,589.25 μs | 1,051.19 μs |  0.84 |    0.12 |        - |       - |    2371 B |       0.002 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,482.2 μs** |    **18.58 μs** |    **12.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    246.5 μs |     8.85 μs |     5.86 μs |  0.04 |    0.00 |        - |       - |     480 B |        0.40 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,484.9 μs** |    **28.51 μs** |    **14.91 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    242.5 μs |     9.05 μs |     5.99 μs |  0.04 |    0.00 |        - |       - |     480 B |        0.40 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,486.6 μs** |    **20.32 μs** |    **13.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    249.8 μs |     6.71 μs |     4.44 μs |  0.05 |    0.00 |        - |       - |     480 B |        0.23 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,485.4 μs** |    **32.06 μs** |    **21.20 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    247.5 μs |     8.93 μs |     5.91 μs |  0.05 |    0.00 |        - |       - |     480 B |        0.23 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **114.4 μs** |    **43.96 μs** |  **22.99 μs** |   **109.1 μs** |  **1.04** |    **0.28** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   132.3 μs |     7.32 μs |   3.25 μs |   132.5 μs |  1.20 |    0.22 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **118.2 μs** |     **9.24 μs** |   **3.29 μs** |   **117.9 μs** |  **1.00** |    **0.04** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   167.8 μs |    15.93 μs |   8.33 μs |   167.9 μs |  1.42 |    0.08 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **952.0 μs** |   **577.76 μs** | **302.18 μs** |   **761.4 μs** |  **1.07** |    **0.42** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   658.2 μs |    49.24 μs |  21.86 μs |   653.5 μs |  0.74 |    0.18 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,193.8 μs** |   **732.18 μs** | **325.09 μs** |   **980.0 μs** |  **1.06** |    **0.36** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,990.5 μs | 1,448.16 μs | 642.99 μs | 2,354.3 μs |  1.76 |    0.67 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,499.5 ns** |    **11.28 ns** |     **6.71 ns** | **5,500.9 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   552.7 ns |   123.52 ns |    81.70 ns |   560.2 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **2,948.6 ns** | **1,684.50 ns** | **1,114.19 ns** | **3,630.9 ns** |  **1.23** |    **0.82** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   974.6 ns |    61.51 ns |    36.60 ns |   988.2 ns |  0.41 |    0.21 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 541.05 ns | 5.222 ns | 1.356 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.27 ns | 0.130 ns | 0.034 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.29 ns | 0.154 ns | 0.024 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.09 ns | 0.160 ns | 0.042 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.005 μs** | **0.0088 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **1**       | **1.927 μs** | **0.0062 μs** | **0.0010 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.456 μs** | **0.0011 μs** | **0.0002 μs** |         **-** |
| **WriteRequest** | **9**       | **2.453 μs** | **0.0074 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **10**      | **2.477 μs** | **0.0136 μs** | **0.0021 μs** |         **-** |
| **WriteRequest** | **11**      | **2.470 μs** | **0.0104 μs** | **0.0027 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **108.44 ns** | **1.396 ns** | **0.216 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.31 ns | 1.485 ns | 0.386 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.78 ns** | **0.611 ns** | **0.159 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  95.45 ns | 0.539 ns | 0.140 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,741.9 ns | 12.26 ns | 8.11 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,068.4 ns |  3.50 ns | 2.08 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,399.9 ns |  9.32 ns | 5.55 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,735.3 ns |  0.95 ns | 0.57 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,053.9 ns |  1.87 ns | 0.98 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,940.4 ns |  8.46 ns | 5.03 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,997.5 ns |  2.54 ns | 1.51 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,900.2 ns |  8.44 ns | 5.02 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.7 ns |  0.71 ns | 0.37 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,041.2 ns |  3.23 ns | 1.92 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   773.2 ns |  1.03 ns | 0.54 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   866.9 ns |  1.81 ns | 1.20 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   140.6 ns |  0.38 ns | 0.23 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,682.3 ns |  2.45 ns | 1.28 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,157.5 ns |  0.90 ns | 0.47 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,848.20 ns | 23.605 ns | 14.047 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.14 ns |  0.012 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     22.32 ns |  0.036 ns |  0.021 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.81 ns |  0.039 ns |  0.021 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.48 ns |  0.157 ns |  0.093 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.96 ns |  0.013 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    114.48 ns |  0.274 ns |  0.181 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.64 ns |  0.049 ns |  0.029 ns |  0.49 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     290.8 ns |   1.66 ns |   1.10 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,099.4 ns | 449.33 ns | 297.21 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.3 ns |   0.84 ns |   0.50 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,904.7 ns | 119.13 ns |  78.80 ns |      - |      80 B |

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