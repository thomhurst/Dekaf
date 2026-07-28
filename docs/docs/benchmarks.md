---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 12:50 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.28 | 1.22–1.54 | 25% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.46 | 1.24–1.93 | 48% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.28 | 1.23–1.60 | 29% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 2.11 | 1.53–2.24 | 34% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 100 | 5 | 2.53 | 2.39–2.91 | 21% | Stable |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 1000 | 5 | 2.46 | 2.30–2.74 | 18% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.10 | 1.01–1.21 | 18% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.13 | 1.06–1.15 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.02 | 0.99–1.05 | 6% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 1.01 | 0.96–1.03 | 7% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.43 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.52 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.44 | 11% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.06 | 1.03–1.11 | 7% | Stable |
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
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,091.7 μs** |    **71.88 μs** |    **47.54 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,657.6 μs |    10.56 μs |     6.99 μs |  0.44 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,331.5 μs** |    **57.19 μs** |    **29.91 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,770.4 μs |    46.80 μs |    30.96 μs |  0.51 |    0.00 |        - |       - |   51826 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,670.4 μs** |    **59.56 μs** |    **35.44 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,640.8 μs |    44.81 μs |    23.44 μs |  0.40 |    0.00 |        - |       - |    7512 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,831.0 μs** |   **166.10 μs** |    **98.84 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,090.1 μs | 2,251.39 μs | 1,339.76 μs |  1.11 |    0.11 |  15.6250 |       - |  348213 B |        0.18 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.1 μs** |     **2.22 μs** |     **1.47 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    137.1 μs |    27.97 μs |    18.50 μs |  1.10 |    0.14 |        - |       - |     206 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,227.0 μs** |    **36.28 μs** |    **24.00 μs** |  **1.00** |    **0.03** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,389.9 μs |   194.55 μs |   128.68 μs |  1.13 |    0.10 |        - |       - |    2133 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,018.4 μs** |    **20.98 μs** |    **12.48 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121478 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,066.0 μs |    81.34 μs |    53.80 μs |  1.05 |    0.05 |        - |       - |    1970 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,994.2 μs** |   **352.57 μs** |   **209.81 μs** |  **1.00** |    **0.03** |  **70.3125** |       **-** | **1214510 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,087.5 μs |   434.28 μs |   258.43 μs |  1.01 |    0.03 |        - |       - |   18509 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,460.1 μs** |     **5.96 μs** |     **3.12 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,561.4 μs |     5.66 μs |     3.74 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,464.7 μs** |     **7.52 μs** |     **3.93 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,561.6 μs |     6.26 μs |     4.14 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,469.0 μs** |     **7.58 μs** |     **5.01 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,569.2 μs |     5.36 μs |     3.54 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,467.0 μs** |     **6.33 μs** |     **4.19 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,569.3 μs |    10.11 μs |     6.68 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **126.7 μs** |    **45.65 μs** |  **23.88 μs** |  **1.03** |    **0.27** |   **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   162.7 μs |    28.67 μs |  15.00 μs |  1.33 |    0.27 |   40.16 KB |        0.62 | Stable |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **166.0 μs** |    **62.31 μs** |  **32.59 μs** |  **1.04** |    **0.28** |  **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   205.6 μs |    56.76 μs |  29.69 μs |  1.29 |    0.31 |  215.95 KB |        0.90 | ⚠ Low |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,114.4 μs** |   **543.47 μs** | **284.25 μs** |  **1.06** |    **0.36** |  **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,376.0 μs |   296.41 μs | 131.61 μs |  1.30 |    0.32 |  476.84 KB |        0.74 | ⚠ Low |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,069.4 μs** |    **27.89 μs** |   **9.95 μs** |  **1.00** |    **0.01** |  **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,260.6 μs | 1,337.76 μs | 593.97 μs |  2.11 |    0.52 | 2234.65 KB |        0.93 | ⚠ Low |

| Method               | MessageSize | Mean       | Error    | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------ |-----------:|---------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **100**         |   **885.7 ns** | **170.6 ns** | **112.87 ns** |  **1.01** |    **0.17** |      **-** |     **648 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 100         | 2,215.3 ns | 393.5 ns | 260.26 ns |  2.54 |    0.40 |      - |     452 B |        0.70 | ⚠ Low |
|                      |             |            |          |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **1000**        | **1,375.7 ns** | **107.7 ns** |  **71.25 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 1000        | 3,502.1 ns | 709.5 ns | 469.28 ns |  2.55 |    0.35 | 0.1000 |    2255 B |        0.92 | ⚠ Low |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 542.89 ns | 19.104 ns | 2.956 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.41 ns |  0.105 ns | 0.016 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.66 ns |  0.315 ns | 0.049 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.21 ns |  0.115 ns | 0.030 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.963 μs** | **0.0203 μs** | **0.0031 μs** |         **-** |
| **WriteRequest** | **1**       | **1.969 μs** | **0.0183 μs** | **0.0047 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.443 μs** | **0.0097 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **9**       | **2.459 μs** | **0.0103 μs** | **0.0027 μs** |         **-** |
| **WriteRequest** | **10**      | **2.467 μs** | **0.0092 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **11**      | **2.460 μs** | **0.0071 μs** | **0.0011 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.04 ns** | **0.863 ns** | **0.224 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  95.80 ns | 0.425 ns | 0.110 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.47 ns** | **0.440 ns** | **0.068 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  90.94 ns | 1.070 ns | 0.166 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,742.7 ns | 16.76 ns | 11.09 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,268.0 ns |  1.77 ns |  1.05 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,400.0 ns |  3.25 ns |  1.94 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,386.7 ns |  5.07 ns |  3.02 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,176.5 ns |  7.80 ns |  4.64 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,001.6 ns |  7.32 ns |  4.36 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,948.5 ns |  6.33 ns |  4.18 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,901.5 ns |  5.61 ns |  3.34 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.6 ns |  1.63 ns |  0.97 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,043.5 ns |  6.67 ns |  3.49 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   730.0 ns |  3.00 ns |  1.79 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   865.1 ns |  3.01 ns |  1.99 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   160.7 ns |  0.25 ns |  0.17 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,728.4 ns | 12.16 ns |  8.04 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,209.9 ns |  2.10 ns |  1.25 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,865.17 ns | 4.186 ns | 2.491 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns | 0.010 ns | 0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns | 0.029 ns | 0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.03 ns | 0.069 ns | 0.041 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.63 ns | 0.480 ns | 0.251 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns | 0.009 ns | 0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |          |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    124.57 ns | 4.235 ns | 2.801 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     56.24 ns | 0.121 ns | 0.080 ns |  0.45 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     285.6 ns |   1.44 ns |   0.86 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,560.1 ns | 239.78 ns | 158.60 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     222.0 ns |   1.66 ns |   0.99 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,108.0 ns | 154.87 ns | 102.44 ns |      - |      80 B |

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