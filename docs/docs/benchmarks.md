---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 22:18 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev     | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|-----------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,874.33 μs** |   **357.46 μs** |  **19.594 μs** | **5,881.15 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,136.50 μs |   166.52 μs |   9.127 μs | 1,136.51 μs |  0.19 |    0.00 |   1.9531 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,932.61 μs** | **1,218.93 μs** |  **66.814 μs** | **6,911.32 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,116.14 μs |   575.75 μs |  31.559 μs | 2,132.14 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.46 KB |        0.32 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,361.87 μs** |   **412.08 μs** |  **22.587 μs** | **6,350.28 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,395.42 μs |   632.59 μs |  34.675 μs | 1,405.39 μs |  0.22 |    0.00 |   1.9531 |       - |    36.3 KB |        0.19 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,848.95 μs** | **8,612.57 μs** | **472.084 μs** | **8,767.21 μs** |  **1.00** |    **0.07** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,758.01 μs | 8,133.75 μs | 445.838 μs | 4,817.21 μs |  0.54 |    0.05 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |          **NA** |          **NA** |         **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    44.20 μs |   131.98 μs |   7.234 μs |    43.95 μs |     ? |       ? |   0.2441 |       - |    9.94 KB |           ? |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,073.36 μs** | **5,154.67 μs** | **282.545 μs** |   **910.84 μs** |  **1.04** |    **0.32** |  **24.4141** |       **-** |  **401.71 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   844.32 μs | 4,507.51 μs | 247.072 μs |   964.96 μs |  0.82 |    0.27 |        - |       - |   48.84 KB |        0.12 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |          **NA** |         **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   332.08 μs | 6,073.84 μs | 332.928 μs |   143.14 μs |     ? |       ? |   0.4883 |       - |    12.1 KB |           ? |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |          **NA** |         **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,526.89 μs | 3,259.66 μs | 178.673 μs | 1,431.62 μs |     ? |       ? |   7.8125 |       - | 1059.33 KB |           ? |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,322.19 μs** |    **11.45 μs** |   **0.628 μs** | **5,321.94 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,315.24 μs |    37.64 μs |   2.063 μs | 1,314.33 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,319.15 μs** |    **46.08 μs** |   **2.526 μs** | **5,319.53 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,326.71 μs |   275.91 μs |  15.124 μs | 1,323.85 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,326.33 μs** |    **12.18 μs** |   **0.668 μs** | **5,326.27 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,308.30 μs |    59.42 μs |   3.257 μs | 1,308.06 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |             |            |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,325.31 μs** |    **60.64 μs** |   **3.324 μs** | **5,323.92 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,316.21 μs |   394.84 μs |  21.642 μs | 1,326.40 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,165.217 ms** | **30.487 ms** | **1.6711 ms** | **3,165.735 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13.841 ms | 15.695 ms | 0.8603 ms |    13.960 ms | 0.004 |  597.29 KB |        8.00 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.212 ms** | **69.521 ms** | **3.8107 ms** | **3,164.104 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    11.219 ms | 14.076 ms | 0.7715 ms |    11.594 ms | 0.004 |  788.87 KB |        3.15 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,163.802 ms** | **13.443 ms** | **0.7369 ms** | **3,163.683 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    11.958 ms | 13.636 ms | 0.7474 ms |    11.760 ms | 0.004 |  999.85 KB |        1.66 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163.474 ms** | **13.865 ms** | **0.7600 ms** | **3,163.713 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.058 ms | 11.930 ms | 0.6539 ms |    13.325 ms | 0.004 | 2765.38 KB |        1.17 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.889 ms** | **25.679 ms** | **1.4075 ms** | **3,156.195 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.332 ms | 62.232 ms | 3.4111 ms |     4.508 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.169 ms** | **23.742 ms** | **1.3014 ms** | **3,155.843 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.618 ms |  6.036 ms | 0.3308 ms |     4.739 ms | 0.001 |   191.1 KB |       45.89 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,154.770 ms** |  **8.230 ms** | **0.4511 ms** | **3,154.722 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     5.378 ms |  8.272 ms | 0.4534 ms |     5.391 ms | 0.002 |  265.87 KB |      110.49 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.436 ms** |  **5.103 ms** | **0.2797 ms** | **3,156.501 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.458 ms |  5.951 ms | 0.3262 ms |     5.536 ms | 0.002 |  187.27 KB |       44.80 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.341 μs | 45.7959 μs | 2.5102 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.256 μs |  2.5886 μs | 0.1419 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.161 μs | 28.2027 μs | 1.5459 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 42.082 μs | 10.3640 μs | 0.5681 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.143 μs |  0.6494 μs | 0.0356 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.351 μs |  2.2748 μs | 0.1247 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.867 μs |  6.1410 μs | 0.3366 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.887 μs | 13.8040 μs | 0.7566 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.481 μs |  2.3562 μs | 0.1292 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.841 μs |  5.4914 μs | 0.3010 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,560.0 ns | 4,571.4 ns | 250.57 ns |  0.38 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,499.2 ns | 5,646.8 ns | 309.52 ns |  0.36 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,425.3 ns | 2,345.8 ns | 128.58 ns |  0.35 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,475.0 ns | 1,590.5 ns |  87.18 ns |  0.60 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    725.3 ns | 1,241.8 ns |  68.07 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,491.3 ns | 2,556.3 ns | 140.12 ns | 10.09 |    0.34 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,117.0 ns | 2,867.2 ns | 157.16 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,900.7 ns | 1,484.4 ns |  81.37 ns |  0.95 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.42 μs |   1.558 μs |  0.085 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   530.23 μs | 256.949 μs | 14.084 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.02 μs |   2.597 μs |  0.142 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,716.76 μs | 160.513 μs |  8.798 μs |    1280 B |


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