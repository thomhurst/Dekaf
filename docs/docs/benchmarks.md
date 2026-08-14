---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 09:10 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 19× faster | 3.0× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 200× less | ⚠ Noisy |
| Consume — drain a topic | 1.7× slower to 1.3× faster | 1.6× less | Mixed |
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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–1.02 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.70 | 0.98–2.40 | 84% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 57% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.75–1.12 | 42% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.92 | 0.74–1.11 | 40% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.82 | 0.76–1.25 | 61% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.77 | 0.69–1.56 | 113% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.09 | 0.99–1.52 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 64% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 58% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 62% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 65% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,844.7 μs** |  **80.49 μs** |  **42.10 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,561.6 μs |  25.75 μs |  15.32 μs |  0.44 |    0.00 |        - |       - |    5400 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,266.2 μs** |  **58.05 μs** |  **38.40 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,656.9 μs |  62.64 μs |  41.43 μs |  0.50 |    0.01 |        - |       - |   50321 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,615.3 μs** |  **20.43 μs** |  **13.52 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,696.5 μs |  62.29 μs |  41.20 μs |  0.41 |    0.01 |        - |       - |    6780 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,291.5 μs** | **187.77 μs** | **124.20 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,911.4 μs | 718.50 μs | 427.57 μs |  1.16 |    0.04 |        - |       - |   57086 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **101.5 μs** |   **0.81 μs** |   **0.54 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    108.8 μs |  23.11 μs |  15.29 μs |  1.07 |    0.14 |        - |       - |      65 B |       0.002 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,100.2 μs** | **103.62 μs** |  **68.54 μs** |  **1.00** |    **0.08** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,099.4 μs | 317.24 μs | 209.83 μs |  1.00 |    0.19 |        - |       - |    1359 B |       0.004 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **860.4 μs** |  **32.14 μs** |  **19.13 μs** |  **1.00** |    **0.03** |   **7.0801** |       **-** |  **121234 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    734.7 μs | 150.91 μs |  99.82 μs |  0.85 |    0.11 |        - |       - |    1413 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,571.4 μs** | **176.53 μs** | **105.05 μs** |  **1.00** |    **0.02** |  **72.2656** |       **-** | **1212715 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,439.5 μs | 660.19 μs | 392.87 μs |  0.75 |    0.04 |        - |       - |    7852 B |       0.006 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,465.7 μs** |  **29.09 μs** |  **19.24 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    249.4 μs |  11.39 μs |   7.53 μs |  0.05 |    0.00 |        - |       - |     514 B |        0.43 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,471.8 μs** |  **20.70 μs** |  **12.32 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    243.4 μs |   9.35 μs |   6.18 μs |  0.04 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,478.4 μs** |  **20.46 μs** |  **13.53 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    245.9 μs |   8.34 μs |   5.52 μs |  0.04 |    0.00 |        - |       - |     512 B |        0.24 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,474.5 μs** |  **11.68 μs** |   **7.73 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    250.1 μs |  10.37 μs |   6.86 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.24 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **118.7 μs** |    **53.05 μs** |  **27.75 μs** |  **1.05** |    **0.31** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   123.8 μs |    15.40 μs |   8.05 μs |  1.09 |    0.23 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **138.7 μs** |    **60.08 μs** |  **31.42 μs** |  **1.04** |    **0.29** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   155.6 μs |     7.94 μs |   4.15 μs |  1.17 |    0.21 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **922.8 μs** |   **547.32 μs** | **286.26 μs** |  **1.07** |    **0.41** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   758.7 μs |   195.40 μs |  86.76 μs |  0.88 |    0.23 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,145.1 μs** |   **693.13 μs** | **307.75 μs** |  **1.05** |    **0.34** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,924.6 μs | 1,418.68 μs | 629.90 μs |  1.77 |    0.66 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,499.8 ns** |    **20.51 ns** |    **10.73 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   554.1 ns |   137.80 ns |    91.15 ns |  0.10 |    0.02 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,154.7 ns** | **2,226.98 ns** | **1,473.01 ns** |  **1.31** |    **1.01** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   950.3 ns |    55.35 ns |    28.95 ns |  0.39 |    0.23 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 562.88 ns | 16.542 ns | 2.560 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.19 ns |  0.121 ns | 0.019 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.38 ns |  0.273 ns | 0.071 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.22 ns |  0.168 ns | 0.026 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.000 μs** | **0.0061 μs** | **0.0016 μs** |         **-** |
| **WriteRequest** | **1**       | **2.002 μs** | **0.0022 μs** | **0.0006 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.395 μs** | **0.0088 μs** | **0.0023 μs** |         **-** |
| **WriteRequest** | **9**       | **2.515 μs** | **0.0274 μs** | **0.0042 μs** |         **-** |
| **WriteRequest** | **10**      | **2.485 μs** | **0.0457 μs** | **0.0119 μs** |         **-** |
| **WriteRequest** | **11**      | **2.386 μs** | **0.0062 μs** | **0.0016 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **106.08 ns** | **0.582 ns** | **0.090 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  95.08 ns | 0.135 ns | 0.035 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **93.98 ns** | **2.377 ns** | **0.617 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  90.01 ns | 0.540 ns | 0.084 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.7 ns |  1.28 ns |  0.67 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,933.3 ns |  2.90 ns |  1.52 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,307.0 ns |  9.28 ns |  5.52 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,319.0 ns | 10.82 ns |  6.44 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,912.9 ns |  1.86 ns |  1.10 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,924.5 ns | 18.48 ns | 11.00 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,849.9 ns |  5.87 ns |  3.50 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,740.0 ns |  9.96 ns |  5.93 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,115.9 ns |  0.67 ns |  0.44 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.7 ns |  3.70 ns |  2.44 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   787.1 ns |  3.80 ns |  2.52 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   814.8 ns |  3.18 ns |  2.11 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   141.4 ns |  0.12 ns |  0.07 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,666.3 ns |  6.67 ns |  3.97 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,280.2 ns |  1.59 ns |  0.83 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean          | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,211.632 ns | 13.5101 ns | 8.0397 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     12.658 ns |  0.0678 ns | 0.0404 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.902 ns |  0.0511 ns | 0.0304 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     33.006 ns |  0.2978 ns | 0.1970 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     44.219 ns |  0.2966 ns | 0.1551 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.580 ns |  0.0808 ns | 0.0481 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    171.910 ns |  1.1686 ns | 0.7729 ns |  1.00 |    0.01 | 0.0105 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     60.518 ns |  0.1301 ns | 0.0774 ns |  0.35 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     292.9 ns |   1.35 ns |   0.89 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,315.9 ns | 517.69 ns | 308.07 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     221.3 ns |   0.69 ns |   0.41 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 125,424.1 ns | 137.28 ns |  81.69 ns |      - |      80 B |

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