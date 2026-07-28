---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 13:58 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.22 | 1.22–1.35 | 11% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.47 | 1.24–1.93 | 47% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.28 | 1.23–1.36 | 10% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 2.11 | 1.53–2.24 | 34% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 100 | 5 | 2.50 | 2.39–2.91 | 21% | Stable |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 1000 | 5 | 2.55 | 2.30–2.74 | 17% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.10 | 1.01–1.21 | 18% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.13 | 1.06–1.15 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.02 | 0.98–1.05 | 7% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 1.01 | 0.94–1.03 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.44 | 12% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.04 | 1.03–1.11 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,108.8 μs** |    **87.59 μs** |    **57.93 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,666.0 μs |    12.94 μs |     8.56 μs |  0.44 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,337.5 μs** |    **78.83 μs** |    **52.14 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,777.3 μs |    66.34 μs |    43.88 μs |  0.51 |    0.01 |        - |       - |   51803 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,433.5 μs** |    **75.79 μs** |    **45.10 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,849.8 μs |    60.02 μs |    39.70 μs |  0.44 |    0.01 |        - |       - |    6296 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,272.1 μs** |   **152.06 μs** |   **100.58 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,599.6 μs | 1,581.69 μs | 1,046.19 μs |  1.03 |    0.08 |        - |       - |  349258 B |        0.18 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.8 μs** |    **11.64 μs** |     **7.70 μs** |  **1.00** |    **0.08** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    145.9 μs |    20.45 μs |    13.53 μs |  1.08 |    0.11 |        - |       - |     206 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,291.7 μs** |    **60.12 μs** |    **39.77 μs** |  **1.00** |    **0.04** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,408.9 μs |   126.81 μs |    83.87 μs |  1.09 |    0.07 |        - |       - |    2267 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,070.1 μs** |    **15.42 μs** |    **10.20 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121566 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,043.6 μs |    57.47 μs |    38.01 μs |  0.98 |    0.04 |        - |       - |    1933 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,441.1 μs** |    **73.31 μs** |    **43.62 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215296 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,833.6 μs |   282.74 μs |   187.01 μs |  0.94 |    0.02 |        - |       - |   18564 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,478.9 μs** |     **8.23 μs** |     **5.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,576.9 μs |    22.06 μs |    14.59 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,472.7 μs** |    **11.45 μs** |     **6.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,574.1 μs |    11.89 μs |     7.86 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,479.2 μs** |     **7.31 μs** |     **4.35 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,591.2 μs |    20.75 μs |    13.73 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,477.1 μs** |     **7.62 μs** |     **5.04 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,577.4 μs |     9.98 μs |     6.60 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **124.5 μs** |    **45.88 μs** |  **24.00 μs** |  **1.03** |    **0.27** |   **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   152.3 μs |    18.82 μs |   8.36 μs |  1.26 |    0.24 |   40.16 KB |        0.62 | Stable |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **148.6 μs** |    **71.12 μs** |  **37.20 μs** |  **1.05** |    **0.33** |  **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   219.0 μs |    34.35 μs |  15.25 μs |  1.55 |    0.34 |  215.95 KB |        0.90 | ⚠ Low |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,039.8 μs** |   **482.89 μs** | **252.56 μs** |  **1.05** |    **0.33** |  **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,410.8 μs |   349.62 μs | 155.23 μs |  1.42 |    0.34 |  476.84 KB |        0.74 | ⚠ Low |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,078.8 μs** |    **36.78 μs** |  **13.12 μs** |  **1.00** |    **0.02** |  **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,398.3 μs | 1,957.13 μs | 868.98 μs |  2.22 |    0.76 | 2234.65 KB |        0.93 | ⚠ Low |

| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **100**         |   **824.9 ns** |  **43.42 ns** |  **25.84 ns** |  **1.00** |    **0.04** |      **-** |     **648 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 100         | 2,034.4 ns | 433.67 ns | 286.85 ns |  2.47 |    0.34 |      - |     452 B |        0.70 | ⚠ Low |
|                      |             |            |           |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **1000**        | **1,408.1 ns** | **127.53 ns** |  **84.35 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 1000        | 3,735.0 ns | 976.95 ns | 646.19 ns |  2.66 |    0.46 | 0.1000 |    2255 B |        0.92 | ⚠ Low |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 557.47 ns | 7.532 ns | 1.956 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.31 ns | 0.149 ns | 0.023 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.49 ns | 0.267 ns | 0.041 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.28 ns | 0.211 ns | 0.033 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.001 μs** | **0.0035 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **1**       | **1.999 μs** | **0.0033 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.399 μs** | **0.0285 μs** | **0.0044 μs** |         **-** |
| **WriteRequest** | **9**       | **2.412 μs** | **0.0173 μs** | **0.0045 μs** |         **-** |
| **WriteRequest** | **10**      | **2.387 μs** | **0.0387 μs** | **0.0060 μs** |         **-** |
| **WriteRequest** | **11**      | **2.382 μs** | **0.0027 μs** | **0.0004 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.65 ns** | **1.159 ns** | **0.179 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  98.84 ns | 0.299 ns | 0.078 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **91.59 ns** | **0.462 ns** | **0.072 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.94 ns | 0.387 ns | 0.060 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,638.3 ns |  9.09 ns |  4.75 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,927.7 ns |  4.06 ns |  2.12 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,377.8 ns | 12.26 ns |  6.41 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,275.2 ns |  4.00 ns |  2.38 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,108.2 ns | 12.12 ns |  7.21 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,024.9 ns |  6.68 ns |  3.98 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,934.5 ns |  3.72 ns |  2.21 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,849.6 ns | 12.95 ns |  8.57 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.3 ns |  1.99 ns |  1.04 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,818.1 ns |  5.60 ns |  3.33 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   724.8 ns |  4.73 ns |  2.81 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   816.2 ns |  5.53 ns |  3.66 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   167.6 ns |  0.51 ns |  0.33 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,746.4 ns | 21.76 ns | 14.39 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,304.3 ns |  7.04 ns |  4.19 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,917.07 ns | 48.469 ns | 32.059 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |  0.013 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.73 ns |  0.025 ns |  0.015 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.41 ns |  0.043 ns |  0.025 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.85 ns |  0.722 ns |  0.477 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.008 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    120.59 ns |  2.148 ns |  1.421 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.36 ns |  0.129 ns |  0.085 ns |  0.47 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     288.7 ns |   3.27 ns |   2.16 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,347.2 ns | 262.70 ns | 137.40 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     222.3 ns |   1.66 ns |   0.99 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,134.0 ns | 911.42 ns | 542.37 ns |      - |      80 B |

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