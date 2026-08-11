---
sidebar_position: 13
---

# Benchmark Results

How Dekaf compares to Confluent.Kafka, measured with BenchmarkDotNet on GitHub Actions and refreshed on every commit to main.

**Last Updated:** 2026-08-11 17:21 UTC

## At a glance

Each scenario is the median Dekaf-vs-Confluent result over the last 10 CI runs (both clients measured on the same runner), aggregated across message and batch sizes. Memory compares heap allocations per operation from the latest run.

| Scenario | Speed vs Confluent | Memory vs Confluent | Confidence |
|---|---|---|---|
| Produce — one message at a time (awaited) | 18× faster | 2.7× less | Stable |
| Produce — batches | on par to 2.3× faster | 22× less | Stable |
| Produce — fire-and-forget | on par to 1.3× faster | 167× less | Stable |
| Consume — drain a topic | 1.5× slower to on par | 1.6× less | Mixed |
| Consume — poll a single message | 3.6×–9.9× faster | 1.6× less | Stable |

"On par" means within ±20% — differences that small are runner noise. A range means the result depends on message or batch size; the per-parameter tables below have the detail.

## Full results

<details>
<summary>Cross-run comparison — last 10 runs, per parameter set</summary>

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 10 | 1.08 | 0.94–1.28 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 10 | 1.18 | 1.00–1.42 | 35% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 10 | 0.88 | 0.72–0.91 | 22% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 10 | 1.51 | 1.47–1.87 | 26% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 10 | 0.10 | 0.09–0.11 | 18% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 10 | 0.28 | 0.27–0.29 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 10 | 0.91 | 0.81–1.02 | 24% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 10 | 0.98 | 0.91–1.20 | 30% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 10 | 0.80 | 0.74–0.95 | 26% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 10 | 0.78 | 0.69–0.85 | 20% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 10 | 0.43 | 0.43–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 10 | 0.50 | 0.48–0.51 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 10 | 0.44 | 0.41–0.46 | 13% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 10 | 1.04 | 0.99–1.09 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 24% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 26% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 10 | 0.06 | 0.05–0.06 | 21% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 10 | 0.06 | 0.05–0.06 | 20% | Stable |

</details>

<details>
<summary>Latest run — producer benchmarks</summary>

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,203.0 μs** |   **113.83 μs** |  **75.29 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,685.0 μs |    20.34 μs |  13.45 μs |  0.43 |    0.01 |        - |       - |    5464 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,660.6 μs** |    **63.75 μs** |  **42.17 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048416 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,724.9 μs |   104.52 μs |  62.20 μs |  0.49 |    0.01 |        - |       - |   50957 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,413.1 μs** |   **110.22 μs** |  **65.59 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,738.6 μs |   125.31 μs |  82.89 μs |  0.43 |    0.01 |        - |       - |    7249 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,931.8 μs** |   **138.42 μs** |  **72.40 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,591.4 μs | 1,086.76 μs | 568.40 μs |  1.06 |    0.05 |        - |       - |   59196 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **126.6 μs** |     **1.94 μs** |   **1.16 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    120.9 μs |    18.55 μs |  12.27 μs |  0.96 |    0.09 |        - |       - |     101 B |       0.003 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,283.2 μs** |     **8.20 μs** |   **4.88 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,225.0 μs |   219.52 μs | 145.20 μs |  0.95 |    0.11 |        - |       - |    1286 B |       0.004 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,024.8 μs** |     **6.71 μs** |   **3.99 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121494 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    784.1 μs |   109.66 μs |  72.54 μs |  0.77 |    0.07 |        - |       - |    1093 B |       0.009 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,286.1 μs** |   **341.28 μs** | **203.09 μs** |  **1.00** |    **0.03** |  **70.3125** |       **-** | **1214831 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,987.5 μs | 1,050.54 μs | 694.87 μs |  0.78 |    0.07 |        - |       - |    9764 B |       0.008 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,488.4 μs** |     **6.82 μs** |   **4.06 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |    307.4 μs |    11.44 μs |   7.57 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,489.9 μs** |    **11.80 μs** |   **7.02 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |    309.7 μs |     7.70 μs |   5.09 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.48 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,497.1 μs** |    **14.57 μs** |   **9.64 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |    315.4 μs |     8.05 μs |   5.33 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.27 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,494.9 μs** |    **10.29 μs** |   **6.12 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |    313.3 μs |    10.76 μs |   7.12 μs |  0.06 |    0.00 |        - |       - |     576 B |        0.27 | Stable |

</details>

<details>
<summary>Latest run — consumer benchmarks</summary>

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **118.0 μs** |    **28.81 μs** |  **12.79 μs** |  **1.01** |    **0.14** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   133.7 μs |     9.00 μs |   3.21 μs |  1.14 |    0.10 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **133.4 μs** |     **8.45 μs** |   **3.01 μs** |  **1.00** |    **0.03** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   175.3 μs |     5.88 μs |   2.61 μs |  1.31 |    0.03 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **925.6 μs** |   **314.96 μs** | **139.84 μs** |  **1.02** |    **0.19** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   764.7 μs |    54.71 μs |  24.29 μs |  0.84 |    0.11 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,474.6 μs** |   **868.34 μs** | **454.16 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,701.1 μs | 1,205.97 μs | 535.46 μs |  1.25 |    0.50 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,574.4 ns** |    **16.93 ns** |  **10.08 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   583.8 ns |   150.50 ns |  99.55 ns |  0.10 |    0.02 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,486.4 ns** | **1,561.38 ns** | **816.63 ns** |  **1.11** |    **0.55** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,039.6 ns |   119.78 ns |  71.28 ns |  0.33 |    0.15 | 0.1225 |    2074 B |        0.85 | Stable |

</details>

<details>
<summary>Protocol serialization — Dekaf internals</summary>

Wire protocol serialization/deserialization. **Allocated = `-` means zero heap allocations** — the goal of Dekaf's design.

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 570.16 ns | 19.925 ns | 5.175 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.03 ns |  0.087 ns | 0.023 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.04 ns |  0.248 ns | 0.064 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.21 ns |  0.237 ns | 0.062 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.003 μs** | **0.0155 μs** | **0.0040 μs** |         **-** |
| **WriteRequest** | **1**       | **1.970 μs** | **0.0306 μs** | **0.0079 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.453 μs** | **0.0240 μs** | **0.0037 μs** |         **-** |
| **WriteRequest** | **9**       | **2.472 μs** | **0.0117 μs** | **0.0030 μs** |         **-** |
| **WriteRequest** | **10**      | **2.455 μs** | **0.0031 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **11**      | **2.467 μs** | **0.0106 μs** | **0.0028 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **105.57 ns** | **1.003 ns** | **0.155 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.43 ns | 0.618 ns | 0.096 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.02 ns** | **0.388 ns** | **0.101 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  94.44 ns | 0.437 ns | 0.068 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,748.5 ns | 11.89 ns | 7.86 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,246.6 ns |  2.62 ns | 1.74 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,453.6 ns |  2.77 ns | 1.65 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,425.3 ns |  3.18 ns | 1.89 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,998.3 ns |  2.41 ns | 1.26 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,030.8 ns |  4.59 ns | 2.40 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,012.3 ns |  5.04 ns | 3.00 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,903.4 ns | 10.97 ns | 6.53 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.0 ns |  0.91 ns | 0.54 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,042.7 ns |  4.25 ns | 2.22 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   726.1 ns |  2.87 ns | 1.90 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   803.9 ns |  1.02 ns | 0.67 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   175.8 ns |  0.58 ns | 0.35 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,709.2 ns |  8.70 ns | 4.55 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,193.9 ns |  1.82 ns | 1.21 ns |      - |         - |

</details>

<details>
<summary>Serializers — Dekaf internals</summary>

| Method                               | Categories | Mean          | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,137.841 ns | 9.8409 ns | 5.8562 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     12.487 ns | 0.0208 ns | 0.0124 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     15.671 ns | 0.0241 ns | 0.0144 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     30.054 ns | 0.2381 ns | 0.1417 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     44.251 ns | 0.1769 ns | 0.1170 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.565 ns | 0.0194 ns | 0.0128 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    156.009 ns | 1.4532 ns | 0.9612 ns |  1.00 |    0.01 | 0.0105 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     60.643 ns | 0.0781 ns | 0.0517 ns |  0.39 |    0.00 |      - |         - |        0.00 |

</details>

<details>
<summary>Compression — Dekaf internals</summary>

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     298.5 ns |   0.88 ns |   0.58 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,590.2 ns | 124.60 ns |  82.42 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     224.0 ns |   0.80 ns |   0.48 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,483.5 ns | 546.43 ns | 285.79 ns |      - |      80 B |

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