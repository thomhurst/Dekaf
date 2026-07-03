---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 22:43 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,905.69 μs** |    **601.30 μs** |    **32.959 μs** | **5,909.90 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,116.76 μs |    365.34 μs |    20.026 μs | 1,111.23 μs |  0.19 |    0.00 |   1.9531 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,077.10 μs** |  **1,791.93 μs** |    **98.222 μs** | **7,039.38 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,137.31 μs |    179.98 μs |     9.865 μs | 2,137.83 μs |  0.30 |    0.00 |  19.5313 |  3.9063 |   339.5 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,667.89 μs** |  **6,821.91 μs** |   **373.932 μs** | **6,551.59 μs** |  **1.00** |    **0.07** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,104.28 μs |    235.58 μs |    12.913 μs | 1,097.21 μs |  0.17 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,329.81 μs** | **17,291.04 μs** |   **947.780 μs** | **8,945.02 μs** |  **1.01** |    **0.12** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,397.70 μs |    610.03 μs |    33.438 μs | 4,385.58 μs |  0.47 |    0.04 |  15.6250 |       - |  361.72 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    38.78 μs |     33.90 μs |     1.858 μs |    37.85 μs |     ? |       ? |   0.2441 |       - |    9.98 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **978.36 μs** |  **2,528.05 μs** |   **138.571 μs** |   **986.32 μs** |  **1.01** |    **0.18** |  **24.4141** |       **-** |  **405.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   459.71 μs |    707.43 μs |    38.777 μs |   440.16 μs |  0.48 |    0.07 |   1.9531 |       - |   69.36 KB |        0.17 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   187.59 μs |    869.32 μs |    47.650 μs |   169.75 μs |     ? |       ? |   0.4883 |       - |   11.62 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 4,550.13 μs | 45,171.86 μs | 2,476.022 μs | 5,722.05 μs |     ? |       ? |   3.9063 |       - |  109.63 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,333.36 μs** |     **22.97 μs** |     **1.259 μs** | **5,332.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,239.65 μs |    174.36 μs |     9.557 μs | 1,237.00 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,386.55 μs** |  **1,404.90 μs** |    **77.007 μs** | **5,348.08 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,308.15 μs |    448.84 μs |    24.603 μs | 1,322.15 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,682.38 μs** |  **5,372.02 μs** |   **294.458 μs** | **5,655.71 μs** |  **1.00** |    **0.06** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,420.26 μs |  4,542.32 μs |   248.980 μs | 1,311.56 μs |  0.25 |    0.04 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,347.98 μs** |    **217.08 μs** |    **11.899 μs** | **5,345.06 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,092.99 μs |     30.57 μs |     1.676 μs | 1,092.17 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,165.615 ms** | **30.855 ms** | **1.6913 ms** | **3,165.848 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    12.832 ms |  9.637 ms | 0.5282 ms |    12.625 ms | 0.004 |  603.52 KB |        8.09 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,163.599 ms** | **17.054 ms** | **0.9348 ms** | **3,163.690 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    11.650 ms | 17.927 ms | 0.9826 ms |    11.304 ms | 0.004 |  777.67 KB |        3.11 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.276 ms** |  **6.043 ms** | **0.3313 ms** | **3,164.337 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    13.292 ms |  7.136 ms | 0.3911 ms |    13.300 ms | 0.004 |  996.22 KB |        1.65 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.686 ms** | **15.332 ms** | **0.8404 ms** | **3,165.094 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    12.861 ms | 16.916 ms | 0.9272 ms |    12.862 ms | 0.004 | 2835.59 KB |        1.20 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.977 ms** | **16.269 ms** | **0.8918 ms** | **3,156.354 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.258 ms |  3.763 ms | 0.2062 ms |     5.210 ms | 0.002 |  184.49 KB |       76.67 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.350 ms** | **25.450 ms** | **1.3950 ms** | **3,156.087 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.767 ms |  6.105 ms | 0.3347 ms |     4.806 ms | 0.002 |  186.35 KB |       44.75 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.941 ms** | **35.517 ms** | **1.9468 ms** | **3,155.927 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.908 ms | 47.227 ms | 2.5887 ms |     5.460 ms | 0.002 |  185.97 KB |       77.29 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.154 ms** | **10.204 ms** | **0.5593 ms** | **3,156.347 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.381 ms | 28.317 ms | 1.5522 ms |     4.612 ms | 0.002 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 18.213 μs | 107.0051 μs | 5.8653 μs | 14.837 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.567 μs |   3.2839 μs | 0.1800 μs |  9.567 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.141 μs |   3.6866 μs | 0.2021 μs | 10.108 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.701 μs |   0.8532 μs | 0.0468 μs | 26.674 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.938 μs |   1.2171 μs | 0.0667 μs |  8.921 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.440 μs |   1.1870 μs | 0.0651 μs | 19.437 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.068 μs |   4.7574 μs | 0.2608 μs | 18.054 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.267 μs |  34.1281 μs | 1.8707 μs | 20.399 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.537 μs |   2.1612 μs | 0.1185 μs |  4.473 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.557 μs |  94.2850 μs | 5.1681 μs | 10.745 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,329.7 ns |     379.8 ns |    20.82 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,633.3 ns |   6,730.7 ns |   368.93 ns |  0.39 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,596.7 ns |   4,845.1 ns |   265.58 ns |  0.38 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,560.7 ns |     278.7 ns |    15.28 ns |  0.62 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    761.0 ns |     182.4 ns |    10.00 ns |  0.18 |    0.00 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 44,178.7 ns | 172,901.4 ns | 9,477.31 ns | 10.63 |    1.99 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,158.0 ns |   1,740.3 ns |    95.39 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,588.7 ns |  19,475.8 ns | 1,067.54 ns |  1.10 |    0.22 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.101 μs |   3.589 μs |  0.1967 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   544.777 μs | 569.284 μs | 31.2044 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.194 μs |   3.286 μs |  0.1801 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,676.850 μs | 174.101 μs |  9.5431 μs |    1280 B |


---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to baseline (Confluent.Kafka)
  - `< 1.0` = Dekaf is faster
  - `> 1.0` = Confluent is faster
  - `1.0` = Same performance
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*