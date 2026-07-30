---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-30 01:38 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.89 | 0.89–1.05 | 18% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.02 | 0.91–1.24 | 33% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.69 | 0.65–0.86 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.25 | 1.05–1.60 | 44% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.09–0.09 | 5% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.34 | 0.32–0.39 | 19% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.04 | 0.96–1.13 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.06 | 0.97–1.09 | 11% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.98–1.00 | 3% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.95–0.99 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.44 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.52 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.40–0.43 | 7% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.05 | 1.00–1.08 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,063.4 μs** |    **93.68 μs** |  **61.96 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,667.6 μs |    16.95 μs |  11.21 μs |  0.44 |    0.00 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,406.4 μs** |    **96.64 μs** |  **63.92 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,800.6 μs |    60.08 μs |  39.74 μs |  0.51 |    0.01 |        - |       - |   51590 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,563.8 μs** |    **77.48 μs** |  **46.11 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,635.6 μs |    41.20 μs |  27.25 μs |  0.40 |    0.00 |        - |       - |    6090 B |        0.03 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,230.7 μs** |   **195.21 μs** | **129.12 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,241.1 μs | 1,714.76 μs | 896.85 μs |  1.00 |    0.07 |        - |       - |   68384 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **129.6 μs** |    **18.12 μs** |  **11.99 μs** |  **1.01** |    **0.13** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    134.6 μs |    28.11 μs |  18.60 μs |  1.05 |    0.17 |        - |       - |     150 B |       0.005 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,223.8 μs** |   **118.54 μs** |  **78.41 μs** |  **1.00** |    **0.09** |  **17.5781** |       **-** |  **304000 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,311.1 μs |    70.21 μs |  46.44 μs |  1.08 |    0.08 |        - |       - |    4427 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,026.3 μs** |    **11.44 μs** |   **6.81 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121486 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,008.7 μs |   146.27 μs |  87.05 μs |  0.98 |    0.08 |        - |       - |    1816 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,186.0 μs** |   **104.38 μs** |  **62.12 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215079 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,875.4 μs |   409.11 μs | 270.60 μs |  0.97 |    0.03 |        - |       - |   17037 B |        0.01 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,480.7 μs** |    **11.02 μs** |   **7.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,586.7 μs |     9.48 μs |   6.27 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,476.7 μs** |     **6.79 μs** |   **3.55 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,578.8 μs |     5.93 μs |   3.53 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,481.6 μs** |     **7.69 μs** |   **4.58 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,580.3 μs |    11.38 μs |   7.53 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,487.3 μs** |     **6.87 μs** |   **4.54 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,605.4 μs |    14.44 μs |   9.55 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **128.7 μs** |    **63.33 μs** |  **33.12 μs** |  **1.06** |    **0.35** |  **64.99 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 100         |   114.7 μs |    32.87 μs |  17.19 μs |  0.94 |    0.25 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **119.7 μs** |     **4.10 μs** |   **1.46 μs** |  **1.00** |    **0.02** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   148.4 μs |    50.02 μs |  26.16 μs |  1.24 |    0.21 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,053.5 μs** |   **605.99 μs** | **316.94 μs** |  **1.07** |    **0.40** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   719.8 μs |    87.93 μs |  39.04 μs |  0.73 |    0.17 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,074.6 μs** |    **56.07 μs** |  **19.99 μs** |  **1.00** |    **0.02** | **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,714.1 μs | 2,037.74 μs | 904.77 μs |  1.60 |    0.79 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,536.7 ns** |    **25.73 ns** |  **13.46 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   476.4 ns |    23.87 ns |  15.79 ns |  0.09 |    0.00 | 0.0150 |     271 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,278.8 ns** | **1,423.70 ns** | **941.69 ns** |  **1.14** |    **0.63** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,170.3 ns |    89.59 ns |  53.31 ns |  0.41 |    0.19 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 440.63 ns | 2.938 ns | 0.763 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  28.99 ns | 0.173 ns | 0.045 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.66 ns | 0.153 ns | 0.040 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.46 ns | 0.054 ns | 0.014 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.072 μs** | **0.0033 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **1**       | **2.074 μs** | **0.0025 μs** | **0.0006 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.377 μs** | **0.0055 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **9**       | **2.392 μs** | **0.0101 μs** | **0.0016 μs** |         **-** |
| **WriteRequest** | **10**      | **2.578 μs** | **0.0075 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **11**      | **2.387 μs** | **0.0190 μs** | **0.0029 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.54 ns** | **0.196 ns** | **0.030 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  95.08 ns | 0.275 ns | 0.043 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **92.99 ns** | **0.277 ns** | **0.043 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  87.33 ns | 0.168 ns | 0.044 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.1 ns | 0.59 ns | 0.35 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,130.9 ns | 3.19 ns | 1.67 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,303.2 ns | 3.76 ns | 1.97 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,881.7 ns | 4.32 ns | 2.57 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,943.4 ns | 2.93 ns | 1.94 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,985.2 ns | 7.85 ns | 4.67 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,905.0 ns | 3.00 ns | 1.57 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,843.5 ns | 3.00 ns | 1.98 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,144.4 ns | 1.11 ns | 0.66 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,818.6 ns | 4.90 ns | 3.24 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   722.2 ns | 1.35 ns | 0.81 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   839.4 ns | 3.59 ns | 2.37 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   167.8 ns | 0.28 ns | 0.17 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,716.5 ns | 4.39 ns | 2.61 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,293.4 ns | 0.94 ns | 0.62 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,659.29 ns | 26.080 ns | 17.250 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.41 ns |  0.017 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.92 ns |  0.019 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.64 ns |  0.034 ns |  0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     27.82 ns |  0.130 ns |  0.086 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.95 ns |  0.027 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    104.99 ns |  1.539 ns |  1.018 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.85 ns |  0.061 ns |  0.040 ns |  0.52 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     299.9 ns |   0.90 ns |   0.54 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 108,811.8 ns | 177.69 ns | 105.74 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     208.7 ns |   0.25 ns |   0.15 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,214.6 ns | 164.26 ns |  97.75 ns |      - |      80 B |

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