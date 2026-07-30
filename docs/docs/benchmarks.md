---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-30 15:49 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.95 | 0.89–1.05 | 17% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.02 | 0.94–1.24 | 29% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.70 | 0.65–0.86 | 30% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.13 | 1.03–1.60 | 50% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.07–0.09 | 18% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.34 | 0.16–0.37 | 61% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.02 | 0.79–1.08 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.00 | 0.85–1.07 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.98–1.14 | 17% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.97 | 0.95–1.11 | 16% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.43–0.44 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.50–0.52 | 4% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.43 | 0.40–0.48 | 17% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.05 | 1.00–1.48 | 46% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 2% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 2% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.46–0.48 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.47–0.47 | 2% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,882.7 μs** |   **136.85 μs** |    **90.51 μs** |  **1.00** |    **0.02** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,558.1 μs |    23.11 μs |    15.29 μs |  0.43 |    0.01 |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,479.1 μs** |    **56.25 μs** |    **29.42 μs** |  **1.00** |    **0.01** |       **-** | **1048372 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,738.8 μs |    35.02 μs |    20.84 μs |  0.50 |    0.00 |       - |   50633 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,521.9 μs** |    **43.80 μs** |    **26.07 μs** |  **1.00** |    **0.01** |       **-** |  **194770 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,102.5 μs |   135.17 μs |    89.41 μs |  0.48 |    0.01 |       - |    6149 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **8,185.9 μs** |   **195.64 μs** |   **116.42 μs** |  **1.00** |    **0.02** | **15.6250** | **1944635 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,148.6 μs | 2,141.68 μs | 1,274.48 μs |  1.48 |    0.15 |       - |   67677 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **130.9 μs** |     **1.49 μs** |     **0.99 μs** |  **1.00** |    **0.01** |  **0.2441** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    103.3 μs |    25.56 μs |    15.21 μs |  0.79 |    0.11 |       - |     197 B |       0.006 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,307.9 μs** |    **35.07 μs** |    **20.87 μs** |  **1.00** |    **0.02** |  **1.9531** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,109.2 μs |   179.59 μs |   106.87 μs |  0.85 |    0.08 |       - |    1868 B |       0.006 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **727.1 μs** |    **93.30 μs** |    **48.80 μs** |  **1.00** |    **0.09** |  **1.2207** |  **120999 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    828.9 μs |   147.21 μs |    87.60 μs |  1.14 |    0.14 |       - |    1737 B |        0.01 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,992.1 μs** | **1,390.03 μs** |   **919.42 μs** |  **1.01** |    **0.15** | **13.6719** | **1211022 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,837.0 μs | 1,837.97 μs | 1,215.71 μs |  1.12 |    0.19 |       - |   16624 B |        0.01 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,412.9 μs** |    **31.00 μs** |    **16.21 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,524.8 μs |    10.92 μs |     7.23 μs |  0.47 |    0.00 |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,433.5 μs** |    **75.39 μs** |    **39.43 μs** |  **1.00** |    **0.01** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,516.6 μs |    24.05 μs |    14.31 μs |  0.46 |    0.00 |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,418.1 μs** |     **8.81 μs** |     **5.24 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,505.1 μs |    15.52 μs |    10.26 μs |  0.46 |    0.00 |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |             |             |       |         |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.8 μs** |    **15.43 μs** |     **9.18 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,526.7 μs |    17.85 μs |    10.62 μs |  0.47 |    0.00 |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **153.8 μs** |    **55.54 μs** |  **29.05 μs** |  **1.03** |    **0.25** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   146.5 μs |    48.50 μs |  25.37 μs |  0.98 |    0.22 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **188.0 μs** |    **48.34 μs** |  **21.46 μs** |  **1.01** |    **0.15** | **240.77 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 1000        |   177.0 μs |    43.05 μs |  19.12 μs |  0.95 |    0.14 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,155.3 μs** |   **363.33 μs** | **161.32 μs** |  **1.01** |    **0.18** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   855.2 μs |   112.91 μs |  50.13 μs |  0.75 |    0.10 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,700.0 μs** |   **791.12 μs** | **413.77 μs** |  **1.05** |    **0.33** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,754.8 μs | 1,001.66 μs | 444.74 μs |  1.08 |    0.35 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------------ |------------ |-----------:|---------:|---------:|------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,700.9 ns** | **42.17 ns** | **27.89 ns** |  **1.00** | **0.0075** |     **654 B** |        **1.00** |
| Dekaf_PollSingle     | 400000            | 100         |   426.2 ns | 28.19 ns | 18.65 ns |  0.07 | 0.0025 |     270 B |        0.41 |
|                      |                   |             |            |          |          |       |        |           |             |
| **Confluent_PollSingle** | **400000**            | **1000**        | **6,206.7 ns** | **29.30 ns** | **19.38 ns** |  **1.00** | **0.0275** |    **2454 B** |        **1.00** |
| Dekaf_PollSingle     | 400000            | 1000        |   975.3 ns | 60.72 ns | 36.13 ns |  0.16 | 0.0225 |    2075 B |        0.85 |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 542.33 ns | 22.129 ns | 5.747 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.28 ns |  0.172 ns | 0.045 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.92 ns |  0.367 ns | 0.095 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.21 ns |  0.266 ns | 0.041 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.001 μs** | **0.0019 μs** | **0.0003 μs** |         **-** |
| **WriteRequest** | **1**       | **2.002 μs** | **0.0072 μs** | **0.0019 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.091 μs** | **0.0154 μs** | **0.0040 μs** |         **-** |
| **WriteRequest** | **9**       | **2.111 μs** | **0.0033 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **10**      | **2.703 μs** | **0.0118 μs** | **0.0018 μs** |         **-** |
| **WriteRequest** | **11**      | **2.166 μs** | **0.0071 μs** | **0.0018 μs** |         **-** |

| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **85.63 ns** | **0.539 ns** | **0.140 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 86.19 ns | 0.395 ns | 0.061 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **77.36 ns** | **0.221 ns** | **0.057 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 75.42 ns | 0.093 ns | 0.014 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,693.6 ns | 12.33 ns |  6.45 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,003.3 ns | 16.72 ns | 11.06 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,262.8 ns |  3.80 ns |  2.26 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,197.0 ns |  5.52 ns |  3.29 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,765.7 ns |  1.68 ns |  0.88 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,692.5 ns |  5.36 ns |  3.54 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,584.7 ns |  4.59 ns |  2.73 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,579.7 ns |  2.30 ns |  1.20 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 2,370.0 ns |  1.81 ns |  1.08 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,619.4 ns | 17.66 ns | 11.68 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   649.4 ns |  1.46 ns |  0.96 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   783.5 ns |  0.56 ns |  0.37 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   166.9 ns |  0.10 ns |  0.07 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,680.3 ns |  2.45 ns |  1.62 ns | 0.0114 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,148.9 ns |  0.50 ns |  0.29 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,634.30 ns | 37.811 ns | 25.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.15 ns |  0.013 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.89 ns |  0.017 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.69 ns |  0.022 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.81 ns |  0.132 ns |  0.079 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.95 ns |  0.004 ns |  0.002 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.09 ns |  1.939 ns |  1.154 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.91 ns |  0.126 ns |  0.066 ns |  0.49 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|------------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     289.5 ns |     1.73 ns |   1.14 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,944.4 ns |   179.65 ns | 106.91 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     225.6 ns |     0.71 ns |   0.42 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,015.8 ns | 1,397.95 ns | 924.66 ns |      - |      80 B |

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