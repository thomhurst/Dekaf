---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-05 01:18 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.1× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 67× less | ⚠ Noisy |
| Consume — drain a topic | 1.4× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.4×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.05 | 0.85–1.29 | 42% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.18 | 0.94–1.50 | 47% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.74 | 0.64–0.95 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.37 | 0.96–2.22 | 92% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 52% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.29 | 0.14–0.41 | 92% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.95 | 0.79–1.13 | 36% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.00 | 0.58–1.20 | 62% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.90 | 0.76–1.14 | 42% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.97 | 0.75–1.39 | 66% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.50–0.53 | 6% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.48 | 18% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.06 | 0.97–1.92 | 89% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.47 | 0.04–0.47 | 92% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.47 | 0.05–0.48 | 92% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.47 | 0.05–0.48 | 92% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.47 | 0.05–0.47 | 91% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,154.8 μs** |   **276.89 μs** | **183.15 μs** |  **1.00** |    **0.04** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,595.6 μs |    54.53 μs |  36.07 μs |  0.42 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,329.0 μs** |    **61.57 μs** |  **40.72 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,707.4 μs |    98.18 μs |  64.94 μs |  0.51 |    0.01 |        - |       - |   51759 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,686.2 μs** |    **47.46 μs** |  **28.24 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,726.9 μs |    46.13 μs |  30.51 μs |  0.41 |    0.00 |        - |       - |    7856 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,790.4 μs** |   **242.32 μs** | **144.20 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,370.2 μs | 1,021.85 μs | 675.89 μs |  1.15 |    0.06 |        - |       - |   73622 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **107.7 μs** |     **3.17 μs** |   **2.09 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    109.4 μs |    27.59 μs |  18.25 μs |  1.02 |    0.16 |        - |       - |     631 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,126.4 μs** |   **107.18 μs** |  **70.89 μs** |  **1.00** |    **0.09** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,098.0 μs |   262.59 μs | 173.68 μs |  0.98 |    0.16 |        - |       - |    2085 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **870.9 μs** |    **12.59 μs** |   **6.59 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121279 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    724.2 μs |   189.30 μs | 125.21 μs |  0.83 |    0.14 |        - |       - |    1815 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,665.7 μs** |    **61.25 μs** |  **36.45 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1212795 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,572.7 μs | 1,286.41 μs | 850.88 μs |  0.87 |    0.09 |        - |       - |   25550 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,482.3 μs** |    **25.87 μs** |  **15.40 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    246.5 μs |     6.26 μs |   4.14 μs |  0.04 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,481.3 μs** |    **16.56 μs** |   **9.86 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    247.6 μs |     5.59 μs |   3.70 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,485.9 μs** |    **16.61 μs** |  **10.99 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    249.2 μs |     6.51 μs |   3.87 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,484.3 μs** |    **20.24 μs** |  **13.39 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    250.7 μs |     6.22 μs |   4.12 μs |  0.05 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **113.4 μs** |    **39.44 μs** |  **20.63 μs** |   **102.7 μs** |  **1.03** |    **0.24** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   136.5 μs |    16.10 μs |   8.42 μs |   132.4 μs |  1.24 |    0.21 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **142.9 μs** |    **70.59 μs** |  **36.92 μs** |   **120.2 μs** |  **1.05** |    **0.33** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   174.1 μs |    34.00 μs |  15.10 μs |   180.2 μs |  1.28 |    0.28 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,020.1 μs** |   **597.71 μs** | **312.61 μs** |   **953.6 μs** |  **1.08** |    **0.43** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   740.0 μs |   150.61 μs |  66.87 μs |   766.9 μs |  0.78 |    0.22 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,285.4 μs** |   **812.89 μs** | **360.93 μs** | **1,030.5 μs** |  **1.06** |    **0.38** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,799.5 μs | 1,582.84 μs | 702.79 μs | 2,286.4 μs |  1.49 |    0.66 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,505.2 ns** |    **29.15 ns** |    **15.24 ns** | **5,502.0 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   581.4 ns |   110.77 ns |    73.27 ns |   589.6 ns |  0.11 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **2,972.4 ns** | **1,669.18 ns** | **1,104.06 ns** | **3,635.8 ns** |  **1.22** |    **0.79** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,043.4 ns |    63.92 ns |    33.43 ns | 1,035.7 ns |  0.43 |    0.22 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 365.06 ns | 4.008 ns | 1.041 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  23.10 ns | 0.143 ns | 0.037 ns |      - |         - |
| WriteDescribeGroupsV6      |  35.38 ns | 0.151 ns | 0.023 ns |      - |         - |
| WriteListConfigResourcesV1 |  15.11 ns | 0.185 ns | 0.029 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.606 μs** | **0.0027 μs** | **0.0004 μs** |         **-** |
| **WriteRequest** | **1**       | **1.606 μs** | **0.0010 μs** | **0.0003 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.404 μs** | **0.0120 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **9**       | **2.378 μs** | **0.0055 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **10**      | **2.396 μs** | **0.0049 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **11**      | **2.387 μs** | **0.0171 μs** | **0.0026 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.64 ns** | **0.294 ns** | **0.046 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  93.85 ns | 0.663 ns | 0.103 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **93.52 ns** | **1.018 ns** | **0.158 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  86.76 ns | 0.221 ns | 0.034 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,640.8 ns | 2.23 ns | 1.17 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,937.7 ns | 2.08 ns | 1.24 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,325.3 ns | 1.81 ns | 0.95 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,303.6 ns | 1.98 ns | 1.04 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,899.5 ns | 1.49 ns | 0.78 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,970.1 ns | 4.39 ns | 2.30 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,934.0 ns | 4.77 ns | 2.84 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,844.8 ns | 1.66 ns | 1.10 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.5 ns | 0.57 ns | 0.34 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.9 ns | 5.19 ns | 3.43 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   705.6 ns | 1.04 ns | 0.55 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   820.8 ns | 5.53 ns | 3.66 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.3 ns | 0.45 ns | 0.30 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,739.8 ns | 8.65 ns | 5.72 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,314.4 ns | 1.76 ns | 1.16 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,749.33 ns | 7.026 ns | 4.181 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     14.59 ns | 0.036 ns | 0.024 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.97 ns | 0.013 ns | 0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     35.87 ns | 0.015 ns | 0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     35.52 ns | 0.535 ns | 0.319 ns |     ? |       ? | 0.0089 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.07 ns | 0.059 ns | 0.039 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    118.22 ns | 1.173 ns | 0.698 ns |  1.00 |    0.01 | 0.0355 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     58.43 ns | 0.020 ns | 0.012 ns |  0.49 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error    | StdDev   | Gen0   | Allocated |
|------------------------ |------------:|---------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    227.9 ns |  0.41 ns |  0.24 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 80,992.8 ns | 65.50 ns | 43.33 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    163.1 ns |  0.15 ns |  0.09 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 80,051.0 ns | 81.98 ns | 48.78 ns |      - |      80 B |

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