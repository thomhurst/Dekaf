---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-27 20:21 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.38 | 1.05–1.47 | 30% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.63 | 1.03–1.99 | 59% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.29 | 0.99–1.69 | 54% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.66 | 1.28–2.64 | 82% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 100 | 5 | 2.25 | 1.96–2.69 | 32% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 1000 | 5 | 2.59 | 2.00–2.75 | 29% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 0.95 | 0.86–1.05 | 20% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.04 | 0.84–1.22 | 37% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.03 | 0.98–1.20 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 1.00 | 0.83–1.26 | 43% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.43 | 0.42–0.43 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.50–0.52 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.43 | 0.41–0.45 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.11 | 0.96–1.70 | 66% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.44–0.48 | 9% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.44–0.47 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.45–0.47 | 3% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,157.0 μs** |   **103.42 μs** |    **68.41 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,666.7 μs |    15.36 μs |    10.16 μs |  0.43 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,346.8 μs** |    **58.01 μs** |    **38.37 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,792.0 μs |    74.75 μs |    49.44 μs |  0.52 |    0.01 |        - |       - |   51793 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,363.7 μs** |    **92.26 μs** |    **54.90 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,680.4 μs |    64.95 μs |    38.65 μs |  0.42 |    0.01 |        - |       - |    6281 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,410.9 μs** |   **420.38 μs** |   **278.06 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,895.0 μs | 1,815.58 μs | 1,200.90 μs |  0.96 |    0.09 |        - |       - |  320403 B |        0.16 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.9 μs** |    **12.17 μs** |     **7.24 μs** |  **1.00** |    **0.07** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    130.7 μs |    21.45 μs |    14.19 μs |  0.95 |    0.11 |        - |       - |     168 B |       0.006 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,273.1 μs** |    **17.03 μs** |    **10.13 μs** |  **1.00** |    **0.01** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,324.6 μs |   250.16 μs |   165.47 μs |  1.04 |    0.12 |        - |       - |    2114 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,056.0 μs** |    **16.47 μs** |     **9.80 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121535 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,031.3 μs |    75.60 μs |    50.01 μs |  0.98 |    0.05 |        - |       - |    1940 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,455.0 μs** |   **317.16 μs** |   **188.74 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** | **1215052 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,654.5 μs | 1,159.63 μs |   767.03 μs |  0.83 |    0.07 |        - |       - |   18626 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,467.2 μs** |    **11.46 μs** |     **7.58 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,595.6 μs |     6.58 μs |     4.36 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,494.4 μs** |     **7.45 μs** |     **4.93 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,592.2 μs |     6.91 μs |     4.57 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,497.1 μs** |     **7.78 μs** |     **4.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,588.5 μs |    34.17 μs |    22.60 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,490.0 μs** |    **13.90 μs** |     **9.20 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,571.7 μs |     7.64 μs |     5.05 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **125.1 μs** |    **52.67 μs** |  **27.55 μs** |   **129.4 μs** |  **1.04** |    **0.31** |   **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   173.7 μs |    36.46 μs |  16.19 μs |   176.3 μs |  1.45 |    0.33 |   40.18 KB |        0.62 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **133.4 μs** |    **33.85 μs** |  **15.03 μs** |   **130.7 μs** |  **1.01** |    **0.15** |  **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   240.6 μs |    55.37 μs |  24.59 μs |   235.9 μs |  1.82 |    0.25 |  215.96 KB |        0.90 | Stable |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,110.9 μs** |   **566.72 μs** | **296.41 μs** | **1,019.5 μs** |  **1.06** |    **0.36** |  **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,550.7 μs |   599.36 μs | 266.12 μs | 1,538.7 μs |  1.48 |    0.41 |  476.85 KB |        0.74 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,372.2 μs** |   **835.64 μs** | **437.05 μs** | **1,086.6 μs** |  **1.08** |    **0.42** |  **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,958.1 μs | 1,435.94 μs | 637.57 μs | 1,589.7 μs |  1.54 |    0.61 | 2234.66 KB |        0.93 | ⚠ Low |

| Method               | MessageSize | Mean       | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------ |-----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **100**         |   **923.7 ns** | **218.9 ns** | **144.8 ns** |  **1.02** |    **0.21** |      **-** |     **648 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 100         | 2,078.8 ns | 390.4 ns | 258.2 ns |  2.30 |    0.42 |      - |     452 B |        0.70 | ⚠ Low |
|                      |             |            |          |          |       |         |        |           |             | — |
| **Confluent_PollSingle** | **1000**        | **1,508.3 ns** | **250.3 ns** | **165.5 ns** |  **1.01** |    **0.15** | **0.1000** |    **2448 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 1000        | 4,140.7 ns | 705.4 ns | 466.6 ns |  2.77 |    0.41 | 0.1000 |    2255 B |        0.92 | ⚠ Low |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 436.73 ns | 4.614 ns | 1.198 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  30.12 ns | 0.159 ns | 0.041 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.74 ns | 0.272 ns | 0.042 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.24 ns | 0.110 ns | 0.028 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.080 μs** | **0.0285 μs** | **0.0074 μs** |         **-** |
| **WriteRequest** | **1**       | **2.108 μs** | **0.0239 μs** | **0.0037 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.394 μs** | **0.0223 μs** | **0.0034 μs** |         **-** |
| **WriteRequest** | **9**       | **2.384 μs** | **0.0055 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **10**      | **2.396 μs** | **0.0042 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **11**      | **2.380 μs** | **0.0059 μs** | **0.0009 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.44 ns** | **0.694 ns** | **0.107 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.49 ns | 0.208 ns | 0.054 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **92.62 ns** | **0.199 ns** | **0.031 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  94.56 ns | 0.455 ns | 0.070 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,636.1 ns | 1.09 ns | 0.57 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,923.4 ns | 2.00 ns | 1.05 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,316.7 ns | 1.80 ns | 1.07 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,263.7 ns | 6.76 ns | 4.02 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,092.3 ns | 6.99 ns | 3.66 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,089.1 ns | 5.75 ns | 3.42 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,932.4 ns | 6.10 ns | 3.63 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,834.6 ns | 4.58 ns | 2.73 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.1 ns | 1.14 ns | 0.68 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.2 ns | 3.38 ns | 2.01 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   724.4 ns | 1.01 ns | 0.67 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   812.3 ns | 2.15 ns | 1.28 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.8 ns | 0.12 ns | 0.08 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,665.6 ns | 5.61 ns | 3.34 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,292.0 ns | 2.83 ns | 1.87 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,094.07 ns | 22.008 ns | 14.557 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.55 ns |  0.015 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns |  0.014 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.06 ns |  0.039 ns |  0.023 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.14 ns |  1.623 ns |  1.074 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.008 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.47 ns |  1.188 ns |  0.785 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.37 ns |  0.171 ns |  0.113 ns |  0.50 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     300.8 ns |   0.52 ns |  0.27 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,800.9 ns |  76.84 ns | 50.82 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     206.6 ns |   0.27 ns |  0.14 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,227.1 ns | 106.79 ns | 70.63 ns |      - |      80 B |

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