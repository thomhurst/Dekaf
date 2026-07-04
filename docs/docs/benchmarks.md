---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 00:51 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,086.38 μs** |   **851.41 μs** |  **46.669 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,300.09 μs | 1,770.02 μs |  97.021 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,150.82 μs** |   **994.41 μs** |  **54.507 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,220.54 μs |   284.02 μs |  15.568 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |   339.5 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,617.96 μs** |   **740.56 μs** |  **40.592 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,131.90 μs |   372.52 μs |  20.419 μs |  0.17 |    0.00 |        - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,352.86 μs** | **1,657.28 μs** |  **90.841 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,066.92 μs | 2,358.11 μs | 129.256 μs |  0.53 |    0.01 |  15.6250 |       - |   361.7 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **146.92 μs** |   **252.22 μs** |  **13.825 μs** |  **1.01** |    **0.12** |   **2.4414** |       **-** |   **41.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     52.82 μs |    31.17 μs |   1.709 μs |  0.36 |    0.03 |   0.2441 |       - |    8.77 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,325.73 μs** | **2,381.60 μs** | **130.543 μs** |  **1.01** |    **0.12** |  **23.4375** |       **-** |  **408.77 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    624.86 μs |   840.53 μs |  46.073 μs |  0.47 |    0.05 |        - |       - |   76.68 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    196.39 μs |   110.25 μs |   6.043 μs |     ? |       ? |   0.4883 |       - |   12.13 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,108.02 μs | 3,129.68 μs | 171.548 μs |     ? |       ? |   7.8125 |       - | 1046.16 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,399.90 μs** |    **43.27 μs** |   **2.372 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,104.93 μs |    19.92 μs |   1.092 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,405.54 μs** |    **65.46 μs** |   **3.588 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,106.30 μs |   103.48 μs |   5.672 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,406.61 μs** |    **41.32 μs** |   **2.265 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,304.97 μs |   321.77 μs |  17.637 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.49 μs** |    **78.49 μs** |   **4.302 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,357.32 μs |   460.28 μs |  25.229 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.455 ms** | **24.458 ms** | **1.3406 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.960 ms | 36.521 ms | 2.0018 ms | 0.005 |  592.69 KB |        7.94 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.993 ms** | **13.253 ms** | **0.7264 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.361 ms | 27.220 ms | 1.4920 ms | 0.005 |   781.1 KB |        3.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.917 ms** |  **2.469 ms** | **0.1353 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.969 ms | 49.434 ms | 2.7096 ms | 0.005 | 1067.47 KB |        1.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.471 ms** | **33.986 ms** | **1.8629 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.937 ms | 28.249 ms | 1.5484 ms | 0.005 | 2763.71 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.607 ms** | **19.798 ms** | **1.0852 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.838 ms | 12.118 ms | 0.6642 ms | 0.002 |  185.72 KB |       77.18 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.634 ms** | **10.288 ms** | **0.5639 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.923 ms | 19.809 ms | 1.0858 ms | 0.002 |  190.88 KB |       45.84 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.566 ms** | **23.897 ms** | **1.3098 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.177 ms |  9.177 ms | 0.5030 ms | 0.002 |  184.76 KB |       76.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.222 ms** | **25.674 ms** | **1.4073 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.366 ms |  9.091 ms | 0.4983 ms | 0.002 |  188.55 KB |       45.11 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.812 μs |   7.372 μs |  0.4041 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.502 μs |   9.169 μs |  0.5026 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.756 μs |   8.472 μs |  0.4644 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.427 μs | 186.718 μs | 10.2346 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.554 μs |  12.556 μs |  0.6883 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.684 μs |   8.232 μs |  0.4512 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 25.997 μs | 168.146 μs |  9.2167 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.710 μs |  40.376 μs |  2.2131 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.307 μs |   9.889 μs |  0.5421 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.699 μs |   7.486 μs |  0.4103 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.861 μs |  6.158 μs | 0.3376 μs |  0.38 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.193 μs |  9.266 μs | 0.5079 μs |  0.24 |    0.09 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.564 μs | 10.714 μs | 0.5873 μs |  0.32 |    0.11 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3.556 μs | 18.419 μs | 1.0096 μs |  0.72 |    0.19 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.000 μs |  9.068 μs | 0.4971 μs |  0.20 |    0.09 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 33.696 μs | 26.459 μs | 1.4503 μs |  6.85 |    0.65 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.954 μs |  9.158 μs | 0.5020 μs |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3.869 μs | 17.233 μs | 0.9446 μs |  0.79 |    0.18 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.447 μs |  68.371 μs |  3.7477 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   472.099 μs | 210.720 μs | 11.5503 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.672 μs |  11.374 μs |  0.6234 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,368.055 μs | 279.176 μs | 15.3026 μs |    1280 B |


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