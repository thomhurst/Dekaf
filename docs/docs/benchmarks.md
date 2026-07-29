---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-29 11:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.22 | 0.89–1.27 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.46 | 0.90–1.74 | 58% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.77 | 0.66–1.36 | 89% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.53 | 1.13–2.22 | 72% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 4 | 0.09 | 0.08–0.10 | 14% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 4 | 0.33 | 0.33–0.39 | 18% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.04 | 0.97–1.14 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.09 | 0.90–1.14 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.97–1.03 | 5% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.91–1.00 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.44 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.53 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.39–0.44 | 12% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.03 | 0.98–1.08 | 10% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.46–0.48 | 3% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,008.1 μs** | **149.94 μs** |  **99.17 μs** |  **1.00** |    **0.02** |       **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,624.7 μs |  43.52 μs |  28.78 μs |  0.44 |    0.01 |       - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,172.1 μs** |  **92.84 μs** |  **61.41 μs** |  **1.00** |    **0.01** | **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,772.4 μs |  70.88 μs |  42.18 μs |  0.53 |    0.01 |       - |       - |   51759 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,653.8 μs** |  **29.05 μs** |  **17.29 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,627.8 μs |  86.00 μs |  51.18 μs |  0.39 |    0.01 |       - |       - |    6283 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,366.9 μs** | **316.42 μs** | **209.29 μs** |  **1.00** |    **0.02** | **93.7500** | **31.2500** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,299.3 μs | 617.59 μs | 408.50 μs |  1.08 |    0.04 |       - |       - |   69986 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **116.2 μs** |   **3.91 μs** |   **2.58 μs** |  **1.00** |    **0.03** |  **1.7090** |       **-** |   **30400 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    117.8 μs |  18.43 μs |  10.97 μs |  1.01 |    0.09 |       - |       - |     473 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,156.6 μs** |  **43.81 μs** |  **28.98 μs** |  **1.00** |    **0.03** | **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,307.2 μs | 261.33 μs | 136.68 μs |  1.13 |    0.11 |       - |       - |    2356 B |       0.008 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **915.6 μs** |  **14.77 μs** |   **8.79 μs** |  **1.00** |    **0.01** |  **7.0801** |       **-** |  **121392 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    939.7 μs |  79.03 μs |  52.27 μs |  1.03 |    0.06 |       - |       - |    1901 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,218.8 μs** | **123.28 μs** |  **81.54 μs** |  **1.00** |    **0.01** | **70.3125** |       **-** | **1214640 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,239.5 μs | 529.94 μs | 350.52 μs |  1.00 |    0.04 |       - |       - |   18657 B |        0.02 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,465.2 μs** |  **37.37 μs** |  **24.72 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,493.2 μs |   9.83 μs |   5.14 μs |  0.46 |    0.00 |       - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,454.0 μs** |  **27.55 μs** |  **16.40 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,509.3 μs |  27.54 μs |  18.22 μs |  0.46 |    0.00 |       - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,460.7 μs** |  **23.62 μs** |  **15.62 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,511.2 μs |  19.80 μs |  11.78 μs |  0.46 |    0.00 |       - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |           |           |       |         |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,461.5 μs** |  **32.25 μs** |  **21.33 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,519.5 μs |  23.49 μs |  15.54 μs |  0.46 |    0.00 |       - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean        | Error       | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |------------:|------------:|-----------:|------------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |    **95.99 μs** |    **21.64 μs** |   **9.610 μs** |    **94.11 μs** |  **1.01** |    **0.13** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   122.30 μs |    37.59 μs |  19.661 μs |   117.00 μs |  1.29 |    0.23 |  26.45 KB |        0.41 | Stable |
|                      |              |             |             |             |            |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **119.86 μs** |    **20.91 μs** |   **9.284 μs** |   **117.57 μs** |  **1.00** |    **0.10** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   174.44 μs |    38.39 μs |  20.080 μs |   183.34 μs |  1.46 |    0.19 | 202.23 KB |        0.84 | Stable |
|                      |              |             |             |             |            |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **964.82 μs** |   **617.05 μs** | **322.731 μs** |   **768.27 μs** |  **1.08** |    **0.44** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   746.62 μs |   148.72 μs |  66.035 μs |   770.25 μs |  0.84 |    0.22 | 258.48 KB |        0.40 | Stable |
|                      |              |             |             |             |            |             |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        |   **992.92 μs** |    **39.70 μs** |  **14.158 μs** |   **995.31 μs** |  **1.00** |    **0.02** | **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,778.16 μs | 1,542.87 μs | 685.045 μs | 2,223.61 μs |  1.79 |    0.65 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,525.5 ns** |    **23.54 ns** |    **12.31 ns** | **5,522.4 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   467.0 ns |    23.16 ns |    15.32 ns |   460.8 ns |  0.08 |    0.00 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |             |            |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,019.0 ns** | **1,608.84 ns** | **1,064.15 ns** | **3,645.2 ns** |  **1.19** |    **0.73** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,165.7 ns |   103.64 ns |    68.55 ns | 1,150.0 ns |  0.46 |    0.22 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 450.96 ns | 1.576 ns | 0.409 ns | 0.0730 |    1224 B |
| WriteFindCoordinatorV6     |  29.00 ns | 0.195 ns | 0.030 ns |      - |         - |
| WriteDescribeGroupsV6      |  49.84 ns | 0.244 ns | 0.038 ns |      - |         - |
| WriteListConfigResourcesV1 |  19.48 ns | 0.171 ns | 0.027 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.076 μs** | **0.0105 μs** | **0.0027 μs** |         **-** |
| **WriteRequest** | **1**       | **2.073 μs** | **0.0033 μs** | **0.0005 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.451 μs** | **0.0117 μs** | **0.0030 μs** |         **-** |
| **WriteRequest** | **9**       | **2.460 μs** | **0.0285 μs** | **0.0044 μs** |         **-** |
| **WriteRequest** | **10**      | **2.477 μs** | **0.0184 μs** | **0.0028 μs** |         **-** |
| **WriteRequest** | **11**      | **2.467 μs** | **0.0382 μs** | **0.0059 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **107.80 ns** | **0.145 ns** | **0.022 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  98.89 ns | 0.512 ns | 0.133 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **97.15 ns** | **0.212 ns** | **0.055 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  94.65 ns | 1.075 ns | 0.166 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,739.2 ns |  6.49 ns |  4.29 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,246.8 ns |  3.51 ns |  2.32 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,412.7 ns | 18.52 ns | 11.02 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,385.2 ns |  7.15 ns |  4.25 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,234.6 ns |  4.02 ns |  2.39 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,065.1 ns | 14.46 ns |  9.56 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,942.5 ns |  5.40 ns |  3.57 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,917.7 ns | 10.99 ns |  6.54 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,191.8 ns |  1.81 ns |  0.95 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,041.9 ns |  3.79 ns |  2.51 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   699.7 ns |  2.12 ns |  1.26 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   850.9 ns |  1.87 ns |  1.11 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   160.1 ns |  0.47 ns |  0.31 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,709.8 ns |  0.96 ns |  0.50 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,191.1 ns |  2.13 ns |  1.27 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,491.79 ns | 47.278 ns | 31.271 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |  0.020 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.73 ns |  0.039 ns |  0.023 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.94 ns |  0.237 ns |  0.157 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.39 ns |  0.108 ns |  0.071 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.004 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.41 ns |  0.345 ns |  0.228 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     53.56 ns |  0.611 ns |  0.404 ns |  0.48 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     295.8 ns |   0.60 ns |   0.36 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 104,914.0 ns | 103.31 ns |  54.03 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     209.7 ns |   2.08 ns |   1.24 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 103,226.0 ns | 250.70 ns | 131.12 ns |      - |      80 B |

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