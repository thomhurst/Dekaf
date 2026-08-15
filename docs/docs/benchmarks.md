---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-15 04:50 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 21×–22× faster | 3.3× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 25× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 667× less | Mixed |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.8×–10× faster | 1.6× less | Mixed |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.16 | 1.00–1.33 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.35 | 1.06–1.51 | 33% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.76 | 0.68–0.97 | 38% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.81 | 1.17–2.40 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.08–0.10 | 21% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.18–0.43 | 95% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.90 | 0.74–1.42 | 76% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.98 | 0.89–1.05 | 17% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.79 | 0.73–0.93 | 25% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.82 | 0.75–1.05 | 36% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.40–0.46 | 14% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.10 | 1.00–1.28 | 25% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 47% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 51% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.04–0.06 | 47% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.04–0.06 | 51% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,838.77 μs** |    **80.524 μs** |  **47.919 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,548.37 μs |    19.381 μs |  11.533 μs |  0.44 |    0.00 |        - |       - |    5344 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,279.73 μs** |    **80.758 μs** |  **53.416 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,659.87 μs |    68.085 μs |  45.034 μs |  0.50 |    0.01 |        - |       - |   49768 B |        0.05 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,634.06 μs** |    **25.926 μs** |  **15.428 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,659.19 μs |    46.931 μs |  31.042 μs |  0.40 |    0.00 |        - |       - |    6333 B |        0.03 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,674.67 μs** |   **320.029 μs** | **211.680 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,250.37 μs | 1,714.320 μs | 896.622 μs |  1.05 |    0.08 |        - |       - |   51321 B |        0.03 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **118.36 μs** |     **2.077 μs** |   **1.236 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     98.93 μs |    18.976 μs |  12.552 μs |  0.84 |    0.10 |        - |       - |      34 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,079.24 μs** |    **16.018 μs** |   **9.532 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    966.99 μs |   189.863 μs | 112.984 μs |  0.90 |    0.10 |        - |       - |     424 B |       0.001 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **867.61 μs** |    **12.735 μs** |   **7.578 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121301 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    728.06 μs |   139.469 μs |  92.250 μs |  0.84 |    0.10 |        - |       - |     677 B |       0.006 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,718.20 μs** |   **191.698 μs** | **100.262 μs** |  **1.00** |    **0.02** |  **72.2656** |       **-** | **1212547 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,557.04 μs | 1,205.330 μs | 797.251 μs |  0.87 |    0.09 |        - |       - |    1990 B |       0.002 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,480.79 μs** |    **21.450 μs** |  **14.188 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    248.73 μs |     8.398 μs |   5.555 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,469.38 μs** |    **32.686 μs** |  **17.096 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    245.62 μs |     5.656 μs |   3.741 μs |  0.04 |    0.00 |        - |       - |     456 B |        0.38 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,508.11 μs** |    **19.468 μs** |  **12.877 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    249.82 μs |     7.286 μs |   4.336 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |
|                         |               |             |           |              |              |            |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,496.33 μs** |    **20.062 μs** |  **11.939 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    256.09 μs |     8.969 μs |   5.932 μs |  0.05 |    0.00 |        - |       - |     456 B |        0.22 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median      | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **104.9 μs** |    **34.68 μs** |  **15.40 μs** |    **98.89 μs** |  **1.02** |    **0.19** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   118.3 μs |     8.43 μs |   4.41 μs |   119.56 μs |  1.15 |    0.15 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **117.5 μs** |     **8.21 μs** |   **2.93 μs** |   **117.28 μs** |  **1.00** |    **0.03** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   168.8 μs |    26.27 μs |  11.66 μs |   167.53 μs |  1.44 |    0.10 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **962.0 μs** |   **548.27 μs** | **286.75 μs** |   **933.93 μs** |  **1.08** |    **0.42** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   714.2 μs |   322.25 μs | 143.08 μs |   636.19 μs |  0.80 |    0.26 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,363.7 μs** | **1,050.80 μs** | **549.59 μs** | **1,057.33 μs** |  **1.11** |    **0.54** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,995.0 μs | 1,376.54 μs | 611.19 μs | 2,327.74 μs |  1.63 |    0.67 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,492.1 ns** |    **45.85 ns** |  **23.98 ns** |  **1.00** |    **0.01** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   544.7 ns |   143.04 ns |  94.61 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,345.3 ns** | **1,512.25 ns** | **790.94 ns** |  **1.11** |    **0.56** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   964.9 ns |    68.81 ns |  35.99 ns |  0.32 |    0.14 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev    | Gen0   | Allocated |
|--------------------------- |----------:|----------:|----------:|-------:|----------:|
| ReadDescribeGroupsV5       | 389.48 ns | 43.577 ns | 11.317 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  16.64 ns |  2.332 ns |  0.361 ns |      - |         - |
| WriteDescribeGroupsV6      |  28.65 ns |  1.785 ns |  0.464 ns |      - |         - |
| WriteListConfigResourcesV1 |  15.48 ns |  2.493 ns |  0.647 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.504 μs** | **0.1132 μs** | **0.0294 μs** |         **-** |
| **WriteRequest** | **1**       | **1.526 μs** | **0.1478 μs** | **0.0384 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.893 μs** | **0.0007 μs** | **0.0001 μs** |         **-** |
| **WriteRequest** | **9**       | **1.896 μs** | **0.0024 μs** | **0.0006 μs** |         **-** |
| **WriteRequest** | **10**      | **1.915 μs** | **0.0121 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **11**      | **1.904 μs** | **0.0109 μs** | **0.0017 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **81.62 ns** | **0.767 ns** | **0.199 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 76.54 ns | 0.692 ns | 0.180 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **75.34 ns** | **0.468 ns** | **0.122 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 70.66 ns | 0.735 ns | 0.114 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,354.3 ns | 6.25 ns | 4.13 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,760.1 ns | 1.52 ns | 0.90 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,943.7 ns | 1.56 ns | 0.82 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,941.6 ns | 1.22 ns | 0.64 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,690.8 ns | 1.17 ns | 0.61 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,407.4 ns | 1.41 ns | 0.84 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,503.7 ns | 2.78 ns | 1.45 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,248.8 ns | 2.46 ns | 1.63 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   961.8 ns | 0.76 ns | 0.50 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,582.6 ns | 1.54 ns | 0.92 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   657.3 ns | 1.95 ns | 1.29 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   632.2 ns | 0.56 ns | 0.33 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   111.4 ns | 0.08 ns | 0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,265.2 ns | 4.93 ns | 3.26 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   889.0 ns | 0.64 ns | 0.38 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                                            | Mean       | Error     | StdDev    | Ratio  | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------- |-----------:|----------:|----------:|-------:|--------:|----------:|------------:|
| &#39;Prepare stable generic Avro schema&#39;              |   1.427 ns | 0.0702 ns | 0.1050 ns |   1.01 |    0.10 |         - |          NA |
| &#39;Prepare equivalent generic Avro schema instance&#39; | 156.526 ns | 3.1871 ns | 3.7940 ns | 110.23 |    8.25 |         - |          NA |

| Method                               | Categories | Mean         | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,722.97 ns | 5.427 ns | 3.230 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     14.54 ns | 0.083 ns | 0.055 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.24 ns | 0.043 ns | 0.026 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     36.09 ns | 0.022 ns | 0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     41.61 ns | 1.672 ns | 1.106 ns |     ? |       ? | 0.0089 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     10.67 ns | 0.058 ns | 0.038 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    135.64 ns | 3.036 ns | 1.807 ns |  1.00 |    0.02 | 0.0355 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     57.88 ns | 0.064 ns | 0.042 ns |  0.43 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean        | Error       | StdDev      | Gen0   | Allocated |
|------------------------ |------------:|------------:|------------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    229.2 ns |     6.10 ns |     3.63 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 67,042.6 ns | 2,566.83 ns | 1,697.80 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    151.1 ns |     7.69 ns |     5.09 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 87,802.8 ns | 3,195.70 ns | 2,113.76 ns |      - |      80 B |

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