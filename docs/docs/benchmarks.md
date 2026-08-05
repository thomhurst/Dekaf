---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-05 22:28 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 20× faster | 2.4× less | ⚠ Noisy |
| Produce — batches | on par to 2.4× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 118× less | ⚠ Noisy |
| Consume — drain a topic | 1.5× slower to 1.3× faster | 1.6× less | ⚠ Noisy |
| Consume — poll a single message | 3.7×–11× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.17 | 1.02–1.46 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.25 | 0.99–1.50 | 41% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.77 | 0.70–1.08 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.48 | 0.80–2.22 | 96% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 49% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.14–0.32 | 66% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.96 | 0.77–1.26 | 51% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.99 | 0.58–1.20 | 63% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.86 | 0.76–1.08 | 37% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.86 | 0.74–1.39 | 75% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.53 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.42 | 0.41–0.44 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.15 | 0.99–1.92 | 81% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.47 | 895% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.47 | 886% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.47 | 879% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.03–0.47 | 873% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,869.90 μs** |   **148.484 μs** |    **88.361 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,455.75 μs |    20.498 μs |    10.721 μs |  0.42 |    0.01 |        - |       - |    5512 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **6,832.28 μs** |   **104.464 μs** |    **54.637 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,526.66 μs |   152.468 μs |   100.848 μs |  0.52 |    0.01 |        - |       - |   51820 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,300.68 μs** |    **26.200 μs** |    **13.703 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194790 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,913.65 μs |   639.261 μs |   422.831 μs |  0.46 |    0.06 |        - |       - |    7868 B |        0.04 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **8,162.05 μs** |   **280.228 μs** |   **185.353 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,358.32 μs | 4,139.038 μs | 2,463.075 μs |  1.51 |    0.29 |        - |       - |   74552 B |        0.04 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |     **84.35 μs** |     **2.643 μs** |     **1.748 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    107.32 μs |    46.290 μs |    30.618 μs |  1.27 |    0.35 |        - |       - |     206 B |       0.007 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |    **852.39 μs** |    **18.951 μs** |    **12.535 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304002 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    883.92 μs |   336.540 μs |   222.600 μs |  1.04 |    0.25 |        - |       - |    2138 B |       0.007 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **727.33 μs** |    **47.602 μs** |    **28.327 μs** |  **1.00** |    **0.05** |   **7.0801** |       **-** |  **121177 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    674.26 μs |   196.161 μs |   129.748 μs |  0.93 |    0.17 |        - |       - |    4102 B |        0.03 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,012.16 μs** |   **601.837 μs** |   **314.772 μs** |  **1.00** |    **0.06** |  **72.2656** |       **-** | **1209703 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,320.73 μs | 2,254.213 μs | 1,341.446 μs |  0.90 |    0.19 |        - |       - |   17918 B |        0.01 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,410.41 μs** |    **25.490 μs** |    **16.860 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    186.79 μs |    10.893 μs |     6.482 μs |  0.03 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,445.74 μs** |   **114.787 μs** |    **68.308 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    183.45 μs |    11.736 μs |     6.984 μs |  0.03 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,412.85 μs** |    **21.946 μs** |    **11.478 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    185.16 μs |     7.002 μs |     4.167 μs |  0.03 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,413.81 μs** |    **19.960 μs** |    **13.202 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    187.41 μs |     7.527 μs |     3.937 μs |  0.03 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |------------:|-------------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |    **84.67 μs** |    **24.858 μs** |  **13.001 μs** |  **1.02** |    **0.20** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |    93.86 μs |     7.088 μs |   3.147 μs |  1.13 |    0.15 |  26.45 KB |        0.41 | Stable |
|                      |              |             |             |              |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **113.18 μs** |    **47.174 μs** |  **24.673 μs** |  **1.04** |    **0.28** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   127.15 μs |    18.210 μs |   8.085 μs |  1.16 |    0.22 | 202.23 KB |        0.84 | Stable |
|                      |              |             |             |              |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **746.57 μs** |   **488.259 μs** | **255.369 μs** |  **1.08** |    **0.45** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   632.79 μs |    84.421 μs |  37.483 μs |  0.92 |    0.23 | 258.48 KB |        0.40 | Stable |
|                      |              |             |             |              |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,127.90 μs** |   **826.553 μs** | **432.303 μs** |  **1.12** |    **0.55** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,503.44 μs | 1,147.201 μs | 509.364 μs |  1.49 |    0.68 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,405.8 ns** |    **15.98 ns** |     **8.36 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   433.0 ns |    96.33 ns |    57.32 ns |  0.08 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,689.7 ns** | **2,539.08 ns** | **1,679.45 ns** |  **1.34** |    **1.14** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   811.2 ns |   132.35 ns |    78.76 ns |  0.29 |    0.20 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 555.48 ns | 12.323 ns | 1.907 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.07 ns |  0.075 ns | 0.012 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.08 ns |  0.431 ns | 0.067 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.22 ns |  0.224 ns | 0.058 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.977 μs** | **0.0403 μs** | **0.0105 μs** |         **-** |
| **WriteRequest** | **1**       | **1.962 μs** | **0.0060 μs** | **0.0016 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.370 μs** | **0.0064 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **9**       | **2.404 μs** | **0.0144 μs** | **0.0037 μs** |         **-** |
| **WriteRequest** | **10**      | **2.410 μs** | **0.0042 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **11**      | **2.392 μs** | **0.0125 μs** | **0.0019 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.04 ns** | **0.467 ns** | **0.072 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  95.94 ns | 0.069 ns | 0.011 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **93.26 ns** | **0.629 ns** | **0.097 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  90.13 ns | 0.226 ns | 0.035 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,636.8 ns |  4.93 ns | 2.58 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,958.4 ns |  6.23 ns | 3.26 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,327.5 ns |  5.41 ns | 2.83 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,715.5 ns |  8.08 ns | 5.34 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,920.9 ns |  2.41 ns | 1.26 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,060.7 ns | 12.93 ns | 8.55 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,926.8 ns |  5.63 ns | 3.35 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,850.9 ns | 10.82 ns | 7.15 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.8 ns |  1.56 ns | 0.81 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,818.5 ns |  6.63 ns | 3.47 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   780.8 ns |  2.83 ns | 1.87 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   827.2 ns |  1.51 ns | 0.90 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   166.9 ns |  0.52 ns | 0.31 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,698.3 ns |  9.25 ns | 6.12 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,310.5 ns |  0.96 ns | 0.50 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,841.65 ns | 32.512 ns | 17.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |  0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.73 ns |  0.033 ns |  0.022 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.37 ns |  0.023 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.43 ns |  0.395 ns |  0.235 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |  0.008 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.84 ns |  1.277 ns |  0.760 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     53.91 ns |  0.149 ns |  0.089 ns |  0.49 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     295.1 ns |   1.84 ns |   0.96 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,144.1 ns |  87.79 ns |  52.24 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     219.3 ns |   0.59 ns |   0.39 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 124,887.3 ns | 288.91 ns | 171.93 ns |      - |      80 B |

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