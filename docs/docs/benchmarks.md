---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 15:15 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,174.14 μs** |   **273.81 μs** |  **15.009 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,327.72 μs | 1,254.69 μs |  68.774 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,597.45 μs** | **2,135.55 μs** | **117.057 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,268.30 μs |   785.98 μs |  43.082 μs |  0.30 |    0.01 |  19.5313 |  3.9063 |  339.35 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,362.80 μs** |   **232.73 μs** |  **12.757 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,257.17 μs | 1,708.48 μs |  93.648 μs |  0.20 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,521.94 μs** | **5,930.03 μs** | **325.045 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,952.75 μs | 8,370.98 μs | 458.842 μs |  0.56 |    0.03 |  15.6250 |       - |  361.65 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **148.32 μs** |    **33.64 μs** |   **1.844 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.74 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     90.85 μs |   271.85 μs |  14.901 μs |  0.61 |    0.09 |        - |       - |    7.09 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,470.11 μs** |   **276.80 μs** |  **15.172 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **421.71 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    694.55 μs |   362.50 μs |  19.870 μs |  0.47 |    0.01 |        - |       - |  185.01 KB |        0.44 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    209.83 μs |   185.78 μs |  10.183 μs |     ? |       ? |   0.4883 |       - |    12.2 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,988.00 μs |   262.94 μs |  14.413 μs |     ? |       ? |   7.8125 |       - | 1040.09 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,427.00 μs** |   **220.30 μs** |  **12.075 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,374.42 μs |   495.23 μs |  27.145 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,418.83 μs** |    **89.94 μs** |   **4.930 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,390.69 μs |   346.23 μs |  18.978 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,421.14 μs** |   **161.74 μs** |   **8.866 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,349.70 μs |   561.36 μs |  30.770 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,425.15 μs** |   **225.61 μs** |  **12.367 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,353.01 μs |   117.60 μs |   6.446 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.620 ms** |   **3.065 ms** |  **0.1680 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    19.256 ms |  44.381 ms |  2.4327 ms | 0.006 |  593.38 KB |        7.95 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.414 ms** |  **13.210 ms** |  **0.7241 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.476 ms |  13.972 ms |  0.7658 ms | 0.005 |  774.58 KB |        3.09 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.801 ms** |   **6.215 ms** |  **0.3407 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.017 ms |  75.067 ms |  4.1147 ms | 0.006 |  993.76 KB |        1.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.420 ms** |   **4.372 ms** |  **0.2396 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.339 ms |  41.617 ms |  2.2812 ms | 0.005 | 2761.06 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.893 ms** |  **19.438 ms** |  **1.0655 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.355 ms |   6.863 ms |  0.3762 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,141.744 ms** | **542.839 ms** | **29.7549 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.436 ms |   2.180 ms |  0.1195 ms | 0.002 |  195.38 KB |       46.92 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.236 ms** |  **24.058 ms** |  **1.3187 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.719 ms |  11.997 ms |  0.6576 ms | 0.002 |  193.68 KB |       80.49 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.561 ms** |  **16.445 ms** |  **0.9014 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.591 ms |  23.386 ms |  1.2819 ms | 0.002 |  187.09 KB |       44.76 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.828 μs |  2.7547 μs | 0.1510 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.263 μs |  3.1173 μs | 0.1709 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.396 μs |  4.0296 μs | 0.2209 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.764 μs |  1.5448 μs | 0.0847 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.957 μs |  0.8460 μs | 0.0464 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.180 μs | 26.0099 μs | 1.4257 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.933 μs |  7.4232 μs | 0.4069 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.260 μs | 53.7847 μs | 2.9481 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.555 μs |  1.2943 μs | 0.0709 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.664 μs |  4.2684 μs | 0.2340 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,791.5 ns |     649.4 ns |     35.59 ns |  0.42 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,252.0 ns |   1,424.9 ns |     78.10 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,338.3 ns |   1,458.4 ns |     79.94 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,754.3 ns |   6,330.3 ns |    346.99 ns |  0.65 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    764.7 ns |     822.0 ns |     45.06 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 45,715.5 ns | 188,370.8 ns | 10,325.24 ns | 10.83 |    2.12 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,223.2 ns |   1,287.5 ns |     70.57 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,024.0 ns |   1,355.1 ns |     74.28 ns |  0.95 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.210 μs |   4.866 μs |  0.2667 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   535.437 μs | 414.300 μs | 22.7092 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.985 μs |   4.303 μs |  0.2359 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,664.381 μs | 461.144 μs | 25.2768 μs |    1280 B |


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