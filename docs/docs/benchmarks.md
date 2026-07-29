---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-29 18:20 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.96 | 0.85–1.27 | 45% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.03 | 0.91–1.46 | 53% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.71 | 0.69–0.87 | 26% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.34 | 0.91–1.79 | 66% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.08–0.09 | 5% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.34 | 0.32–0.39 | 19% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.07 | 0.99–1.19 | 19% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.06 | 1.00–1.13 | 12% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.00 | 0.92–1.03 | 10% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.98 | 0.95–1.00 | 5% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.45 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.46 | 15% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.02 | 0.98–1.08 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.48 | 0.46–0.48 | 3% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,060.9 μs** |  **97.75 μs** |  **64.65 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,665.5 μs |  17.51 μs |  10.42 μs |  0.44 |    0.00 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,437.2 μs** |  **75.81 μs** |  **50.14 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,778.2 μs |  56.82 μs |  37.58 μs |  0.51 |    0.01 |        - |       - |   51619 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,496.6 μs** | **104.13 μs** |  **61.97 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,639.7 μs |  45.05 μs |  26.81 μs |  0.41 |    0.01 |        - |       - |    6101 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,263.2 μs** | **290.99 μs** | **192.47 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,295.4 μs | 911.17 μs | 602.69 μs |  1.08 |    0.05 |        - |       - |   68769 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.5 μs** |   **3.00 μs** |   **1.98 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    145.4 μs |  19.59 μs |  12.96 μs |  1.13 |    0.10 |        - |       - |     188 B |       0.006 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,266.9 μs** |  **32.07 μs** |  **21.22 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,382.8 μs | 172.77 μs | 114.28 μs |  1.09 |    0.09 |        - |       - |    1974 B |       0.006 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,054.0 μs** |  **19.05 μs** |  **11.34 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121522 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,047.8 μs |  77.35 μs |  51.16 μs |  0.99 |    0.05 |        - |       - |    1786 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,477.4 μs** | **257.89 μs** | **170.58 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** | **1214945 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,322.2 μs | 234.89 μs | 139.78 μs |  0.99 |    0.02 |        - |       - |   18367 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,525.1 μs** |  **19.86 μs** |  **13.14 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,620.4 μs |  17.69 μs |  11.70 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,508.0 μs** |  **13.25 μs** |   **8.76 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,616.5 μs |   6.77 μs |   4.48 μs |  0.48 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,532.2 μs** |  **21.83 μs** |  **14.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,620.2 μs |   9.73 μs |   5.79 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,504.1 μs** |  **11.62 μs** |   **6.92 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,622.3 μs |  13.20 μs |   8.73 μs |  0.48 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **117.9 μs** |    **44.11 μs** |  **23.07 μs** |   **109.1 μs** |  **1.03** |    **0.26** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   120.2 μs |    39.64 μs |  20.73 μs |   111.4 μs |  1.05 |    0.24 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **140.7 μs** |    **49.66 μs** |  **22.05 μs** |   **135.7 μs** |  **1.02** |    **0.20** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   145.3 μs |    23.87 μs |  12.48 μs |   143.2 μs |  1.05 |    0.16 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,034.6 μs** |   **525.76 μs** | **274.98 μs** |   **869.1 μs** |  **1.05** |    **0.35** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   715.9 μs |    90.76 μs |  32.36 μs |   718.2 μs |  0.73 |    0.16 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,076.6 μs** |    **50.18 μs** |  **17.89 μs** | **1,077.9 μs** |  **1.00** |    **0.02** | **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,501.8 μs | 1,218.55 μs | 541.04 μs | 1,863.0 μs |  1.40 |    0.47 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,531.2 ns** |     **9.16 ns** |     **4.79 ns** | **5,531.2 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   471.8 ns |    21.73 ns |    11.37 ns |   473.8 ns |  0.09 |    0.00 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,041.7 ns** | **1,628.42 ns** | **1,077.10 ns** | **3,683.1 ns** |  **1.19** |    **0.72** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,174.1 ns |   124.26 ns |    64.99 ns | 1,162.9 ns |  0.46 |    0.22 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 535.13 ns | 9.815 ns | 1.519 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.04 ns | 0.682 ns | 0.106 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.74 ns | 0.192 ns | 0.030 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.30 ns | 0.734 ns | 0.114 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.058 μs** | **0.0057 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **1**       | **2.007 μs** | **0.0025 μs** | **0.0004 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.410 μs** | **0.0176 μs** | **0.0046 μs** |         **-** |
| **WriteRequest** | **9**       | **2.401 μs** | **0.0171 μs** | **0.0045 μs** |         **-** |
| **WriteRequest** | **10**      | **2.392 μs** | **0.0091 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **11**      | **2.446 μs** | **0.0020 μs** | **0.0003 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **104.55 ns** | **0.680 ns** | **0.177 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 104.64 ns | 2.926 ns | 0.453 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **96.81 ns** | **0.347 ns** | **0.090 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  86.03 ns | 0.321 ns | 0.083 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,635.5 ns | 1.95 ns | 1.02 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,117.2 ns | 3.96 ns | 2.36 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,328.8 ns | 2.72 ns | 1.62 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,272.3 ns | 2.35 ns | 1.23 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,097.1 ns | 1.21 ns | 0.80 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,968.2 ns | 2.55 ns | 1.52 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,945.8 ns | 4.04 ns | 2.41 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,849.0 ns | 2.40 ns | 1.59 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,145.8 ns | 1.90 ns | 1.13 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,815.3 ns | 3.21 ns | 1.91 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   732.5 ns | 1.73 ns | 1.14 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   863.1 ns | 0.98 ns | 0.58 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   169.2 ns | 0.11 ns | 0.06 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,686.4 ns | 4.49 ns | 2.35 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,311.2 ns | 0.71 ns | 0.42 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,299.52 ns | 39.473 ns | 20.645 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.55 ns |  0.018 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns |  0.014 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.39 ns |  0.072 ns |  0.043 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.06 ns |  0.231 ns |  0.121 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |  0.007 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.58 ns |  5.972 ns |  3.950 ns |  1.00 |    0.05 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.59 ns |  0.086 ns |  0.051 ns |  0.49 |    0.02 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     290.3 ns |   5.01 ns |   2.62 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  98,172.5 ns | 760.92 ns | 452.81 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     221.5 ns |   0.55 ns |   0.33 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,895.7 ns | 168.98 ns | 100.56 ns |      - |      80 B |

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