---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-16 12:39 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 20× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.2× faster | 667× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.2× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–9.7× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.15 | 1.01–1.29 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.30 | 1.06–1.51 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.81 | 0.68–0.97 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.79 | 1.17–2.40 | 69% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.10–0.11 | 12% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.25–0.43 | 66% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.81–1.16 | 39% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.96 | 0.89–1.08 | 20% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.82 | 0.74–0.97 | 27% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.82 | 0.75–0.88 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.50 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.53 | 31% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.06 | 1.01–1.18 | 16% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 32% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 30% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 31% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 31% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,023.6 μs** |    **59.23 μs** |    **30.98 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,652.7 μs |    12.19 μs |     8.06 μs |  0.44 |    0.00 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,553.0 μs** |   **131.88 μs** |    **87.23 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,642.7 μs |   105.27 μs |    69.63 μs |  0.48 |    0.01 |        - |       - |   49812 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,527.0 μs** |    **85.75 μs** |    **51.03 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,701.4 μs |    42.53 μs |    28.13 μs |  0.41 |    0.01 |        - |       - |    6324 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,327.8 μs** |   **179.50 μs** |   **106.82 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,540.8 μs | 2,102.74 μs | 1,099.77 μs |  1.02 |    0.09 |        - |       - |   51479 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **122.3 μs** |     **2.01 μs** |     **1.05 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    115.7 μs |    20.39 μs |    13.48 μs |  0.95 |    0.11 |        - |       - |      26 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,291.4 μs** |    **11.89 μs** |     **7.86 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,155.7 μs |   207.21 μs |   123.31 μs |  0.89 |    0.09 |        - |       - |     523 B |       0.002 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **939.2 μs** |     **7.26 μs** |     **4.32 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121380 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    790.7 μs |   149.65 μs |    98.98 μs |  0.84 |    0.10 |        - |       - |     168 B |       0.001 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,586.5 μs** |   **264.73 μs** |   **157.53 μs** |  **1.00** |    **0.02** |  **72.2656** |       **-** | **1213853 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,875.3 μs | 1,038.69 μs |   687.03 μs |  0.82 |    0.07 |        - |       - |    1978 B |       0.002 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,475.7 μs** |     **8.05 μs** |     **5.33 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    288.5 μs |    10.15 μs |     6.72 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,483.9 μs** |    **11.16 μs** |     **6.64 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    288.2 μs |    13.04 μs |     8.62 μs |  0.05 |    0.00 |        - |       - |     455 B |        0.38 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,487.7 μs** |    **15.62 μs** |    **10.33 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    288.2 μs |     8.89 μs |     5.88 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,479.1 μs** |     **8.43 μs** |     **5.02 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    283.6 μs |     6.07 μs |     3.17 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **141.2 μs** |  **67.63 μs** |  **35.37 μs** |  **1.05** |    **0.33** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   129.8 μs |   1.72 μs |   0.76 μs |  0.96 |    0.20 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **154.2 μs** |  **64.06 μs** |  **33.50 μs** |  **1.04** |    **0.29** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   154.7 μs |  15.61 μs |   8.16 μs |  1.04 |    0.20 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,119.5 μs** | **588.54 μs** | **307.82 μs** |  **1.06** |    **0.38** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   799.5 μs | 141.52 μs |  62.84 μs |  0.76 |    0.19 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |           |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,450.7 μs** | **847.51 μs** | **443.26 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,814.8 μs | 900.39 μs | 399.78 μs |  1.35 |    0.46 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,562.9 ns** |    **13.06 ns** |     **7.77 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   575.4 ns |   179.23 ns |   118.55 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,250.1 ns** | **2,223.09 ns** | **1,470.43 ns** |  **1.28** |    **0.94** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   952.3 ns |    37.73 ns |    19.73 ns |  0.37 |    0.21 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 452.99 ns | 3.271 ns | 0.850 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.18 ns | 0.209 ns | 0.032 ns |      - |         - |
| WriteDescribeGroupsV6      |  47.07 ns | 6.520 ns | 1.693 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.46 ns | 0.091 ns | 0.024 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.073 μs** | **0.0045 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **1**       | **2.077 μs** | **0.0017 μs** | **0.0004 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.439 μs** | **0.0077 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **9**       | **2.402 μs** | **0.0078 μs** | **0.0020 μs** |         **-** |
| **WriteRequest** | **10**      | **2.377 μs** | **0.0048 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **11**      | **2.389 μs** | **0.0070 μs** | **0.0018 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **105.17 ns** | **0.239 ns** | **0.062 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.26 ns | 0.361 ns | 0.056 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **91.61 ns** | **0.729 ns** | **0.113 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  92.40 ns | 0.546 ns | 0.142 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.6 ns | 0.65 ns | 0.39 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,891.7 ns | 3.90 ns | 2.32 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,281.9 ns | 4.73 ns | 2.82 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,495.7 ns | 9.42 ns | 4.93 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,023.1 ns | 1.14 ns | 0.68 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,037.5 ns | 5.81 ns | 3.84 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,910.2 ns | 1.83 ns | 1.21 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,976.7 ns | 3.29 ns | 2.18 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,116.7 ns | 1.22 ns | 0.73 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.3 ns | 3.44 ns | 2.05 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   804.6 ns | 2.06 ns | 1.36 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   814.5 ns | 1.73 ns | 1.15 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   141.1 ns | 0.08 ns | 0.04 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,661.8 ns | 3.84 ns | 2.29 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,351.6 ns | 0.72 ns | 0.38 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   3.966 ns | 0.0068 ns | 0.0057 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 237.511 ns | 0.1220 ns | 0.1141 ns | 59.89 |    0.09 |         - |          NA |

| Method                               | Categories | Mean         | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,053.72 ns | 8.398 ns | 4.392 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.58 ns | 0.098 ns | 0.065 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.92 ns | 0.082 ns | 0.054 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.87 ns | 0.429 ns | 0.284 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.86 ns | 0.525 ns | 0.312 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns | 0.007 ns | 0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    114.86 ns | 1.601 ns | 0.953 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.14 ns | 0.110 ns | 0.073 ns |  0.49 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     298.9 ns |   0.73 ns |   0.43 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 109,098.9 ns | 199.50 ns | 104.34 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     211.7 ns |   0.35 ns |   0.23 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,208.4 ns | 188.69 ns | 124.81 ns |      - |      80 B |

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