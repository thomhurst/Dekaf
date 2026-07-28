---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 12:03 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.35 | 1.22–1.54 | 24% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.80 | 1.44–1.93 | 27% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.29 | 1.24–1.60 | 28% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 2.09 | 1.43–2.24 | 39% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 100 | 5 | 2.53 | 2.25–2.91 | 26% | Stable |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 1000 | 5 | 2.46 | 2.30–2.75 | 18% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.09 | 0.95–1.21 | 24% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.08 | 1.04–1.15 | 10% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.98–1.04 | 7% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.99 | 0.83–1.03 | 20% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.43 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.52 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.42 | 0.39–0.44 | 11% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.04 | 0.96–1.06 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,991.5 μs** |    **83.23 μs** |    **55.05 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,571.1 μs |     9.13 μs |     4.78 μs |  0.43 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,140.4 μs** |    **75.76 μs** |    **50.11 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,768.8 μs |    43.55 μs |    28.80 μs |  0.53 |    0.01 |        - |       - |   51805 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,715.9 μs** |    **47.00 μs** |    **27.97 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,644.5 μs |    52.84 μs |    34.95 μs |  0.39 |    0.01 |        - |       - |    6355 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,148.2 μs** |   **241.50 μs** |   **159.74 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,853.3 μs | 1,577.25 μs | 1,043.25 μs |  1.06 |    0.09 |        - |       - |  351398 B |        0.18 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **112.0 μs** |     **2.47 μs** |     **1.63 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    135.4 μs |    30.47 μs |    20.15 μs |  1.21 |    0.17 |        - |       - |     205 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,151.6 μs** |    **36.95 μs** |    **24.44 μs** |  **1.00** |    **0.03** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,301.2 μs |   185.76 μs |   110.54 μs |  1.13 |    0.09 |        - |       - |    2199 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **933.9 μs** |    **23.97 μs** |    **15.86 μs** |  **1.00** |    **0.02** |   **7.0801** |       **-** |  **121385 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    973.9 μs |    95.58 μs |    63.22 μs |  1.04 |    0.07 |        - |       - |    1966 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,128.3 μs** |    **69.84 μs** |    **46.20 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1213618 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,371.3 μs |   242.16 μs |   126.65 μs |  1.03 |    0.01 |        - |       - |   18315 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,495.6 μs** |    **28.51 μs** |    **18.86 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,508.6 μs |    18.08 μs |    11.96 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,477.4 μs** |    **31.48 μs** |    **18.73 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,519.5 μs |    23.95 μs |    15.84 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,475.5 μs** |    **20.59 μs** |    **13.62 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,499.5 μs |    10.58 μs |     5.53 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,489.7 μs** |    **11.32 μs** |     **6.74 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,493.7 μs |     6.73 μs |     4.00 μs |  0.45 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **109.1 μs** |    **48.98 μs** |  **25.62 μs** |    **99.34 μs** |  **1.05** |    **0.32** |   **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   133.6 μs |     9.92 μs |   5.19 μs |   135.47 μs |  1.28 |    0.27 |   40.16 KB |        0.62 | Stable |
|                      |              |             |            |             |           |             |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **113.4 μs** |    **14.17 μs** |   **5.05 μs** |   **111.80 μs** |  **1.00** |    **0.06** |  **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   219.3 μs |    64.67 μs |  28.71 μs |   233.43 μs |  1.94 |    0.25 |  215.95 KB |        0.90 | Stable |
|                      |              |             |            |             |           |             |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **977.2 μs** |   **590.11 μs** | **308.64 μs** |   **834.79 μs** |  **1.07** |    **0.42** |  **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,213.2 μs |   591.67 μs | 262.70 μs | 1,127.49 μs |  1.33 |    0.43 |  476.84 KB |        0.74 | ⚠ Low |
|                      |              |             |            |             |           |             |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        |   **969.8 μs** |    **33.89 μs** |  **12.09 μs** |   **966.62 μs** |  **1.00** |    **0.02** |  **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,029.2 μs | 1,837.66 μs | 815.93 μs | 1,490.13 μs |  2.09 |    0.79 | 2234.65 KB |        0.93 | ⚠ Low |

| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **100**         |   **646.6 ns** |  **37.12 ns** |  **19.41 ns** |  **1.00** |    **0.04** |      **-** |     **648 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 100         | 1,881.4 ns | 457.89 ns | 302.86 ns |  2.91 |    0.45 |      - |     452 B |        0.70 | ⚠ Low |
|                      |             |            |           |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **1000**        | **1,272.1 ns** |  **82.52 ns** |  **43.16 ns** |  **1.00** |    **0.05** | **0.1000** |    **2448 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 1000        | 3,479.9 ns | 735.52 ns | 486.50 ns |  2.74 |    0.38 | 0.1000 |    2255 B |        0.92 | ⚠ Low |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 448.95 ns | 9.910 ns | 2.573 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  32.87 ns | 0.255 ns | 0.040 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.31 ns | 0.305 ns | 0.047 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.48 ns | 0.129 ns | 0.034 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.072 μs** | **0.0020 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **1**       | **2.072 μs** | **0.0051 μs** | **0.0013 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.387 μs** | **0.0181 μs** | **0.0028 μs** |         **-** |
| **WriteRequest** | **9**       | **2.383 μs** | **0.0259 μs** | **0.0067 μs** |         **-** |
| **WriteRequest** | **10**      | **2.409 μs** | **0.0267 μs** | **0.0041 μs** |         **-** |
| **WriteRequest** | **11**      | **2.404 μs** | **0.0099 μs** | **0.0026 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.95 ns** | **0.559 ns** | **0.086 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 100.11 ns | 0.499 ns | 0.077 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.56 ns** | **5.863 ns** | **1.523 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  89.93 ns | 0.717 ns | 0.186 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,636.8 ns |  1.95 ns |  1.16 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,141.9 ns |  1.91 ns |  1.00 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,341.3 ns |  1.45 ns |  0.87 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,265.9 ns |  9.81 ns |  6.49 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,916.3 ns |  1.19 ns |  0.79 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,976.1 ns | 89.80 ns | 59.40 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,979.2 ns |  5.99 ns |  3.13 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,838.6 ns | 10.63 ns |  5.56 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,143.6 ns |  0.70 ns |  0.41 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.9 ns |  9.43 ns |  5.61 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   793.1 ns |  5.42 ns |  3.22 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   894.5 ns |  4.04 ns |  2.11 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.6 ns |  0.13 ns |  0.07 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,711.0 ns |  3.19 ns |  1.90 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,295.2 ns |  1.03 ns |  0.61 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,861.49 ns | 13.256 ns | 7.888 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |  0.010 ns | 0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.73 ns |  0.030 ns | 0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.38 ns |  0.072 ns | 0.043 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     37.77 ns |  1.433 ns | 0.948 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.79 ns |  0.020 ns | 0.013 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    130.11 ns |  4.662 ns | 3.084 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.50 ns |  0.294 ns | 0.194 ns |  0.42 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     299.0 ns |   1.36 ns |   0.81 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,604.5 ns | 235.52 ns | 123.18 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     207.8 ns |   0.47 ns |   0.31 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,016.8 ns | 131.13 ns |  86.73 ns |      - |      80 B |

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