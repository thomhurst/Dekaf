---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-09 16:39 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 118× less | Mixed |
| Consume — drain a topic | 1.5× slower to on par | 1.6× less | Mixed |
| Consume — poll a single message | 3.5×–9.9× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.06 | 0.94–1.21 | 26% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.19 | 1.00–1.27 | 23% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.89 | 0.72–1.08 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.49 | 1.47–1.87 | 27% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 28% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.24–0.29 | 20% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.91 | 0.81–1.26 | 49% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.91–1.11 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.80 | 0.74–0.90 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.80 | 0.69–0.85 | 19% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.42–0.46 | 9% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.05 | 0.99–1.51 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 41% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 48% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.03–0.06 | 45% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.03–0.06 | 42% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,267.8 μs** |   **201.77 μs** | **133.46 μs** |  **1.00** |    **0.03** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,729.9 μs |    43.92 μs |  29.05 μs |  0.44 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,585.3 μs** |   **104.63 μs** |  **62.26 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,758.7 μs |   117.78 μs |  70.09 μs |  0.50 |    0.01 |        - |       - |   50854 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,261.0 μs** |    **62.93 μs** |  **37.45 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,823.1 μs |   100.60 μs |  59.87 μs |  0.45 |    0.01 |        - |       - |    7803 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,398.8 μs** |   **344.46 μs** | **227.84 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,480.5 μs | 1,202.41 μs | 795.32 μs |  1.01 |    0.06 |        - |       - |   74198 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.3 μs** |     **1.47 μs** |   **0.97 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    109.2 μs |    21.23 μs |  12.64 μs |  0.85 |    0.09 |        - |       - |     207 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,277.5 μs** |     **8.85 μs** |   **5.85 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,241.2 μs |   183.85 μs | 121.61 μs |  0.97 |    0.09 |        - |       - |    1527 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,051.3 μs** |     **8.81 μs** |   **5.25 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121551 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    832.5 μs |    71.67 μs |  47.40 μs |  0.79 |    0.04 |        - |       - |    1772 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,440.7 μs** |   **160.98 μs** |  **95.80 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215288 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,379.1 μs | 1,217.88 μs | 805.55 μs |  0.80 |    0.07 |        - |       - |   20967 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,527.4 μs** |    **26.90 μs** |  **17.79 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    321.7 μs |    12.31 μs |   8.14 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,559.0 μs** |   **226.34 μs** | **118.38 μs** |  **1.00** |    **0.03** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    336.4 μs |     7.71 μs |   5.10 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,521.6 μs** |    **17.59 μs** |  **11.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    322.1 μs |     8.38 μs |   5.55 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,511.8 μs** |     **7.53 μs** |   **4.48 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    317.6 μs |     9.67 μs |   5.75 μs |  0.06 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **134.3 μs** |    **38.27 μs** |  **20.02 μs** |   **127.7 μs** |  **1.02** |    **0.19** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   130.8 μs |    11.76 μs |   5.22 μs |   132.1 μs |  0.99 |    0.13 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **159.0 μs** |    **55.69 μs** |  **29.13 μs** |   **147.9 μs** |  **1.03** |    **0.24** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   170.6 μs |    28.02 μs |  14.66 μs |   171.4 μs |  1.10 |    0.20 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,145.7 μs** |   **519.85 μs** | **271.89 μs** | **1,057.8 μs** |  **1.05** |    **0.33** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   801.8 μs |    71.12 μs |  31.58 μs |   801.2 μs |  0.73 |    0.16 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,492.7 μs** |   **860.32 μs** | **449.96 μs** | **1,359.5 μs** |  **1.08** |    **0.42** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,650.8 μs | 1,209.00 μs | 536.80 μs | 2,023.1 μs |  1.19 |    0.48 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,573.9 ns** |    **21.16 ns** |    **11.07 ns** | **5,569.2 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   583.4 ns |   130.60 ns |    86.39 ns |   578.3 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,092.4 ns** | **1,710.67 ns** | **1,017.99 ns** | **3,745.8 ns** |  **1.15** |    **0.64** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,136.4 ns |   211.63 ns |   139.98 ns | 1,077.5 ns |  0.42 |    0.19 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 582.77 ns | 9.024 ns | 1.396 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.34 ns | 0.127 ns | 0.033 ns |      - |         - |
| WriteDescribeGroupsV6      |  51.25 ns | 0.117 ns | 0.030 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.23 ns | 0.147 ns | 0.038 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.978 μs** | **0.0048 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **1**       | **2.000 μs** | **0.0035 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.446 μs** | **0.0029 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **9**       | **2.450 μs** | **0.0314 μs** | **0.0049 μs** |         **-** |
| **WriteRequest** | **10**      | **2.461 μs** | **0.0395 μs** | **0.0103 μs** |         **-** |
| **WriteRequest** | **11**      | **2.455 μs** | **0.0169 μs** | **0.0044 μs** |         **-** |

| Method                   | Version | Mean     | Error   | StdDev  | Allocated |
|------------------------- |-------- |---------:|--------:|--------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **107.8 ns** | **0.97 ns** | **0.25 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 100.4 ns | 0.88 ns | 0.14 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **102.9 ns** | **2.73 ns** | **0.71 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 115.7 ns | 2.71 ns | 0.42 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,741.8 ns |  9.11 ns | 6.03 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,088.4 ns |  6.38 ns | 4.22 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,405.7 ns | 10.79 ns | 5.64 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,392.2 ns |  3.30 ns | 1.73 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,176.3 ns |  7.07 ns | 3.70 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,014.9 ns |  1.94 ns | 1.28 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,991.7 ns |  2.19 ns | 1.31 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,900.5 ns |  6.39 ns | 3.34 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.7 ns |  2.41 ns | 1.43 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,040.1 ns |  2.16 ns | 1.29 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   725.3 ns |  3.03 ns | 2.00 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   781.6 ns |  0.90 ns | 0.54 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   161.9 ns |  0.44 ns | 0.23 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,729.4 ns |  2.84 ns | 1.48 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,198.3 ns |  1.30 ns | 0.77 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,675.96 ns | 32.469 ns | 21.476 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |  0.020 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.71 ns |  0.008 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.36 ns |  0.046 ns |  0.027 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.49 ns |  0.769 ns |  0.509 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.015 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.51 ns |  2.068 ns |  1.368 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.52 ns |  0.189 ns |  0.125 ns |  0.50 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     291.2 ns |   0.71 ns |  0.42 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,222.9 ns | 135.36 ns | 80.55 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     224.5 ns |   0.79 ns |  0.52 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 125,212.2 ns | 133.49 ns | 79.44 ns |      - |      80 B |

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