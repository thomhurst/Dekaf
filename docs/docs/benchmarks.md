---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-13 21:05 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 19× faster | 2.7× less | ⚠ Noisy |
| Produce — batches | on par to 2.3× faster | 22× less | Mixed |
| Produce — fire-and-forget | on par to 1.3× faster | 143× less | ⚠ Noisy |
| Consume — drain a topic | 1.6× slower to 1.3× faster | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.9× faster | 1.6× less | ⚠ Noisy |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.09 | 1.00–1.28 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.22 | 0.97–1.42 | 37% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.78 | 0.70–0.89 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.55 | 1.34–2.40 | 69% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.06–0.11 | 51% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.13–0.29 | 57% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.91 | 0.75–1.02 | 31% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.97 | 0.74–1.20 | 48% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.83 | 0.76–1.25 | 60% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.76 | 0.69–1.56 | 114% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.43 | 0.41–0.47 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.06 | 0.99–1.52 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 64% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.05 | 0.03–0.06 | 58% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.05 | 0.03–0.06 | 62% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.05 | 0.02–0.06 | 68% | ⚠ Low |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,085.2 μs** |   **136.06 μs** |  **89.99 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,662.3 μs |    36.65 μs |  24.24 μs |  0.44 |    0.01 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,574.4 μs** |    **90.62 μs** |  **59.94 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,812.3 μs |   215.15 μs | 142.31 μs |  0.50 |    0.02 |        - |       - |   50904 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,485.4 μs** |    **76.85 μs** |  **50.83 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,754.0 μs |    94.78 μs |  49.57 μs |  0.42 |    0.01 |        - |       - |    7250 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,191.9 μs** |   **153.35 μs** |  **91.26 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,373.6 μs | 1,440.58 μs | 857.27 μs |  1.11 |    0.07 |        - |       - |   58989 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **123.6 μs** |     **1.84 μs** |   **1.22 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    100.5 μs |     9.23 μs |   4.83 μs |  0.81 |    0.04 |        - |       - |     123 B |       0.004 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,209.7 μs** |   **105.04 μs** |  **62.51 μs** |  **1.00** |    **0.07** |  **17.5781** |       **-** |  **304004 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,190.6 μs |    81.69 μs |  54.03 μs |  0.99 |    0.07 |        - |       - |    1272 B |       0.004 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **978.0 μs** |    **27.06 μs** |  **16.10 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121886 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    798.7 μs |    76.06 μs |  45.26 μs |  0.82 |    0.05 |        - |       - |    1279 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,627.2 μs** |    **73.23 μs** |  **43.58 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215402 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,265.0 μs |   702.93 μs | 464.94 μs |  0.75 |    0.05 |        - |       - |   13788 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,481.1 μs** |    **15.37 μs** |   **9.15 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    289.3 μs |    10.73 μs |   7.10 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,485.5 μs** |    **30.83 μs** |  **16.12 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    297.7 μs |    14.93 μs |   9.88 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,487.3 μs** |     **9.32 μs** |   **6.16 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    298.7 μs |    18.44 μs |  12.20 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,480.9 μs** |    **16.99 μs** |  **11.24 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    290.7 μs |     7.68 μs |   5.08 μs |  0.05 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **135.6 μs** |    **43.02 μs** |  **22.50 μs** |  **1.02** |    **0.22** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   138.7 μs |    23.73 μs |  12.41 μs |  1.05 |    0.18 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **154.5 μs** |    **67.35 μs** |  **35.22 μs** |  **1.04** |    **0.30** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   157.3 μs |    16.98 μs |   7.54 μs |  1.06 |    0.20 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,145.6 μs** |   **583.92 μs** | **305.40 μs** |  **1.06** |    **0.38** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   782.2 μs |    60.59 μs |  26.90 μs |  0.73 |    0.18 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,450.1 μs** |   **821.55 μs** | **429.69 μs** |  **1.07** |    **0.41** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,752.1 μs | 1,211.64 μs | 537.97 μs |  1.30 |    0.51 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,551.9 ns** |    **17.96 ns** |    **10.69 ns** | **5,545.4 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   583.6 ns |   133.67 ns |    88.42 ns |   595.9 ns |  0.11 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,091.0 ns** | **1,580.63 ns** | **1,045.49 ns** | **3,715.8 ns** |  **1.17** |    **0.68** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,092.1 ns |   163.52 ns |   108.16 ns | 1,050.6 ns |  0.41 |    0.20 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 542.46 ns | 22.437 ns | 5.827 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.06 ns |  0.147 ns | 0.038 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.62 ns |  0.131 ns | 0.034 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.16 ns |  0.326 ns | 0.051 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.002 μs** | **0.0011 μs** | **0.0002 μs** |         **-** |
| **WriteRequest** | **1**       | **1.970 μs** | **0.0046 μs** | **0.0007 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.678 μs** | **0.0625 μs** | **0.0162 μs** |         **-** |
| **WriteRequest** | **9**       | **1.738 μs** | **0.1189 μs** | **0.0309 μs** |         **-** |
| **WriteRequest** | **10**      | **1.764 μs** | **0.1368 μs** | **0.0212 μs** |         **-** |
| **WriteRequest** | **11**      | **1.763 μs** | **0.3532 μs** | **0.0547 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **68.03 ns** | **4.076 ns** | **1.058 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 66.13 ns | 6.148 ns | 0.951 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **60.41 ns** | **4.065 ns** | **0.629 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 58.58 ns | 4.657 ns | 1.209 ns |         - |

| Method                                          | Mean       | Error     | StdDev    | Gen0   | Allocated |
|------------------------------------------------ |-----------:|----------:|----------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,317.0 ns |  24.87 ns |  14.80 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,670.7 ns |  40.69 ns |  26.92 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,754.9 ns |  77.06 ns |  45.85 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,707.2 ns |  53.35 ns |  35.29 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,464.7 ns |  30.72 ns |  20.32 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 2,673.9 ns | 127.32 ns |  84.22 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 2,852.1 ns | 191.52 ns | 126.68 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,438.9 ns |  91.97 ns |  60.84 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   981.2 ns |  46.69 ns |  30.88 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,738.9 ns |  69.99 ns |  46.30 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   594.9 ns |  17.59 ns |  11.64 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   621.6 ns |  21.23 ns |  14.04 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   147.7 ns |   7.10 ns |   4.70 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,387.0 ns |  27.94 ns |  18.48 ns | 0.0019 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,118.9 ns |  33.74 ns |  22.31 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean          | Error      | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|-----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,831.473 ns | 23.5313 ns | 15.5645 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     12.292 ns |  0.0385 ns |  0.0201 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     16.208 ns |  0.0436 ns |  0.0289 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     28.456 ns |  0.1361 ns |  0.0810 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     43.419 ns |  0.4479 ns |  0.2343 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.322 ns |  0.0232 ns |  0.0138 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    150.717 ns |  2.1536 ns |  1.4245 ns |  1.00 |    0.01 | 0.0105 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     57.861 ns |  0.0901 ns |  0.0471 ns |  0.38 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     296.5 ns |   0.78 ns |   0.52 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,428.9 ns | 192.67 ns | 114.65 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.4 ns |   0.89 ns |   0.59 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 125,445.2 ns |  77.63 ns |  46.20 ns |      - |      80 B |

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