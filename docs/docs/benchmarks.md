---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-05 02:51 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.2× faster | 2.5× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 50× less | ⚠ Noisy |
| Consume — drain a topic | 1.4× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.6×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.05 | 0.85–1.29 | 42% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 0.94–1.50 | 46% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.75 | 0.64–0.95 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.37 | 0.80–2.22 | 104% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 52% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.14–0.41 | 96% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.95 | 0.79–1.13 | 36% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.95 | 0.58–1.20 | 65% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.88 | 0.76–1.14 | 43% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.92 | 0.75–1.39 | 69% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.48 | 19% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.09 | 0.97–1.92 | 87% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.46 | 0.04–0.47 | 93% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.46 | 0.05–0.48 | 94% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.46 | 0.05–0.48 | 94% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.46 | 0.05–0.47 | 93% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,179.5 μs** | **136.79 μs** |  **90.48 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,683.0 μs |  76.88 μs |  45.75 μs |  0.43 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,564.8 μs** |  **64.26 μs** |  **42.50 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,679.5 μs | 100.40 μs |  66.41 μs |  0.49 |    0.01 |        - |       - |   51886 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,524.4 μs** | **138.62 μs** |  **72.50 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194787 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,754.3 μs |  53.52 μs |  31.85 μs |  0.42 |    0.01 |        - |       - |    7821 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,500.6 μs** | **345.08 μs** | **180.49 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,703.5 μs | 592.41 μs | 352.54 μs |  1.10 |    0.03 |        - |       - |   71235 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.2 μs** |   **1.24 μs** |   **0.82 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    120.0 μs |  24.25 μs |  16.04 μs |  0.99 |    0.13 |        - |       - |     470 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,289.5 μs** | **172.21 μs** | **113.91 μs** |  **1.01** |    **0.12** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,047.9 μs | 227.39 μs | 150.41 μs |  0.82 |    0.13 |        - |       - |    2137 B |       0.007 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **956.2 μs** |  **79.93 μs** |  **47.57 μs** |  **1.00** |    **0.06** |   **7.0801** |       **-** |  **121360 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    915.9 μs | 238.22 μs | 141.76 μs |  0.96 |    0.15 |        - |       - |    1894 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,374.5 μs** | **155.79 μs** |  **92.71 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1213786 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,997.0 μs | 934.42 μs | 618.06 μs |  0.85 |    0.06 |        - |       - |   21451 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,477.6 μs** |   **8.13 μs** |   **5.37 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    286.3 μs |   6.84 μs |   4.52 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,479.3 μs** |  **18.33 μs** |   **9.59 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1217 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    284.4 μs |   9.48 μs |   5.64 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.51 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,490.1 μs** |  **17.07 μs** |  **11.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    286.4 μs |  11.50 μs |   6.84 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,494.1 μs** |  **19.35 μs** |  **12.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    294.1 μs |   7.26 μs |   4.80 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **133.7 μs** |    **47.77 μs** |  **24.98 μs** |   **128.6 μs** |  **1.03** |    **0.25** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   131.9 μs |     8.67 μs |   3.09 μs |   132.7 μs |  1.02 |    0.17 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.4 μs** |    **53.07 μs** |  **27.76 μs** |   **143.4 μs** |  **1.03** |    **0.24** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   170.7 μs |    13.79 μs |   7.21 μs |   172.6 μs |  1.15 |    0.18 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,126.5 μs** |   **583.81 μs** | **305.34 μs** | **1,021.8 μs** |  **1.06** |    **0.38** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   769.7 μs |    79.90 μs |  35.48 μs |   761.4 μs |  0.73 |    0.18 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,466.6 μs** |   **917.66 μs** | **479.95 μs** | **1,307.2 μs** |  **1.09** |    **0.46** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,403.2 μs | 1,223.15 μs | 543.09 μs | 1,049.0 μs |  1.04 |    0.48 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,569.9 ns** |    **23.52 ns** |  **12.30 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   556.3 ns |   147.61 ns |  97.64 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,490.4 ns** | **1,087.05 ns** | **719.01 ns** |  **1.09** |    **0.50** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,058.7 ns |   139.81 ns |  92.47 ns |  0.33 |    0.14 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 555.80 ns | 5.870 ns | 0.908 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.25 ns | 0.093 ns | 0.024 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.02 ns | 0.068 ns | 0.010 ns |      - |         - |
| WriteListConfigResourcesV1 |  21.38 ns | 0.051 ns | 0.013 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.965 μs** | **0.0120 μs** | **0.0031 μs** |         **-** |
| **WriteRequest** | **1**       | **2.001 μs** | **0.0149 μs** | **0.0023 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.087 μs** | **0.0059 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **9**       | **2.057 μs** | **0.0034 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **10**      | **2.133 μs** | **0.0091 μs** | **0.0024 μs** |         **-** |
| **WriteRequest** | **11**      | **2.106 μs** | **0.0394 μs** | **0.0102 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **101.22 ns** | **0.797 ns** | **0.123 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  82.06 ns | 0.136 ns | 0.021 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **74.96 ns** | **0.841 ns** | **0.218 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  72.80 ns | 0.190 ns | 0.049 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,663.8 ns | 32.99 ns | 21.82 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,785.9 ns |  7.94 ns |  4.15 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,264.3 ns |  2.67 ns |  1.77 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,214.2 ns |  2.30 ns |  1.20 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,840.2 ns |  2.82 ns |  1.68 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,705.3 ns |  3.11 ns |  1.63 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,578.6 ns |  3.70 ns |  2.20 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,627.1 ns |  4.19 ns |  2.49 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 2,368.4 ns |  1.28 ns |  0.67 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,618.8 ns | 15.34 ns | 10.14 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   643.1 ns |  0.80 ns |  0.47 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   786.6 ns |  0.71 ns |  0.42 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   181.5 ns |  0.44 ns |  0.23 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,624.7 ns | 12.52 ns |  8.28 ns | 0.0114 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,228.3 ns |  0.44 ns |  0.26 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean          | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,009.458 ns | 483.0177 ns | 319.4863 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |             |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     11.137 ns |   0.6594 ns |   0.4361 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     15.243 ns |   0.5975 ns |   0.3952 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     27.811 ns |   2.7321 ns |   1.8071 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.301 ns |   1.3264 ns |   0.7893 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      7.834 ns |   0.5579 ns |   0.3690 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |             |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.313 ns |  11.2405 ns |   7.4349 ns |  1.00 |    0.09 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     52.893 ns |   1.7782 ns |   1.1762 ns |  0.48 |    0.03 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     292.0 ns |   0.73 ns |   0.48 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,412.2 ns | 357.13 ns | 212.52 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.8 ns |   0.32 ns |   0.19 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,173.0 ns | 198.10 ns | 103.61 ns |      - |      80 B |

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