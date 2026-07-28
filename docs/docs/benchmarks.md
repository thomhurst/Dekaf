---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 09:09 UTC

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
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.63 | 1.44–1.84 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.40 | 1.28–1.69 | 29% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 2.22 | 1.43–2.64 | 54% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 100 | 5 | 2.53 | 2.25–2.88 | 25% | Stable |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 1000 | 5 | 2.46 | 2.30–2.75 | 18% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.05 | 0.95–1.18 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.06 | 1.02–1.15 | 12% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.98–1.02 | 5% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.83–1.01 | 19% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.43 | 0.42–0.44 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.52 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.42 | 0.41–0.44 | 7% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.04 | 0.96–1.06 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,019.5 μs** |    **60.14 μs** |  **39.78 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,584.6 μs |    27.21 μs |  16.19 μs |  0.43 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,131.8 μs** |   **106.32 μs** |  **63.27 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,792.7 μs |    49.25 μs |  29.31 μs |  0.53 |    0.01 |        - |       - |   51762 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,681.5 μs** |    **53.81 μs** |  **32.02 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,928.8 μs |    93.94 μs |  55.90 μs |  0.44 |    0.01 |        - |       - |    6270 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,416.2 μs** |   **227.14 μs** | **150.24 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,784.3 μs | 1,204.06 μs | 796.41 μs |  1.03 |    0.07 |        - |       - |  345991 B |        0.18 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.2 μs** |     **4.03 μs** |   **2.66 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    142.5 μs |    11.40 μs |   7.54 μs |  1.18 |    0.06 |        - |       - |     211 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,127.7 μs** |    **56.55 μs** |  **33.65 μs** |  **1.00** |    **0.04** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,299.5 μs |   183.23 μs | 121.20 μs |  1.15 |    0.11 |        - |       - |    2176 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **942.7 μs** |    **14.21 μs** |   **7.43 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121534 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    964.4 μs |    87.60 μs |  57.94 μs |  1.02 |    0.06 |        - |       - |    1989 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,296.5 μs** |    **89.75 μs** |  **46.94 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1213938 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,429.9 μs |   362.27 μs | 215.58 μs |  1.01 |    0.02 |        - |       - |   18260 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,462.3 μs** |    **27.04 μs** |  **17.89 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,503.4 μs |    20.54 μs |  12.22 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,461.7 μs** |    **36.23 μs** |  **21.56 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,487.6 μs |    30.95 μs |  20.47 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,475.7 μs** |    **14.48 μs** |   **9.58 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,491.0 μs |     6.83 μs |   4.07 μs |  0.45 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,478.5 μs** |    **23.91 μs** |  **15.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,522.1 μs |    27.52 μs |  16.37 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |------------:|-------------:|-------------:|------------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |    **97.80 μs** |    **47.843 μs** |    **21.243 μs** |    **85.70 μs** |  **1.03** |    **0.27** |   **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   132.45 μs |    15.093 μs |     5.382 μs |   133.12 μs |  1.40 |    0.23 |   40.16 KB |        0.62 | Stable |
|                      |              |             |             |              |              |             |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **116.08 μs** |    **17.219 μs** |     **7.646 μs** |   **113.00 μs** |  **1.00** |    **0.08** |  **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   213.33 μs |    58.080 μs |    25.788 μs |   212.97 μs |  1.84 |    0.23 |  215.95 KB |        0.90 | Stable |
|                      |              |             |             |              |              |             |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,007.53 μs** |   **586.761 μs** |   **306.887 μs** |   **936.00 μs** |  **1.08** |    **0.43** |  **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,292.40 μs |   390.936 μs |   173.578 μs | 1,373.40 μs |  1.38 |    0.41 |  476.84 KB |        0.74 | ⚠ Low |
|                      |              |             |             |              |              |             |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        |   **969.62 μs** |     **7.018 μs** |     **2.503 μs** |   **969.30 μs** |  **1.00** |    **0.00** |  **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,175.55 μs | 3,653.961 μs | 1,622.381 μs | 1,324.76 μs |  2.24 |    1.57 | 2234.65 KB |        0.93 | ⚠ Low |

| Method               | MessageSize | Mean       | Error    | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------ |-----------:|---------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **100**         |   **736.1 ns** | **172.3 ns** | **113.97 ns** |  **1.02** |    **0.21** |      **-** |     **648 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 100         | 1,858.8 ns | 354.0 ns | 234.16 ns |  2.58 |    0.47 |      - |     452 B |        0.70 | ⚠ Low |
|                      |             |            |          |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **1000**        | **1,285.3 ns** | **126.0 ns** |  **65.92 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 1000        | 3,155.9 ns | 441.5 ns | 262.73 ns |  2.46 |    0.23 | 0.1000 |    2255 B |        0.92 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 585.66 ns | 3.021 ns | 0.785 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     | 124.61 ns | 2.160 ns | 0.334 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.50 ns | 0.272 ns | 0.042 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.43 ns | 0.295 ns | 0.077 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.001 μs** | **0.0032 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **1**       | **2.001 μs** | **0.0054 μs** | **0.0008 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.403 μs** | **0.0070 μs** | **0.0011 μs** |         **-** |
| **WriteRequest** | **9**       | **2.417 μs** | **0.0044 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **10**      | **2.643 μs** | **0.0017 μs** | **0.0003 μs** |         **-** |
| **WriteRequest** | **11**      | **2.395 μs** | **0.0146 μs** | **0.0038 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **101.23 ns** | **0.446 ns** | **0.116 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  95.23 ns | 0.467 ns | 0.072 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **92.53 ns** | **0.156 ns** | **0.024 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  90.22 ns | 1.034 ns | 0.160 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.4 ns |  1.29 ns | 0.77 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,115.2 ns |  4.58 ns | 2.72 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,372.5 ns |  7.20 ns | 4.76 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,286.7 ns |  2.24 ns | 1.33 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,901.5 ns |  8.12 ns | 5.37 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,980.2 ns |  5.28 ns | 3.14 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,929.2 ns |  2.72 ns | 1.42 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,834.9 ns |  7.89 ns | 4.70 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.3 ns |  0.69 ns | 0.36 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,814.8 ns |  3.21 ns | 1.91 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   752.9 ns |  1.24 ns | 0.82 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   807.2 ns |  1.64 ns | 1.09 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   170.4 ns |  0.13 ns | 0.07 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,757.9 ns | 13.61 ns | 9.00 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,303.2 ns |  0.88 ns | 0.46 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,769.74 ns | 29.284 ns | 19.370 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.15 ns |  0.018 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.18 ns |  0.021 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.64 ns |  0.024 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.96 ns |  0.429 ns |  0.256 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.95 ns |  0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.22 ns |  8.132 ns |  5.379 ns |  1.00 |    0.06 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.55 ns |  0.063 ns |  0.038 ns |  0.49 |    0.02 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     287.6 ns |   0.95 ns |   0.57 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,402.2 ns | 141.65 ns |  84.29 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     230.4 ns |   0.37 ns |   0.22 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,282.1 ns | 211.84 ns | 110.80 ns |      - |      80 B |

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