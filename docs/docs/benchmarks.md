---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-08-01 01:31 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.95 | 0.85–1.05 | 21% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.05 | 0.94–1.24 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.73 | 0.68–0.86 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.05 | 0.96–1.60 | 61% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.07–0.09 | 18% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.36 | 0.16–0.37 | 59% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.02 | 0.79–1.08 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.00 | 0.85–1.07 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.01 | 0.98–1.14 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.97 | 0.96–1.11 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.43–0.44 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.50–0.52 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.43 | 0.40–0.48 | 17% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.02 | 0.97–1.48 | 50% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 2% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 2% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.48 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.47–0.47 | 2% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,063.4 μs** |    **99.31 μs** |  **65.68 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,663.4 μs |    16.78 μs |   9.99 μs |  0.44 |    0.00 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,387.6 μs** |    **91.02 μs** |  **60.20 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,781.5 μs |    46.04 μs |  30.45 μs |  0.51 |    0.01 |        - |       - |   51687 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,487.1 μs** |    **83.10 μs** |  **54.97 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,865.2 μs |    52.06 μs |  30.98 μs |  0.44 |    0.01 |        - |       - |    6095 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,088.3 μs** | **1,511.29 μs** | **999.63 μs** |  **1.01** |    **0.10** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,739.5 μs |   664.97 μs | 439.84 μs |  0.98 |    0.07 |        - |       - |   71612 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.3 μs** |    **24.12 μs** |  **15.96 μs** |  **1.02** |    **0.18** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    126.0 μs |    15.97 μs |   9.51 μs |  0.97 |    0.15 |        - |       - |     144 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,269.5 μs** |    **34.95 μs** |  **20.80 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,333.5 μs |   159.09 μs | 105.23 μs |  1.05 |    0.08 |        - |       - |    2025 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,036.3 μs** |     **7.18 μs** |   **3.75 μs** |  **1.00** |    **0.00** |   **7.0801** |       **-** |  **121526 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,045.9 μs |    58.49 μs |  38.69 μs |  1.01 |    0.04 |        - |       - |    1809 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,395.2 μs** |   **153.45 μs** |  **91.32 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215140 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,962.7 μs |   289.68 μs | 172.39 μs |  0.96 |    0.02 |        - |       - |   17020 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,469.8 μs** |     **9.57 μs** |   **6.33 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,589.5 μs |    32.17 μs |  21.28 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,480.1 μs** |    **20.41 μs** |  **10.68 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,567.2 μs |     8.45 μs |   5.59 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,484.9 μs** |    **16.67 μs** |   **9.92 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,579.1 μs |    10.62 μs |   7.03 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,477.9 μs** |     **8.45 μs** |   **5.59 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,581.4 μs |     9.34 μs |   6.18 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **127.8 μs** |    **56.40 μs** |  **29.50 μs** |   **115.2 μs** |  **1.05** |    **0.32** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   108.9 μs |    24.52 μs |  12.82 μs |   107.6 μs |  0.89 |    0.21 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.6 μs** |    **80.07 μs** |  **41.88 μs** |   **136.2 μs** |  **1.06** |    **0.38** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   164.5 μs |    42.63 μs |  18.93 μs |   166.1 μs |  1.15 |    0.30 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,119.3 μs** |   **615.65 μs** | **322.00 μs** | **1,022.1 μs** |  **1.07** |    **0.39** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   815.1 μs |   339.93 μs | 150.93 μs |   798.6 μs |  0.78 |    0.23 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,490.7 μs** |   **907.19 μs** | **474.48 μs** | **1,404.0 μs** |  **1.09** |    **0.46** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,425.4 μs | 1,653.56 μs | 734.19 μs |   974.2 μs |  1.04 |    0.60 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,541.4 ns** |    **24.90 ns** |  **13.02 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   485.2 ns |    51.53 ns |  34.08 ns |  0.09 |    0.01 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,307.5 ns** | **1,530.55 ns** | **910.81 ns** |  **1.12** |    **0.56** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,218.0 ns |   175.89 ns | 104.67 ns |  0.41 |    0.17 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 366.41 ns | 4.430 ns | 1.150 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  23.03 ns | 0.069 ns | 0.018 ns |      - |         - |
| WriteDescribeGroupsV6      |  35.23 ns | 0.378 ns | 0.059 ns |      - |         - |
| WriteListConfigResourcesV1 |  15.07 ns | 0.058 ns | 0.015 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.607 μs** | **0.0019 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **1**       | **1.802 μs** | **0.0097 μs** | **0.0015 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.679 μs** | **0.0045 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **9**       | **2.404 μs** | **0.0100 μs** | **0.0026 μs** |         **-** |
| **WriteRequest** | **10**      | **2.418 μs** | **0.0231 μs** | **0.0036 μs** |         **-** |
| **WriteRequest** | **11**      | **2.458 μs** | **0.0180 μs** | **0.0047 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **99.97 ns** | **0.325 ns** | **0.084 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 96.98 ns | 0.310 ns | 0.081 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **90.44 ns** | **0.671 ns** | **0.174 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 88.18 ns | 0.542 ns | 0.141 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,637.6 ns |  3.36 ns | 1.76 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,217.0 ns |  2.49 ns | 1.48 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,324.8 ns |  3.50 ns | 2.08 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,271.2 ns |  2.76 ns | 1.45 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,094.9 ns |  2.25 ns | 1.34 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,965.3 ns |  5.53 ns | 2.89 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 4,089.3 ns | 10.75 ns | 5.62 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,850.0 ns |  4.33 ns | 2.58 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.3 ns |  1.90 ns | 0.99 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.7 ns |  4.90 ns | 2.91 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   769.7 ns |  2.64 ns | 1.57 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   833.0 ns |  3.18 ns | 1.66 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   176.3 ns |  0.23 ns | 0.14 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,694.3 ns |  8.36 ns | 4.37 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,316.6 ns |  3.57 ns | 1.87 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,082.60 ns | 13.282 ns | 8.785 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |  0.011 ns | 0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.73 ns |  0.056 ns | 0.037 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.37 ns |  0.088 ns | 0.046 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.62 ns |  0.281 ns | 0.186 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.019 ns | 0.012 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.23 ns |  0.799 ns | 0.475 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.15 ns |  0.130 ns | 0.077 ns |  0.52 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    228.5 ns |   0.43 ns |   0.28 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 84,282.8 ns | 322.26 ns | 213.15 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    160.7 ns |   0.34 ns |   0.23 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 79,850.8 ns |  78.38 ns |  51.85 ns |      - |      80 B |

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