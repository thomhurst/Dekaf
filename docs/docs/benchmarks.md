---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-29 15:45 UTC

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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.02 | 0.90–1.46 | 55% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.72 | 0.66–0.87 | 29% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.29 | 0.91–1.79 | 68% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.08–0.09 | 5% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.33 | 0.32–0.39 | 20% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.07 | 0.97–1.19 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.04 | 0.90–1.14 | 23% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.92–1.03 | 10% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.91–1.00 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.45 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.46 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.02 | 0.98–1.08 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 3% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,070.3 μs** |    **85.00 μs** |  **56.22 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,704.7 μs |    22.19 μs |  14.68 μs |  0.45 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,381.8 μs** |    **84.69 μs** |  **50.40 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,747.6 μs |    47.66 μs |  28.36 μs |  0.51 |    0.00 |        - |       - |   51861 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,622.9 μs** |    **58.63 μs** |  **38.78 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,646.5 μs |    20.96 μs |  13.86 μs |  0.40 |    0.00 |        - |       - |    6291 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,559.8 μs** |   **431.13 μs** | **256.56 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944528 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,784.8 μs | 1,336.18 μs | 795.14 μs |  1.02 |    0.06 |        - |       - |   69899 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.9 μs** |     **6.20 μs** |   **3.69 μs** |  **1.00** |    **0.04** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    145.5 μs |     8.56 μs |   5.10 μs |  1.19 |    0.05 |        - |       - |     260 B |       0.009 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,272.9 μs** |    **83.06 μs** |  **54.94 μs** |  **1.00** |    **0.06** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,275.5 μs |   170.15 μs | 112.54 μs |  1.00 |    0.09 |        - |       - |    4612 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,036.9 μs** |    **14.31 μs** |   **8.52 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121510 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,043.5 μs |    65.45 μs |  38.95 μs |  1.01 |    0.04 |        - |       - |    1923 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,290.2 μs** |   **132.30 μs** |  **78.73 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214937 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,103.9 μs |   237.23 μs | 156.91 μs |  0.98 |    0.02 |        - |       - |   18625 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,502.2 μs** |     **7.24 μs** |   **4.31 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,602.8 μs |     8.95 μs |   5.92 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,503.0 μs** |    **13.92 μs** |   **8.28 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,606.7 μs |    16.48 μs |  10.90 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,501.2 μs** |     **8.68 μs** |   **5.74 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,624.0 μs |     9.03 μs |   5.97 μs |  0.48 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,501.1 μs** |     **8.80 μs** |   **5.24 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,623.1 μs |    10.89 μs |   7.20 μs |  0.48 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **130.5 μs** |    **54.50 μs** |  **28.50 μs** |  **1.04** |    **0.30** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   124.6 μs |    38.09 μs |  19.92 μs |  0.99 |    0.25 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **145.4 μs** |    **61.33 μs** |  **27.23 μs** |  **1.03** |    **0.24** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   157.3 μs |    38.83 μs |  20.31 μs |  1.11 |    0.21 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,015.5 μs** |   **456.27 μs** | **238.64 μs** |  **1.04** |    **0.31** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   721.4 μs |    70.62 μs |  31.36 μs |  0.74 |    0.15 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,330.5 μs** |   **809.65 μs** | **423.46 μs** |  **1.07** |    **0.41** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,781.5 μs | 1,616.68 μs | 717.81 μs |  1.44 |    0.65 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,528.8 ns** |   **9.33 ns** |  **5.55 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   481.6 ns |  36.20 ns | 23.94 ns |  0.09 |    0.00 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |           |          |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,672.5 ns** |  **71.98 ns** | **47.61 ns** |  **1.00** |    **0.02** | **0.1450** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        | 1,178.1 ns | 122.16 ns | 72.69 ns |  0.32 |    0.02 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev    | Gen0   | Allocated |
|--------------------------- |----------:|----------:|----------:|-------:|----------:|
| ReadDescribeGroupsV5       | 465.03 ns | 47.244 ns | 12.269 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  17.29 ns |  2.341 ns |  0.362 ns |      - |         - |
| WriteDescribeGroupsV6      |  30.69 ns |  2.832 ns |  0.736 ns |      - |         - |
| WriteListConfigResourcesV1 |  16.47 ns |  1.422 ns |  0.220 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.735 μs** | **0.3085 μs** | **0.0801 μs** |         **-** |
| **WriteRequest** | **1**       | **1.598 μs** | **0.0864 μs** | **0.0224 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.950 μs** | **0.0069 μs** | **0.0018 μs** |         **-** |
| **WriteRequest** | **9**       | **1.943 μs** | **0.0171 μs** | **0.0045 μs** |         **-** |
| **WriteRequest** | **10**      | **1.974 μs** | **0.0249 μs** | **0.0039 μs** |         **-** |
| **WriteRequest** | **11**      | **1.949 μs** | **0.0110 μs** | **0.0029 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **79.73 ns** | **0.206 ns** | **0.054 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 73.67 ns | 0.422 ns | 0.110 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **61.96 ns** | **0.305 ns** | **0.047 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 64.36 ns | 0.213 ns | 0.055 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,455.4 ns |  2.80 ns |  1.47 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,860.7 ns |  6.76 ns |  4.02 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,996.5 ns | 10.86 ns |  7.19 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,992.1 ns |  3.87 ns |  2.30 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,681.5 ns |  5.16 ns |  3.07 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,158.9 ns | 23.22 ns | 12.15 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,154.4 ns | 20.40 ns | 12.14 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,636.7 ns |  8.42 ns |  5.57 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,080.9 ns | 10.26 ns |  6.11 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,900.6 ns | 35.23 ns | 23.31 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   625.8 ns |  2.25 ns |  1.34 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   668.9 ns |  1.29 ns |  0.86 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   162.1 ns |  0.67 ns |  0.35 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,532.9 ns |  6.44 ns |  4.26 ns | 0.0019 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,054.4 ns |  4.32 ns |  2.86 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,622.24 ns | 39.247 ns | 20.527 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.15 ns |  0.010 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.95 ns |  0.158 ns |  0.094 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.66 ns |  0.123 ns |  0.073 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.46 ns |  0.268 ns |  0.140 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.94 ns |  0.012 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    112.94 ns |  1.324 ns |  0.876 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.89 ns |  0.153 ns |  0.091 ns |  0.49 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean        | Error       | StdDev      | Gen0   | Allocated |
|------------------------ |------------:|------------:|------------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    234.1 ns |     8.81 ns |     5.82 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 78,978.5 ns | 2,819.37 ns | 1,864.84 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    161.6 ns |     9.56 ns |     6.32 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 88,945.4 ns | 2,600.18 ns | 1,359.95 ns |      - |      80 B |

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