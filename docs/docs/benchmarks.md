---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-04 21:01 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 2.1× faster | 2.4× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par | 100× less | ⚠ Noisy |
| Consume — drain a topic | 1.4× slower to 1.4× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.3×–12× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.03 | 0.85–1.16 | 30% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.18 | 0.94–1.38 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.73 | 0.64–0.95 | 42% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.37 | 0.96–1.86 | 66% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.09 | 0.06–0.11 | 52% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.30 | 0.14–0.41 | 89% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.95 | 0.79–1.13 | 36% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 1.00 | 0.58–1.20 | 62% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.96 | 0.77–1.14 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.97 | 0.75–1.39 | 66% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.42–0.44 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.51 | 0.50–0.53 | 6% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.40–0.48 | 18% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.03 | 0.97–1.92 | 92% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.47 | 0.44–0.47 | 7% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.47 | 0.44–0.48 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.47 | 0.44–0.48 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.47 | 0.45–0.47 | 6% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,833.33 μs** |   **133.306 μs** |    **88.174 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,452.57 μs |    37.332 μs |    22.216 μs |  0.42 |    0.01 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **6,822.11 μs** |    **40.130 μs** |    **23.881 μs** |  **1.00** |    **0.00** |  **62.5000** | **23.4375** | **1048384 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,540.04 μs |   121.407 μs |    80.303 μs |  0.52 |    0.01 |        - |       - |   51926 B |        0.05 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,300.64 μs** |    **35.705 μs** |    **21.247 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,670.38 μs |    70.907 μs |    46.901 μs |  0.42 |    0.01 |        - |       - |    7840 B |        0.04 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **8,387.15 μs** |   **342.505 μs** |   **226.546 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 14,631.45 μs | 6,462.616 μs | 4,274.620 μs |  1.75 |    0.49 |        - |       - |   75512 B |        0.04 | ⚠ Low |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |     **84.44 μs** |     **2.775 μs** |     **1.836 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     90.27 μs |    26.784 μs |    17.716 μs |  1.07 |    0.20 |        - |       - |     474 B |        0.02 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |    **776.20 μs** |    **55.480 μs** |    **36.697 μs** |  **1.00** |    **0.06** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    960.03 μs |   195.991 μs |   116.631 μs |  1.24 |    0.15 |        - |       - |    2129 B |       0.007 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **699.74 μs** |    **38.865 μs** |    **20.327 μs** |  **1.00** |    **0.04** |   **7.0801** |       **-** |  **121047 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    544.57 μs |   142.291 μs |    94.117 μs |  0.78 |    0.13 |        - |       - |    1324 B |        0.01 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **6,881.30 μs** |   **469.754 μs** |   **279.543 μs** |  **1.00** |    **0.05** |  **72.2656** |       **-** | **1210446 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,205.08 μs | 1,902.816 μs | 1,132.335 μs |  1.05 |    0.16 |        - |       - |   16294 B |        0.01 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,399.49 μs** |    **21.124 μs** |    **13.972 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,419.04 μs |    18.129 μs |    11.991 μs |  0.45 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,401.97 μs** |    **36.545 μs** |    **19.114 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,423.02 μs |    16.522 μs |    10.928 μs |  0.45 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,407.69 μs** |    **11.798 μs** |     **7.804 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,393.07 μs |     7.655 μs |     5.063 μs |  0.44 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |              |              |              |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,409.90 μs** |    **15.242 μs** |    **10.082 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,417.58 μs |    11.647 μs |     7.704 μs |  0.45 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean        | Error        | StdDev     | Median    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |------------:|-------------:|-----------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |    **92.65 μs** |    **39.579 μs** |  **20.700 μs** |  **82.59 μs** |  **1.04** |    **0.30** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |    95.50 μs |     2.925 μs |   1.530 μs |  95.48 μs |  1.07 |    0.20 |  26.45 KB |        0.41 | Stable |
|                      |              |             |             |              |            |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **112.45 μs** |    **49.670 μs** |  **25.978 μs** |  **96.83 μs** |  **1.04** |    **0.30** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   133.41 μs |    13.190 μs |   6.899 μs | 133.80 μs |  1.23 |    0.23 | 202.23 KB |        0.84 | Stable |
|                      |              |             |             |              |            |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **738.01 μs** |   **491.623 μs** | **257.128 μs** | **579.92 μs** |  **1.09** |    **0.46** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   537.82 μs |    96.686 μs |  42.929 μs | 548.62 μs |  0.79 |    0.21 | 258.48 KB |        0.40 | Stable |
|                      |              |             |             |              |            |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        |   **933.64 μs** |   **629.467 μs** | **279.487 μs** | **784.05 μs** |  **1.06** |    **0.38** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,217.97 μs | 1,161.433 μs | 515.684 μs | 894.04 μs |  1.38 |    0.63 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,390.2 ns** |    **11.75 ns** |   **6.99 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   437.9 ns |    68.18 ns |  45.10 ns |  0.08 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,180.7 ns** | **1,663.45 ns** | **870.02 ns** |  **1.18** |    **0.78** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        |   759.5 ns |    71.14 ns |  42.33 ns |  0.28 |    0.17 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 485.99 ns | 7.544 ns | 1.167 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  29.10 ns | 0.142 ns | 0.022 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.73 ns | 0.131 ns | 0.034 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.49 ns | 0.242 ns | 0.063 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.071 μs** | **0.0014 μs** | **0.0002 μs** |         **-** |
| **WriteRequest** | **1**       | **2.073 μs** | **0.0036 μs** | **0.0006 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.445 μs** | **0.0113 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **9**       | **2.474 μs** | **0.1055 μs** | **0.0274 μs** |         **-** |
| **WriteRequest** | **10**      | **2.450 μs** | **0.0074 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **11**      | **2.467 μs** | **0.0102 μs** | **0.0027 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **104.34 ns** | **0.765 ns** | **0.199 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.74 ns | 0.381 ns | 0.099 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **98.87 ns** | **0.260 ns** | **0.068 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  95.41 ns | 0.626 ns | 0.097 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,740.0 ns | 11.27 ns |  6.70 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,073.7 ns |  3.84 ns |  2.01 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,486.4 ns | 42.54 ns | 28.14 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,422.2 ns |  2.68 ns |  1.40 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,003.7 ns |  2.09 ns |  1.38 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,031.2 ns |  3.57 ns |  2.13 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,959.3 ns | 21.90 ns | 11.46 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,920.2 ns | 12.34 ns |  8.16 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,191.6 ns |  1.00 ns |  0.60 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,038.0 ns |  1.58 ns |  0.83 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   772.8 ns |  1.16 ns |  0.69 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   793.0 ns |  1.68 ns |  1.00 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   161.7 ns |  0.09 ns |  0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,723.6 ns |  5.72 ns |  2.99 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,181.2 ns |  1.43 ns |  0.85 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,080.25 ns | 19.718 ns | 13.042 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.14 ns |  0.013 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.91 ns |  0.029 ns |  0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.65 ns |  0.023 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.28 ns |  0.269 ns |  0.178 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.96 ns |  0.012 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.54 ns |  2.117 ns |  1.260 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.10 ns |  0.065 ns |  0.034 ns |  0.50 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     298.2 ns |   1.07 ns |   0.63 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,684.3 ns | 104.39 ns |  69.05 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     206.6 ns |   1.16 ns |   0.69 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,249.2 ns | 215.93 ns | 128.50 ns |      - |      80 B |

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