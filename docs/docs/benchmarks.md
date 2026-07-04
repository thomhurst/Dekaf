---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 10:43 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,166.48 μs** |    **347.42 μs** |    **19.043 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,334.86 μs |    946.14 μs |    51.861 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,350.46 μs** |    **744.83 μs** |    **40.827 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,255.11 μs |    664.83 μs |    36.442 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.48 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **7,138.60 μs** | **20,190.38 μs** | **1,106.703 μs** |  **1.01** |    **0.19** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,272.09 μs |  3,075.50 μs |   168.578 μs |  0.18 |    0.03 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,579.71 μs** |  **4,545.19 μs** |   **249.137 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,814.15 μs |  3,159.93 μs |   173.207 μs |  0.50 |    0.02 |  15.6250 |       - |  361.68 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **150.03 μs** |    **401.27 μs** |    **21.995 μs** |  **1.01** |    **0.18** |   **2.1973** |       **-** |   **36.37 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.17 μs |    114.10 μs |     6.254 μs |  0.39 |    0.06 |   0.2441 |       - |    9.28 KB |        0.26 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,221.47 μs** |  **1,341.92 μs** |    **73.555 μs** |  **1.00** |    **0.07** |  **21.4844** |       **-** |     **380 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    568.54 μs |    624.20 μs |    34.214 μs |  0.47 |    0.03 |        - |       - |   63.75 KB |        0.17 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    208.50 μs |    134.42 μs |     7.368 μs |     ? |       ? |   0.4883 |       - |      23 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,244.80 μs |  6,022.71 μs |   330.125 μs |     ? |       ? |   7.8125 |       - |  978.89 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,451.61 μs** |     **63.94 μs** |     **3.505 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,270.46 μs |    450.92 μs |    24.716 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,435.69 μs** |     **46.99 μs** |     **2.576 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,295.16 μs |    301.94 μs |    16.550 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,469.74 μs** |     **23.31 μs** |     **1.278 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,318.12 μs |    280.96 μs |    15.400 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,406.72 μs** |    **209.79 μs** |    **11.499 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,309.06 μs |    553.04 μs |    30.314 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.891 ms** | **32.183 ms** | **1.7641 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.956 ms | 43.473 ms | 2.3829 ms | 0.006 | 602.21 KB |        8.07 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.627 ms** | **11.330 ms** | **0.6210 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.492 ms | 42.248 ms | 2.3158 ms | 0.005 | 784.34 KB |        3.13 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.252 ms** | **20.576 ms** | **1.1278 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.249 ms | 22.814 ms | 1.2505 ms | 0.005 | 1003.5 KB |        1.67 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.421 ms** | **15.887 ms** | **0.8708 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.945 ms | 74.244 ms | 4.0696 ms | 0.006 | 2757.8 KB |        1.16 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.520 ms** |  **6.998 ms** | **0.3836 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.776 ms |  6.189 ms | 0.3393 ms | 0.002 | 198.02 KB |       82.29 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.350 ms** |  **7.967 ms** | **0.4367 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.815 ms | 19.027 ms | 1.0429 ms | 0.002 | 186.47 KB |       44.78 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.622 ms** | **43.066 ms** | **2.3606 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.918 ms |  9.920 ms | 0.5438 ms | 0.002 | 184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.658 ms** |  **7.750 ms** | **0.4248 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.896 ms |  6.127 ms | 0.3358 ms | 0.002 | 189.27 KB |       45.28 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.912 μs |   2.212 μs |  0.1212 μs | 14.893 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.484 μs |   1.827 μs |  0.1002 μs |  9.447 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.161 μs |   6.867 μs |  0.3764 μs |  9.963 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.684 μs |   2.209 μs |  0.1211 μs | 26.670 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.056 μs |   1.905 μs |  0.1044 μs |  9.106 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.508 μs |   1.695 μs |  0.0929 μs | 19.482 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.801 μs |   7.629 μs |  0.4182 μs | 21.601 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.629 μs |   8.330 μs |  0.4566 μs | 24.455 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.551 μs |   1.393 μs |  0.0764 μs |  4.568 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 21.052 μs | 324.234 μs | 17.7723 μs | 11.251 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,785.2 ns |  2,034.3 ns | 111.50 ns |  0.40 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,344.7 ns |    832.1 ns |  45.61 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,399.7 ns |  1,655.4 ns |  90.74 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,246.7 ns |  1,931.6 ns | 105.88 ns |  0.73 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    726.2 ns |    805.7 ns |  44.16 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,099.7 ns | 10,089.3 ns | 553.03 ns |  9.21 |    0.63 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,478.8 ns |  6,079.3 ns | 333.23 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,953.3 ns |  1,705.6 ns |  93.49 ns |  0.89 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.70 μs |   8.636 μs |  0.473 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   525.54 μs | 401.987 μs | 22.034 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.44 μs |   5.403 μs |  0.296 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,663.43 μs | 473.691 μs | 25.965 μs |    1280 B |


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