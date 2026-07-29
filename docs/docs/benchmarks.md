---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-29 15:13 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.90 | 0.85–1.27 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.02 | 0.90–1.74 | 83% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.77 | 0.66–1.26 | 77% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.29 | 0.91–1.79 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.08–0.10 | 14% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.33 | 0.33–0.39 | 19% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.04 | 0.97–1.14 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.05 | 0.90–1.14 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.92–1.03 | 10% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.91–1.00 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.44 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.46 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.02 | 0.98–1.08 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 3% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,069.5 μs** |    **90.47 μs** |  **59.84 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,688.4 μs |    24.45 μs |  16.17 μs |  0.44 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,422.0 μs** |    **72.04 μs** |  **47.65 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,785.9 μs |    36.92 μs |  21.97 μs |  0.51 |    0.00 |        - |       - |   51783 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,390.2 μs** |   **113.55 μs** |  **67.57 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194964 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,916.7 μs |    48.03 μs |  31.77 μs |  0.46 |    0.01 |        - |       - |    6285 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,474.3 μs** |   **207.69 μs** | **123.60 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,210.2 μs | 1,265.33 μs | 752.98 μs |  0.98 |    0.06 |        - |       - |   71508 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **122.2 μs** |    **19.11 μs** |  **12.64 μs** |  **1.01** |    **0.15** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    130.2 μs |    22.38 μs |  13.32 μs |  1.08 |    0.16 |        - |       - |     209 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,286.8 μs** |    **49.04 μs** |  **32.44 μs** |  **1.00** |    **0.03** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,344.4 μs |   260.28 μs | 172.16 μs |  1.05 |    0.13 |        - |       - |    4633 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,076.5 μs** |    **19.42 μs** |  **11.55 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121578 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    993.7 μs |   136.38 μs |  90.21 μs |  0.92 |    0.08 |        - |       - |    1952 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,532.0 μs** |   **123.05 μs** |  **81.39 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215128 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,030.6 μs |   317.69 μs | 210.13 μs |  0.95 |    0.02 |        - |       - |   18717 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,489.0 μs** |     **8.28 μs** |   **5.48 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,587.8 μs |     9.68 μs |   6.40 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,492.3 μs** |     **9.01 μs** |   **5.96 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,580.1 μs |     7.80 μs |   4.64 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,498.8 μs** |    **10.55 μs** |   **6.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,586.9 μs |     6.34 μs |   4.19 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,495.9 μs** |     **8.10 μs** |   **5.36 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,602.8 μs |    14.41 μs |   8.58 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **142.7 μs** |    **64.32 μs** |  **33.64 μs** |   **150.7 μs** |  **1.05** |    **0.35** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   120.7 μs |    31.82 μs |  16.64 μs |   125.7 μs |  0.89 |    0.24 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **159.8 μs** |    **78.88 μs** |  **41.25 μs** |   **141.2 μs** |  **1.06** |    **0.35** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   152.6 μs |    53.70 μs |  23.84 μs |   155.0 μs |  1.01 |    0.27 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **880.8 μs** |    **36.33 μs** |  **12.95 μs** |   **878.1 μs** |  **1.00** |    **0.02** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   768.2 μs |   170.61 μs |  75.75 μs |   743.0 μs |  0.87 |    0.08 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,439.9 μs** |   **905.88 μs** | **473.79 μs** | **1,225.5 μs** |  **1.09** |    **0.45** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,305.7 μs | 1,079.21 μs | 479.18 μs |   974.2 μs |  0.99 |    0.44 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,538.7 ns** |    **16.98 ns** |  **10.10 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   491.7 ns |    36.07 ns |  23.86 ns |  0.09 |    0.00 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,495.9 ns** | **1,014.23 ns** | **670.85 ns** |  **1.07** |    **0.43** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,150.1 ns |    43.92 ns |  22.97 ns |  0.35 |    0.13 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev    | Gen0   | Allocated |
|--------------------------- |----------:|----------:|----------:|-------:|----------:|
| ReadDescribeGroupsV5       | 586.00 ns | 54.086 ns | 14.046 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.11 ns |  0.234 ns |  0.036 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.15 ns |  0.218 ns |  0.057 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.22 ns |  0.182 ns |  0.047 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.006 μs** | **0.0053 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **1**       | **2.002 μs** | **0.0039 μs** | **0.0010 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.381 μs** | **0.0105 μs** | **0.0016 μs** |         **-** |
| **WriteRequest** | **9**       | **2.370 μs** | **0.0046 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **10**      | **2.422 μs** | **0.0079 μs** | **0.0020 μs** |         **-** |
| **WriteRequest** | **11**      | **2.408 μs** | **0.0082 μs** | **0.0021 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.47 ns** | **1.154 ns** | **0.179 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.89 ns | 0.541 ns | 0.084 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **95.61 ns** | **1.755 ns** | **0.456 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  88.85 ns | 0.089 ns | 0.023 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.6 ns | 1.06 ns | 0.63 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,928.1 ns | 3.26 ns | 2.16 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,311.2 ns | 4.41 ns | 2.62 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,261.5 ns | 2.59 ns | 1.54 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,094.1 ns | 1.76 ns | 0.92 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,070.2 ns | 6.90 ns | 4.56 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,013.1 ns | 4.35 ns | 2.59 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,840.8 ns | 4.60 ns | 2.41 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.2 ns | 0.31 ns | 0.16 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.7 ns | 4.66 ns | 2.77 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   719.5 ns | 1.81 ns | 1.08 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   830.6 ns | 1.92 ns | 1.27 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   178.0 ns | 0.09 ns | 0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,674.4 ns | 7.12 ns | 4.71 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,390.1 ns | 2.11 ns | 1.26 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,064.08 ns | 19.022 ns | 11.320 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.86 ns |  0.013 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.71 ns |  0.013 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.45 ns |  0.115 ns |  0.076 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.20 ns |  0.978 ns |  0.647 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.79 ns |  0.032 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    126.06 ns |  5.907 ns |  3.515 ns |  1.00 |    0.04 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.28 ns |  0.162 ns |  0.107 ns |  0.43 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     291.0 ns |   1.23 ns |   0.73 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,395.4 ns |  90.78 ns |  54.02 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     221.1 ns |   0.23 ns |   0.14 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 125,383.4 ns | 300.05 ns | 198.46 ns |      - |      80 B |

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