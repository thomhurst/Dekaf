---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 00:43 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.38 | 1.22–1.54 | 23% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.46 | 1.15–1.80 | 44% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.40 | 0.99–1.69 | 50% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.66 | 1.43–2.64 | 73% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 100 | 5 | 2.39 | 1.96–2.88 | 38% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 1000 | 5 | 2.30 | 2.02–2.75 | 32% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.01 | 0.86–1.09 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.04 | 0.84–1.08 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.98–1.20 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.83–1.26 | 45% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.43 | 0.42–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.52 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.42 | 0.41–0.45 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.04 | 0.96–1.56 | 58% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 2% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 2% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.47–0.47 | 2% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.47–0.47 | 1% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,112.5 μs** |    **69.97 μs** |  **46.28 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,676.3 μs |    12.66 μs |   8.38 μs |  0.44 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,374.0 μs** |    **40.04 μs** |  **26.48 μs** |  **1.00** |    **0.00** |  **62.5000** | **23.4375** | **1048384 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,774.6 μs |    64.21 μs |  42.47 μs |  0.51 |    0.01 |        - |       - |   51804 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,662.3 μs** |    **97.89 μs** |  **58.26 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,712.4 μs |    50.52 μs |  30.06 μs |  0.41 |    0.01 |        - |       - |    6301 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,065.4 μs** |   **201.43 μs** | **133.24 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,493.0 μs | 1,450.81 μs | 863.35 μs |  1.04 |    0.07 |  15.6250 |       - |  347850 B |        0.18 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.5 μs** |     **3.64 μs** |   **2.41 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    126.5 μs |    25.23 μs |  16.69 μs |  1.01 |    0.13 |        - |       - |     159 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,254.1 μs** |    **24.75 μs** |  **14.73 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,324.3 μs |    55.46 μs |  29.00 μs |  1.06 |    0.02 |        - |       - |    2344 B |       0.008 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,040.4 μs** |     **8.43 μs** |   **5.02 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121781 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,031.5 μs |    44.20 μs |  29.24 μs |  0.99 |    0.03 |        - |       - |    1949 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,239.9 μs** |    **78.84 μs** |  **46.92 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214831 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,150.7 μs |   522.97 μs | 345.91 μs |  0.99 |    0.03 |        - |       - |   18896 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,491.1 μs** |     **7.15 μs** |   **4.73 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,590.2 μs |     4.20 μs |   2.50 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,486.4 μs** |     **8.40 μs** |   **5.00 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,609.6 μs |     9.81 μs |   6.49 μs |  0.48 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,495.8 μs** |     **5.89 μs** |   **3.90 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,609.6 μs |    16.02 μs |  10.59 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,494.1 μs** |     **8.29 μs** |   **4.94 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,598.2 μs |     9.39 μs |   6.21 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **143.8 μs** |    **78.29 μs** |  **40.95 μs** |   **156.3 μs** |  **1.08** |    **0.43** |   **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   175.3 μs |    71.01 μs |  31.53 μs |   160.7 μs |  1.32 |    0.44 |   40.16 KB |        0.62 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **149.2 μs** |    **80.52 μs** |  **42.11 μs** |   **127.5 μs** |  **1.06** |    **0.38** |  **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   214.8 μs |    38.36 μs |  20.07 μs |   204.2 μs |  1.53 |    0.38 |  215.95 KB |        0.90 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,032.8 μs** |   **464.08 μs** | **242.73 μs** |   **982.4 μs** |  **1.05** |    **0.33** |  **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,328.3 μs |   306.93 μs | 136.28 μs | 1,371.4 μs |  1.35 |    0.32 |  476.84 KB |        0.74 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,386.4 μs** |   **923.50 μs** | **483.01 μs** | **1,087.8 μs** |  **1.09** |    **0.46** |  **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,126.0 μs | 1,487.06 μs | 660.27 μs | 2,562.1 μs |  1.67 |    0.65 | 2234.65 KB |        0.93 | ⚠ Low |

| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **100**         |   **829.1 ns** |  **40.92 ns** |  **24.35 ns** |  **1.00** |    **0.04** |      **-** |     **648 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 100         | 1,983.2 ns | 203.51 ns | 134.61 ns |  2.39 |    0.17 |      - |     452 B |        0.70 | Stable |
|                      |             |            |           |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **1000**        | **1,474.9 ns** | **283.38 ns** | **187.44 ns** |  **1.01** |    **0.17** | **0.1000** |    **2448 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 1000        | 3,392.7 ns | 644.94 ns | 426.59 ns |  2.33 |    0.39 | 0.1000 |    2255 B |        0.92 | ⚠ Low |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 340.19 ns | 28.795 ns | 7.478 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  13.87 ns |  0.030 ns | 0.008 ns |      - |         - |
| WriteDescribeGroupsV6      |  24.72 ns |  0.167 ns | 0.043 ns |      - |         - |
| WriteListConfigResourcesV1 |  13.11 ns |  0.158 ns | 0.041 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.178 μs** | **0.0083 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **1**       | **1.181 μs** | **0.0042 μs** | **0.0006 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.392 μs** | **0.0241 μs** | **0.0037 μs** |         **-** |
| **WriteRequest** | **9**       | **2.399 μs** | **0.0051 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **10**      | **2.406 μs** | **0.0117 μs** | **0.0030 μs** |         **-** |
| **WriteRequest** | **11**      | **2.396 μs** | **0.0046 μs** | **0.0007 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **101.99 ns** | **0.263 ns** | **0.068 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 102.48 ns | 0.624 ns | 0.097 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **91.49 ns** | **0.508 ns** | **0.079 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.50 ns | 0.055 ns | 0.008 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.5 ns |  2.14 ns | 1.42 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,040.2 ns |  4.29 ns | 2.55 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,338.3 ns |  2.57 ns | 1.70 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,274.6 ns |  9.04 ns | 5.98 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,894.7 ns |  4.67 ns | 2.44 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,990.3 ns |  6.38 ns | 3.80 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,947.8 ns |  6.33 ns | 3.31 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,842.7 ns |  2.96 ns | 1.76 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.0 ns |  1.66 ns | 0.99 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,815.6 ns |  3.52 ns | 1.84 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   721.0 ns |  1.59 ns | 1.05 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   877.9 ns |  3.37 ns | 2.00 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   169.2 ns |  0.10 ns | 0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,687.1 ns | 14.62 ns | 9.67 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,308.4 ns |  1.02 ns | 0.53 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,288.20 ns | 21.594 ns | 12.850 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |  0.004 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns |  0.008 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.04 ns |  0.024 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.66 ns |  0.232 ns |  0.154 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.008 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.16 ns |  1.665 ns |  0.871 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.27 ns |  0.085 ns |  0.057 ns |  0.49 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    172.8 ns |   1.15 ns |   0.60 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 46,593.9 ns | 183.64 ns | 109.28 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    125.3 ns |   1.38 ns |   0.82 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 70,129.1 ns | 156.58 ns |  81.89 ns |      - |      80 B |

---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Unit tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **RatioSD**: BenchmarkDotNet's uncertainty for the latest run's ratio
- **Confidence**: `⚠ Low` when latest `RatioSD > 0.30` or rolling run spread exceeds 30%
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*