---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-15 09:54 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 21×–22× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 667× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.2× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–9.8× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.16 | 1.00–1.33 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.38 | 1.06–1.51 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.68–0.97 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 1.17–2.40 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.11 | 24% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.18–0.43 | 94% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.93 | 0.74–1.42 | 73% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.00 | 0.92–1.08 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.85 | 0.73–0.97 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.82 | 0.75–1.05 | 36% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 7% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.53 | 31% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.13 | 1.00–1.28 | 25% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 48% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 52% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 52% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,021.9 μs** |    **97.38 μs** |    **57.95 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,658.7 μs |    39.80 μs |    23.68 μs |  0.44 |    0.01 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,551.7 μs** |    **63.03 μs** |    **41.69 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,648.1 μs |   100.52 μs |    59.82 μs |  0.48 |    0.01 |        - |       - |   49784 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,475.8 μs** |    **81.92 μs** |    **54.19 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,699.5 μs |    43.33 μs |    25.78 μs |  0.42 |    0.01 |        - |       - |    6295 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,421.1 μs** |   **377.16 μs** |   **249.47 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,499.3 μs | 1,945.33 μs | 1,017.45 μs |  1.01 |    0.09 |        - |       - |   51270 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.5 μs** |     **1.60 μs** |     **1.06 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    110.7 μs |    23.53 μs |    15.57 μs |  0.91 |    0.12 |        - |       - |      22 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,167.2 μs** |   **145.24 μs** |    **86.43 μs** |  **1.01** |    **0.10** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,153.2 μs |   198.26 μs |   131.14 μs |  0.99 |    0.13 |        - |       - |     263 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **950.7 μs** |    **21.32 μs** |    **11.15 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121367 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    858.9 μs |   129.35 μs |    76.97 μs |  0.90 |    0.08 |        - |       - |     406 B |       0.003 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,088.1 μs** |   **911.33 μs** |   **602.79 μs** |  **1.00** |    **0.08** |  **72.2656** |       **-** | **1213700 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,916.6 μs | 1,231.66 μs |   814.67 μs |  0.79 |    0.09 |        - |       - |    2849 B |       0.002 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,469.7 μs** |    **11.35 μs** |     **6.76 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    298.5 μs |    12.42 μs |     8.21 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,487.4 μs** |    **70.31 μs** |    **36.77 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1201 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    300.7 μs |    12.15 μs |     8.03 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,476.0 μs** |    **18.46 μs** |    **10.99 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    294.3 μs |    12.80 μs |     8.47 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,473.2 μs** |    **11.22 μs** |     **6.68 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    288.5 μs |    10.29 μs |     6.81 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **128.8 μs** |  **32.70 μs** |  **14.52 μs** |  **1.01** |    **0.15** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   134.9 μs |  24.35 μs |  12.74 μs |  1.06 |    0.14 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **159.3 μs** |  **57.44 μs** |  **30.04 μs** |  **1.03** |    **0.25** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   163.0 μs |  14.15 μs |   6.28 μs |  1.05 |    0.18 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,126.3 μs** | **595.28 μs** | **311.34 μs** |  **1.07** |    **0.38** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   859.4 μs | 214.66 μs |  95.31 μs |  0.81 |    0.22 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,409.0 μs** | **845.64 μs** | **442.28 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,961.4 μs | 144.01 μs |  63.94 μs |  1.50 |    0.38 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,601.5 ns** |    **97.65 ns** |  **64.59 ns** |  **1.00** |    **0.02** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   587.3 ns |   152.79 ns | 101.06 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,495.4 ns** | **1,313.52 ns** | **781.65 ns** |  **1.10** |    **0.55** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   981.7 ns |    60.92 ns |  31.86 ns |  0.31 |    0.14 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 470.24 ns | 4.768 ns | 1.238 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  18.33 ns | 0.081 ns | 0.021 ns |      - |         - |
| WriteDescribeGroupsV6      |  32.14 ns | 0.049 ns | 0.013 ns |      - |         - |
| WriteListConfigResourcesV1 |  17.01 ns | 0.315 ns | 0.082 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.692 μs** | **0.0073 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **1**       | **1.691 μs** | **0.0038 μs** | **0.0010 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.754 μs** | **0.0569 μs** | **0.0148 μs** |         **-** |
| **WriteRequest** | **9**       | **1.692 μs** | **0.1273 μs** | **0.0331 μs** |         **-** |
| **WriteRequest** | **10**      | **1.774 μs** | **0.1274 μs** | **0.0331 μs** |         **-** |
| **WriteRequest** | **11**      | **1.795 μs** | **0.1519 μs** | **0.0394 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **71.80 ns** | **2.387 ns** | **0.369 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 63.81 ns | 2.359 ns | 0.613 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **53.98 ns** | **1.866 ns** | **0.484 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 56.04 ns | 3.039 ns | 0.789 ns |         - |

| Method                                          | Mean       | Error     | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|----------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,349.2 ns |  20.02 ns | 13.24 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,527.3 ns |  21.35 ns | 12.70 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,682.3 ns |  44.32 ns | 29.32 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,687.6 ns |  30.97 ns | 20.48 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,655.6 ns |  29.45 ns | 19.48 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 2,779.4 ns |  75.34 ns | 49.83 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 2,690.4 ns | 142.40 ns | 84.74 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,348.8 ns |  56.78 ns | 33.79 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,048.6 ns |  22.20 ns | 14.68 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,613.3 ns |  28.29 ns | 18.71 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   577.4 ns |   6.86 ns |  4.54 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   610.9 ns |  22.65 ns | 14.98 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   118.0 ns |   1.40 ns |  0.92 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,367.1 ns |  30.09 ns | 19.90 ns | 0.0019 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   934.2 ns |  26.32 ns | 17.41 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio  | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|-------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   1.513 ns | 0.0152 ns | 0.0143 ns |   1.00 |    0.01 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 174.929 ns | 0.4123 ns | 0.3443 ns | 115.63 |    1.07 |         - |          NA |

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,457.84 ns | 82.362 ns | 54.477 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.14 ns |  0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     23.27 ns |  0.044 ns |  0.026 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.82 ns |  0.123 ns |  0.064 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.05 ns |  0.220 ns |  0.145 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.00 ns |  0.038 ns |  0.025 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    127.13 ns |  0.739 ns |  0.440 ns |  1.00 |    0.00 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.48 ns |  0.060 ns |  0.036 ns |  0.43 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error       | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|------------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    251.5 ns |     0.24 ns |   0.12 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 99,155.4 ns | 1,027.53 ns | 679.64 ns |      - |      50 B |
| &#39;Snappy Decompress 1KB&#39; |    166.2 ns |     0.79 ns |   0.47 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 96,178.7 ns |    85.74 ns |  51.02 ns |      - |      80 B |

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