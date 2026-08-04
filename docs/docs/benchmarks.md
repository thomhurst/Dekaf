---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-04 17:58 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.1× faster | 2.4× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 100× less | Mixed |
| Consume — drain a topic | 1.3× slower to 1.4× faster | 1.6× less | Mixed |
| Consume — poll a single message | 2.9×–12× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 0.99 | 0.85–1.09 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.07 | 0.94–1.31 | 34% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.71 | 0.64–0.86 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.27 | 0.96–1.86 | 71% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.07–0.10 | 30% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.35 | 0.16–0.41 | 73% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.99 | 0.79–1.13 | 35% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.03 | 0.85–1.14 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.99 | 0.80–1.14 | 34% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.96 | 0.75–1.11 | 37% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.51 | 0.50–0.53 | 6% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.48 | 19% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.97–1.48 | 49% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.47 | 0.46–0.48 | 4% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,199.6 μs** | **144.05 μs** |  **85.72 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,695.2 μs |  45.75 μs |  30.26 μs |  0.43 |    0.01 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,608.2 μs** |  **47.91 μs** |  **28.51 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,777.2 μs |  97.06 μs |  57.76 μs |  0.50 |    0.01 |        - |       - |   51862 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,390.6 μs** |  **80.00 μs** |  **52.91 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,773.0 μs |  42.55 μs |  25.32 μs |  0.43 |    0.01 |        - |       - |    7795 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,085.8 μs** | **204.93 μs** | **107.18 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,628.6 μs | 693.94 μs | 412.95 μs |  1.04 |    0.03 |        - |       - |   71413 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.9 μs** |   **1.83 μs** |   **1.09 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    111.9 μs |  20.81 μs |  13.76 μs |  0.87 |    0.10 |        - |       - |     236 B |       0.008 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,278.3 μs** |  **25.58 μs** |  **16.92 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,289.7 μs | 220.11 μs | 145.59 μs |  1.01 |    0.11 |        - |       - |    7130 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,017.6 μs** |   **7.90 μs** |   **4.70 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121496 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    831.3 μs | 137.08 μs |  90.67 μs |  0.82 |    0.09 |        - |       - |    1815 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,108.7 μs** | **105.18 μs** |  **62.59 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214541 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,603.1 μs | 697.58 μs | 461.41 μs |  0.75 |    0.04 |        - |       - |   17018 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,487.1 μs** |  **12.43 μs** |   **7.39 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,580.6 μs |  13.36 μs |   7.95 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,492.2 μs** |   **9.63 μs** |   **5.73 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,588.6 μs |  17.94 μs |  11.87 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,501.3 μs** |  **18.42 μs** |  **12.18 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,600.2 μs |   8.27 μs |   4.92 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,504.3 μs** |  **13.48 μs** |   **8.91 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,593.7 μs |  17.43 μs |  11.53 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **139.8 μs** |    **56.05 μs** |  **29.32 μs** |  **1.04** |    **0.28** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   130.2 μs |    12.53 μs |   5.56 μs |  0.97 |    0.18 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **165.1 μs** |    **78.23 μs** |  **40.92 μs** |  **1.05** |    **0.33** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   176.0 μs |    10.83 μs |   5.66 μs |  1.12 |    0.23 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,117.6 μs** |   **566.53 μs** | **296.31 μs** |  **1.06** |    **0.35** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   787.9 μs |    81.96 μs |  36.39 μs |  0.74 |    0.16 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,471.2 μs** |   **879.01 μs** | **459.74 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,696.0 μs | 1,156.49 μs | 513.49 μs |  1.25 |    0.49 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,575.8 ns** |    **14.88 ns** |   **7.78 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   546.6 ns |   149.12 ns |  98.64 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,483.4 ns** | **1,519.53 ns** | **794.74 ns** |  **1.10** |    **0.52** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,187.5 ns |   284.45 ns | 188.15 ns |  0.37 |    0.17 | 0.1225 |    2075 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 469.98 ns | 4.134 ns | 0.640 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.19 ns | 0.280 ns | 0.073 ns |      - |         - |
| WriteDescribeGroupsV6      |  46.57 ns | 0.412 ns | 0.107 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.47 ns | 0.108 ns | 0.028 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.197 μs** | **0.0015 μs** | **0.0004 μs** |         **-** |
| **WriteRequest** | **1**       | **2.072 μs** | **0.0011 μs** | **0.0002 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.425 μs** | **0.0100 μs** | **0.0026 μs** |         **-** |
| **WriteRequest** | **9**       | **2.385 μs** | **0.0061 μs** | **0.0016 μs** |         **-** |
| **WriteRequest** | **10**      | **2.423 μs** | **0.0189 μs** | **0.0029 μs** |         **-** |
| **WriteRequest** | **11**      | **2.669 μs** | **0.0091 μs** | **0.0024 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.11 ns** | **0.506 ns** | **0.078 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  95.93 ns | 0.126 ns | 0.033 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **97.35 ns** | **0.797 ns** | **0.207 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  91.39 ns | 1.092 ns | 0.169 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.8 ns | 1.25 ns | 0.66 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,155.6 ns | 3.50 ns | 2.32 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,338.4 ns | 4.46 ns | 2.65 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,291.1 ns | 2.79 ns | 1.46 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,101.9 ns | 2.36 ns | 1.24 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,034.8 ns | 6.10 ns | 3.63 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,943.5 ns | 4.93 ns | 2.93 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,848.3 ns | 5.03 ns | 3.33 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.4 ns | 1.48 ns | 0.77 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,818.3 ns | 4.47 ns | 2.96 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   711.6 ns | 1.88 ns | 0.98 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   807.5 ns | 4.99 ns | 3.30 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   169.0 ns | 0.21 ns | 0.12 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,716.7 ns | 7.84 ns | 5.19 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,299.7 ns | 2.55 ns | 1.52 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,700.37 ns | 31.019 ns | 18.459 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.14 ns |  0.007 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.90 ns |  0.021 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.74 ns |  0.207 ns |  0.123 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.18 ns |  0.362 ns |  0.239 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.98 ns |  0.069 ns |  0.036 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    113.38 ns |  3.215 ns |  2.126 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.54 ns |  0.155 ns |  0.103 ns |  0.50 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|---------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     326.5 ns |  0.90 ns |  0.53 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,570.4 ns | 73.89 ns | 43.97 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     211.5 ns |  0.33 ns |  0.19 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,256.3 ns | 62.12 ns | 41.09 ns |      - |      80 B |

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