---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 15:09 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.27 | 1.22–1.35 | 10% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.74 | 1.24–1.93 | 40% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.26 | 1.23–1.36 | 10% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 2.11 | 1.53–2.24 | 34% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 1 | 0.10 | 0.10–0.10 | 0% | ⚠ Insufficient history |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 1 | 0.33 | 0.33–0.33 | 0% | ⚠ Insufficient history |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.10 | 1.04–1.21 | 15% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.13 | 1.05–1.15 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.02 | 0.98–1.05 | 7% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 1.01 | 0.94–1.03 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.43–0.44 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.44 | 12% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.04 | 1.03–1.11 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.45–0.47 | 4% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,061.7 μs** |    **82.38 μs** |    **54.49 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,654.5 μs |     9.15 μs |     5.44 μs |  0.44 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,447.3 μs** |   **127.78 μs** |    **84.52 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,792.8 μs |    71.91 μs |    37.61 μs |  0.51 |    0.01 |        - |       - |   51786 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,400.9 μs** |   **104.94 μs** |    **54.89 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,635.3 μs |    36.88 μs |    21.95 μs |  0.41 |    0.00 |        - |       - |    6291 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,293.0 μs** |   **208.47 μs** |   **124.06 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944395 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,730.5 μs | 1,661.46 μs | 1,098.96 μs |  1.04 |    0.09 |        - |       - |   72196 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.3 μs** |    **11.17 μs** |     **7.39 μs** |  **1.00** |    **0.08** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    138.2 μs |    34.74 μs |    22.98 μs |  1.05 |    0.18 |        - |       - |     210 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,301.2 μs** |    **24.02 μs** |    **15.89 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,372.5 μs |   187.99 μs |   124.34 μs |  1.05 |    0.09 |        - |       - |    4658 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,026.2 μs** |    **12.81 μs** |     **7.62 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121509 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,014.7 μs |    73.26 μs |    48.45 μs |  0.99 |    0.05 |        - |       - |    1933 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,365.4 μs** |   **105.55 μs** |    **62.81 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215319 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,182.1 μs |   146.79 μs |    87.35 μs |  0.98 |    0.01 |        - |       - |   18536 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,469.7 μs** |     **6.76 μs** |     **4.47 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,564.3 μs |     6.56 μs |     4.34 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,470.5 μs** |    **13.75 μs** |     **7.19 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,569.2 μs |     7.79 μs |     5.15 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,477.5 μs** |    **14.33 μs** |     **9.48 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,574.5 μs |     7.43 μs |     4.42 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,471.2 μs** |    **16.65 μs** |    **11.01 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,582.3 μs |     4.41 μs |     2.62 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **129.6 μs** |    **41.80 μs** |  **21.86 μs** |   **130.5 μs** |  **1.03** |    **0.24** |   **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   165.0 μs |    25.22 μs |  13.19 μs |   160.4 μs |  1.31 |    0.24 |   40.16 KB |        0.62 | Stable |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **127.4 μs** |    **18.98 μs** |   **6.77 μs** |   **129.9 μs** |  **1.00** |    **0.07** |  **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   221.7 μs |    51.28 μs |  26.82 μs |   219.8 μs |  1.74 |    0.22 |  215.95 KB |        0.90 | Stable |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,067.4 μs** |   **513.19 μs** | **268.41 μs** |   **907.6 μs** |  **1.05** |    **0.33** |  **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,343.0 μs |   345.04 μs | 153.20 μs | 1,302.6 μs |  1.32 |    0.30 |  476.84 KB |        0.74 | Stable |
|                      |              |             |            |             |           |            |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,381.9 μs** |   **921.96 μs** | **482.20 μs** | **1,084.5 μs** |  **1.09** |    **0.47** |  **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,108.4 μs | 1,418.69 μs | 629.91 μs | 2,544.0 μs |  1.66 |    0.65 | 2234.65 KB |        0.93 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|----------:|---------:|------:|--------:|-------:|-------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,554.2 ns** |  **76.76 ns** | **45.68 ns** |  **1.00** |    **0.01** | **0.0375** |      **-** |      **-** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   535.7 ns |  44.47 ns | 29.41 ns |  0.10 |    0.01 | 0.0150 | 0.0025 |      - |     276 B |        0.42 | Stable |
|                      |                   |             |            |           |          |       |         |        |        |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,710.5 ns** |  **55.75 ns** | **33.17 ns** |  **1.00** |    **0.01** | **0.1450** |      **-** |      **-** |    **2454 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 1000        | 1,213.9 ns | 125.61 ns | 83.08 ns |  0.33 |    0.02 | 0.1225 | 0.0075 | 0.0025 |    2081 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 571.88 ns | 27.673 ns | 7.187 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.09 ns |  0.379 ns | 0.059 ns |      - |         - |
| WriteDescribeGroupsV6      |  46.16 ns |  0.099 ns | 0.026 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.18 ns |  0.054 ns | 0.014 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.001 μs** | **0.0088 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **1**       | **2.002 μs** | **0.0049 μs** | **0.0008 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.451 μs** | **0.0163 μs** | **0.0025 μs** |         **-** |
| **WriteRequest** | **9**       | **2.456 μs** | **0.0081 μs** | **0.0013 μs** |         **-** |
| **WriteRequest** | **10**      | **2.467 μs** | **0.0551 μs** | **0.0143 μs** |         **-** |
| **WriteRequest** | **11**      | **2.464 μs** | **0.0256 μs** | **0.0066 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.74 ns** | **0.362 ns** | **0.056 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.56 ns | 0.333 ns | 0.052 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **92.48 ns** | **0.703 ns** | **0.109 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  93.61 ns | 2.507 ns | 0.651 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,741.1 ns |  7.40 ns | 4.89 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,244.9 ns |  2.57 ns | 1.70 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,394.1 ns |  2.16 ns | 1.29 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,477.4 ns |  9.43 ns | 5.61 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,008.7 ns |  9.00 ns | 5.36 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,926.8 ns |  4.03 ns | 2.11 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,991.5 ns |  3.96 ns | 2.36 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,905.9 ns | 14.08 ns | 8.38 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,195.9 ns |  3.38 ns | 2.01 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,043.1 ns |  3.88 ns | 2.57 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   724.6 ns |  2.42 ns | 1.44 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   801.3 ns |  0.88 ns | 0.52 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   159.0 ns |  0.05 ns | 0.03 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,673.8 ns |  4.74 ns | 2.48 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,184.2 ns |  1.15 ns | 0.60 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,158.98 ns | 19.996 ns | 13.226 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.54 ns |  0.014 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.73 ns |  0.022 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.35 ns |  0.043 ns |  0.025 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.36 ns |  0.595 ns |  0.354 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.48 ns |  0.006 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    115.61 ns |  5.037 ns |  2.997 ns |  1.00 |    0.04 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.90 ns |  0.097 ns |  0.064 ns |  0.48 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     296.4 ns |   2.44 ns |   1.62 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,373.4 ns | 259.30 ns | 154.31 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     225.2 ns |   0.98 ns |   0.58 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 125,992.6 ns | 250.28 ns | 130.90 ns |      - |      80 B |

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