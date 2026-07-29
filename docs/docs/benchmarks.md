---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-29 19:41 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.89 | 0.85–1.02 | 19% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.02 | 0.91–1.08 | 17% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.69 | 0.65–0.87 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.25 | 0.91–1.39 | 39% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.09–0.09 | 5% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.33 | 0.32–0.39 | 20% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.07 | 0.96–1.19 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.04 | 1.00–1.09 | 9% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.92–1.01 | 8% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.95–0.99 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.45 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.52 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.40–0.46 | 14% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.02 | 0.98–1.08 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.47 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.48 | 0.47–0.48 | 2% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,108.5 μs** |    **64.99 μs** |  **42.99 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,677.9 μs |    10.08 μs |   6.67 μs |  0.44 |    0.00 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,401.8 μs** |   **126.45 μs** |  **83.64 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,764.7 μs |    43.87 μs |  26.11 μs |  0.51 |    0.01 |        - |       - |   51578 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,471.0 μs** |   **136.57 μs** |  **81.27 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,627.5 μs |    30.96 μs |  18.42 μs |  0.41 |    0.01 |        - |       - |    6085 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,037.4 μs** |   **288.93 μs** | **171.94 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,665.5 μs | 1,121.46 μs | 741.78 μs |  1.05 |    0.06 |        - |       - |   68826 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.4 μs** |    **27.59 μs** |  **16.42 μs** |  **1.02** |    **0.19** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    130.6 μs |    22.01 μs |  14.56 μs |  0.98 |    0.18 |        - |       - |     226 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,278.6 μs** |    **31.07 μs** |  **20.55 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,336.0 μs |   217.31 μs | 143.74 μs |  1.05 |    0.11 |        - |       - |    1979 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,046.3 μs** |     **6.88 μs** |   **4.10 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121520 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,022.0 μs |   145.90 μs |  96.50 μs |  0.98 |    0.09 |        - |       - |    1795 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,309.7 μs** |   **128.62 μs** |  **67.27 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215258 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,830.8 μs |   277.01 μs | 183.23 μs |  0.95 |    0.02 |        - |       - |   17258 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,474.9 μs** |    **12.69 μs** |   **8.39 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,583.3 μs |    15.13 μs |  10.01 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,477.5 μs** |    **32.12 μs** |  **16.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,588.7 μs |    18.59 μs |  12.30 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,494.5 μs** |     **7.35 μs** |   **4.38 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,593.2 μs |     6.29 μs |   4.16 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,485.4 μs** |    **21.53 μs** |  **14.24 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,575.2 μs |    10.73 μs |   7.10 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **133.2 μs** |    **50.50 μs** |  **26.41 μs** |   **131.4 μs** |  **1.03** |    **0.27** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   118.4 μs |    36.13 μs |  18.90 μs |   121.2 μs |  0.92 |    0.22 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **147.5 μs** |    **45.41 μs** |  **23.75 μs** |   **139.1 μs** |  **1.02** |    **0.21** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   149.7 μs |    49.46 μs |  25.87 μs |   134.8 μs |  1.04 |    0.22 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,067.7 μs** |   **628.39 μs** | **328.66 μs** |   **892.7 μs** |  **1.07** |    **0.41** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   695.9 μs |    84.00 μs |  29.95 μs |   690.6 μs |  0.70 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,072.6 μs** |    **35.75 μs** |  **12.75 μs** | **1,078.8 μs** |  **1.00** |    **0.02** | **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,337.3 μs | 1,197.83 μs | 531.84 μs |   923.9 μs |  1.25 |    0.46 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,539.4 ns** |    **56.26 ns** |  **33.48 ns** |  **1.00** |    **0.01** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   472.0 ns |    37.38 ns |  22.25 ns |  0.09 |    0.00 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,453.2 ns** | **1,087.53 ns** | **719.34 ns** |  **1.09** |    **0.51** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,107.3 ns |    43.19 ns |  22.59 ns |  0.35 |    0.15 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 514.37 ns | 3.475 ns | 0.903 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.13 ns | 0.088 ns | 0.023 ns |      - |         - |
| WriteDescribeGroupsV6      |  47.23 ns | 0.161 ns | 0.042 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.18 ns | 0.074 ns | 0.019 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.002 μs** | **0.0034 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **1**       | **1.962 μs** | **0.0033 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.450 μs** | **0.0046 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **9**       | **2.459 μs** | **0.0552 μs** | **0.0143 μs** |         **-** |
| **WriteRequest** | **10**      | **2.472 μs** | **0.0067 μs** | **0.0017 μs** |         **-** |
| **WriteRequest** | **11**      | **2.453 μs** | **0.0035 μs** | **0.0009 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **107.47 ns** | **0.978 ns** | **0.254 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.90 ns | 1.325 ns | 0.205 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **95.08 ns** | **0.782 ns** | **0.203 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  95.43 ns | 0.750 ns | 0.116 ns |         - |

| Method                                          | Mean       | Error    | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,742.5 ns | 10.43 ns | 6.21 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,067.5 ns |  8.40 ns | 5.56 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,488.5 ns |  2.54 ns | 1.51 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,460.8 ns |  7.62 ns | 4.53 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,001.9 ns |  2.42 ns | 1.44 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,036.1 ns |  9.74 ns | 5.80 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,977.0 ns |  3.52 ns | 2.10 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,901.7 ns |  7.03 ns | 4.65 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,193.3 ns |  3.52 ns | 1.84 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,042.8 ns |  3.14 ns | 2.08 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   787.2 ns |  1.84 ns | 1.10 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   779.7 ns |  1.70 ns | 1.01 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   161.3 ns |  0.38 ns | 0.23 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,719.3 ns |  3.89 ns | 2.31 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,186.8 ns |  2.26 ns | 1.18 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,955.41 ns | 10.467 ns | 5.475 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |  0.022 ns | 0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.81 ns |  0.011 ns | 0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.34 ns |  0.063 ns | 0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.87 ns |  0.869 ns | 0.575 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.90 ns |  0.010 ns | 0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.55 ns |  1.141 ns | 0.597 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.18 ns |  0.074 ns | 0.049 ns |  0.49 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     311.2 ns |   1.24 ns |   0.74 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,372.9 ns | 170.54 ns | 112.80 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     218.9 ns |   0.58 ns |   0.35 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,559.2 ns | 318.33 ns | 189.43 ns |      - |      80 B |

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