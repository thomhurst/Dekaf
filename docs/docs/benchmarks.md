---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-29 16:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.89 | 0.85–1.27 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.02 | 0.91–1.46 | 54% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.72 | 0.69–0.87 | 26% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.13 | 0.91–1.79 | 78% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.08–0.09 | 5% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.34 | 0.32–0.39 | 19% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.01 | 0.97–1.19 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.06 | 1.00–1.14 | 13% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.00 | 0.92–1.03 | 10% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.95–1.00 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.45 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.46 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.00 | 0.98–1.08 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 3% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,130.2 μs** |   **101.89 μs** |    **67.39 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,701.5 μs |    28.35 μs |    16.87 μs |  0.44 |    0.01 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,348.8 μs** |    **33.16 μs** |    **19.73 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,811.2 μs |    38.26 μs |    25.30 μs |  0.52 |    0.00 |        - |       - |   51738 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,308.4 μs** |    **76.82 μs** |    **45.71 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,723.4 μs |    95.68 μs |    63.29 μs |  0.43 |    0.01 |        - |       - |    6302 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,507.4 μs** |   **265.03 μs** |   **175.30 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,454.2 μs | 1,544.02 μs | 1,021.28 μs |  1.00 |    0.08 |        - |       - |   69860 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.0 μs** |    **13.46 μs** |     **8.90 μs** |  **1.00** |    **0.09** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    136.4 μs |    13.92 μs |     8.28 μs |  0.99 |    0.09 |        - |       - |     209 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,251.2 μs** |    **55.50 μs** |    **33.02 μs** |  **1.00** |    **0.04** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,330.7 μs |   204.72 μs |   121.83 μs |  1.06 |    0.10 |        - |       - |    2166 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,055.5 μs** |    **11.44 μs** |     **5.98 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121541 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,059.8 μs |    83.03 μs |    54.92 μs |  1.00 |    0.05 |        - |       - |    1965 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,564.2 μs** |   **131.84 μs** |    **78.45 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215418 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,110.7 μs |   215.73 μs |   142.69 μs |  0.96 |    0.01 |        - |       - |   18890 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,493.0 μs** |    **15.40 μs** |    **10.19 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,593.7 μs |    12.50 μs |     8.27 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,501.0 μs** |    **13.80 μs** |     **7.22 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,598.9 μs |    11.02 μs |     6.56 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,502.7 μs** |    **14.82 μs** |     **8.82 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,593.2 μs |     8.43 μs |     5.02 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,503.1 μs** |    **18.23 μs** |    **10.85 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,618.9 μs |    19.08 μs |    12.62 μs |  0.48 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **139.6 μs** |    **63.01 μs** |  **32.96 μs** |   **138.9 μs** |  **1.05** |    **0.34** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   124.8 μs |    71.48 μs |  31.74 μs |   116.9 μs |  0.94 |    0.31 |  26.45 KB |        0.41 | ⚠ Low |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.3 μs** |    **45.98 μs** |  **24.05 μs** |   **140.9 μs** |  **1.02** |    **0.20** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   138.0 μs |    14.44 μs |   5.15 μs |   138.9 μs |  0.92 |    0.13 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,073.3 μs** |   **465.46 μs** | **243.44 μs** | **1,025.3 μs** |  **1.04** |    **0.30** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   737.4 μs |   105.07 μs |  46.65 μs |   756.1 μs |  0.71 |    0.14 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,434.5 μs** |   **944.91 μs** | **494.21 μs** | **1,108.3 μs** |  **1.09** |    **0.47** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,524.7 μs | 1,748.71 μs | 776.44 μs | 1,001.1 μs |  1.16 |    0.65 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,534.5 ns** |    **19.27 ns** |  **11.47 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   493.6 ns |    49.25 ns |  32.58 ns |  0.09 |    0.01 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,525.1 ns** | **1,058.53 ns** | **700.15 ns** |  **1.08** |    **0.46** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,193.5 ns |   105.37 ns |  62.70 ns |  0.36 |    0.14 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 449.45 ns | 1.738 ns | 0.269 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.71 ns | 0.146 ns | 0.038 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.63 ns | 0.097 ns | 0.025 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.49 ns | 0.150 ns | 0.039 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.073 μs** | **0.0049 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **1**       | **2.076 μs** | **0.0084 μs** | **0.0013 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.409 μs** | **0.0118 μs** | **0.0031 μs** |         **-** |
| **WriteRequest** | **9**       | **2.428 μs** | **0.0121 μs** | **0.0031 μs** |         **-** |
| **WriteRequest** | **10**      | **2.412 μs** | **0.0132 μs** | **0.0034 μs** |         **-** |
| **WriteRequest** | **11**      | **2.409 μs** | **0.0064 μs** | **0.0017 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **100.41 ns** | **0.367 ns** | **0.095 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.03 ns | 0.169 ns | 0.026 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **98.39 ns** | **0.382 ns** | **0.059 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  91.08 ns | 0.260 ns | 0.068 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.6 ns | 3.21 ns | 1.91 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,110.3 ns | 2.67 ns | 1.77 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,331.1 ns | 3.61 ns | 2.15 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,488.2 ns | 3.47 ns | 2.06 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,106.9 ns | 4.55 ns | 2.70 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,988.4 ns | 5.23 ns | 2.74 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,916.1 ns | 3.86 ns | 2.02 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,842.5 ns | 4.38 ns | 2.90 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.8 ns | 0.26 ns | 0.16 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.1 ns | 5.25 ns | 3.12 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   721.1 ns | 1.41 ns | 0.84 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   832.2 ns | 0.84 ns | 0.50 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   167.4 ns | 0.10 ns | 0.05 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,745.1 ns | 6.12 ns | 3.64 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,307.9 ns | 2.46 ns | 1.29 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,657.98 ns | 34.970 ns | 23.130 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.25 ns |  0.025 ns |  0.015 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.10 ns |  0.048 ns |  0.025 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.69 ns |  0.073 ns |  0.048 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     27.81 ns |  0.028 ns |  0.019 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.96 ns |  0.025 ns |  0.015 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.38 ns |  0.999 ns |  0.523 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.55 ns |  0.097 ns |  0.051 ns |  0.51 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     301.2 ns |   2.52 ns |  1.50 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,560.3 ns | 147.00 ns | 97.23 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     206.5 ns |   0.67 ns |  0.40 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,177.4 ns |  59.29 ns | 39.22 ns |      - |      80 B |

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