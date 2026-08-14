---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-14 13:06 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 19×–20× faster | 3.0× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 25× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 200× less | ⚠ Noisy |
| Consume — drain a topic | 1.8× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.7×–10× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.10 | 0.93–1.20 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.24 | 1.01–1.50 | 39% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.71–1.02 | 39% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.80 | 0.98–2.40 | 79% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 53% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.27 | 0.13–0.29 | 59% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.88 | 0.75–1.12 | 43% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.93 | 0.74–1.09 | 38% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.81 | 0.73–1.25 | 64% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.78 | 0.69–1.56 | 111% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.44 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.49–0.51 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.47 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.10 | 1.03–1.52 | 44% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 63% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 61% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 60% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 67% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,884.6 μs** |   **103.07 μs** |  **68.17 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,566.8 μs |    25.93 μs |  15.43 μs |  0.44 |    0.01 |        - |       - |    5400 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,285.8 μs** |    **57.66 μs** |  **38.14 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,696.0 μs |    63.13 μs |  41.76 μs |  0.51 |    0.01 |        - |       - |   50327 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,654.5 μs** |    **28.03 μs** |  **18.54 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,886.9 μs |    69.95 μs |  46.27 μs |  0.43 |    0.01 |        - |       - |    6767 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,613.2 μs** |   **331.93 μs** | **173.61 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,948.7 μs | 1,064.94 μs | 633.73 μs |  1.13 |    0.06 |        - |       - |   55119 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **111.3 μs** |     **7.52 μs** |   **4.97 μs** |  **1.00** |    **0.06** |   **1.7090** |       **-** |   **30403 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    110.8 μs |    15.18 μs |  10.04 μs |  1.00 |    0.10 |        - |       - |     371 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,076.6 μs** |   **141.77 μs** |  **93.77 μs** |  **1.01** |    **0.13** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,079.4 μs |   184.22 μs | 121.85 μs |  1.01 |    0.15 |        - |       - |     811 B |       0.003 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **920.1 μs** |    **13.46 μs** |   **7.04 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121329 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    689.9 μs |    72.04 μs |  47.65 μs |  0.75 |    0.05 |        - |       - |     649 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,008.5 μs** |   **197.75 μs** | **117.68 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** | **1212772 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,609.3 μs | 1,574.65 μs | 937.05 μs |  0.84 |    0.10 |        - |       - |    6173 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,504.7 μs** |    **16.11 μs** |  **10.66 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    261.3 μs |    15.58 μs |   9.27 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,498.7 μs** |    **16.67 μs** |   **9.92 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    252.8 μs |     6.82 μs |   4.06 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.43 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,524.3 μs** |    **38.10 μs** |  **25.20 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    269.5 μs |     3.39 μs |   2.25 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.24 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,533.0 μs** |    **32.10 μs** |  **21.23 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    261.9 μs |     8.18 μs |   5.41 μs |  0.05 |    0.00 |        - |       - |     512 B |        0.24 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **120.7 μs** |    **33.62 μs** |  **17.58 μs** |  **1.02** |    **0.20** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   123.6 μs |    21.05 μs |   7.51 μs |  1.04 |    0.16 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **148.0 μs** |    **72.21 μs** |  **37.77 μs** |  **1.05** |    **0.34** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   181.1 μs |    18.28 μs |   9.56 μs |  1.29 |    0.27 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,011.6 μs** |   **620.93 μs** | **324.76 μs** |  **1.09** |    **0.45** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   795.3 μs |   210.91 μs |  93.65 μs |  0.85 |    0.26 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,441.7 μs** | **1,060.65 μs** | **554.74 μs** |  **1.12** |    **0.54** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,966.7 μs | 1,434.80 μs | 637.06 μs |  1.52 |    0.67 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,507.2 ns** |     **8.23 ns** |     **4.31 ns** | **5,507.1 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   576.3 ns |   119.43 ns |    71.07 ns |   575.2 ns |  0.10 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **2,967.2 ns** | **2,362.28 ns** | **1,562.50 ns** | **3,623.2 ns** |  **1.33** |    **1.04** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,050.6 ns |   116.91 ns |    61.15 ns | 1,045.1 ns |  0.47 |    0.26 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 592.67 ns | 23.466 ns | 6.094 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  30.80 ns |  0.255 ns | 0.066 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.24 ns |  0.051 ns | 0.013 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.15 ns |  0.039 ns | 0.010 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.020 μs** | **0.0026 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **1**       | **2.003 μs** | **0.0121 μs** | **0.0031 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.385 μs** | **0.0077 μs** | **0.0020 μs** |         **-** |
| **WriteRequest** | **9**       | **2.416 μs** | **0.0105 μs** | **0.0027 μs** |         **-** |
| **WriteRequest** | **10**      | **2.401 μs** | **0.0060 μs** | **0.0009 μs** |         **-** |
| **WriteRequest** | **11**      | **2.407 μs** | **0.0155 μs** | **0.0040 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.85 ns** | **0.494 ns** | **0.128 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 111.91 ns | 0.083 ns | 0.013 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **98.56 ns** | **0.469 ns** | **0.073 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  91.85 ns | 0.569 ns | 0.088 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,636.4 ns | 1.86 ns | 1.10 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,053.0 ns | 1.66 ns | 1.10 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,320.6 ns | 2.06 ns | 1.08 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,318.2 ns | 2.46 ns | 1.29 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,071.1 ns | 2.41 ns | 1.43 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,931.4 ns | 2.91 ns | 1.73 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,937.5 ns | 2.62 ns | 1.56 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,730.0 ns | 3.67 ns | 2.18 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,117.1 ns | 1.21 ns | 0.63 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.4 ns | 5.28 ns | 3.49 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   758.7 ns | 1.78 ns | 0.93 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   885.6 ns | 0.87 ns | 0.45 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   139.4 ns | 0.05 ns | 0.03 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,728.3 ns | 3.47 ns | 2.07 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,271.4 ns | 2.05 ns | 1.22 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,119.90 ns | 76.946 ns | 40.244 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.26 ns |  0.016 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.28 ns |  0.021 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     41.78 ns |  0.065 ns |  0.043 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.54 ns |  1.043 ns |  0.621 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     13.71 ns |  0.021 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    122.80 ns |  4.814 ns |  3.184 ns |  1.00 |    0.04 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.71 ns |  0.196 ns |  0.116 ns |  0.46 |    0.01 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     288.7 ns |   0.56 ns |  0.33 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,323.0 ns | 162.96 ns | 96.98 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     225.8 ns |   0.39 ns |  0.26 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,385.7 ns |  95.02 ns | 56.55 ns |      - |      80 B |

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