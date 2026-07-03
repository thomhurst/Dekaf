---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 09:38 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,084.98 μs** |    **440.36 μs** |    **24.138 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,348.94 μs |    555.66 μs |    30.457 μs |  0.22 |    0.00 |       - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,201.00 μs** |    **887.77 μs** |    **48.662 μs** |  **1.00** |    **0.01** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,323.31 μs |    435.62 μs |    23.878 μs |  0.32 |    0.00 | 15.6250 |       - |  339.84 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,660.52 μs** |    **107.68 μs** |     **5.902 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,142.12 μs |    122.83 μs |     6.733 μs |  0.17 |    0.00 |       - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,416.90 μs** | **45,662.12 μs** | **2,502.895 μs** |  **1.02** |    **0.22** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,901.59 μs |  2,542.71 μs |   139.375 μs |  0.45 |    0.07 | 15.6250 |       - |  369.46 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **130.68 μs** |     **19.22 μs** |     **1.053 μs** |  **1.00** |    **0.01** |  **2.4414** |       **-** |   **41.75 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.74 μs |     58.98 μs |     3.233 μs |  0.50 |    0.02 |  0.4883 |  0.2441 |   17.37 KB |        0.42 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,231.75 μs** |    **413.81 μs** |    **22.682 μs** |  **1.00** |    **0.02** | **19.5313** |       **-** |  **375.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    615.88 μs |  1,946.17 μs |   106.676 μs |  0.50 |    0.08 |  5.8594 |  3.9063 |  135.77 KB |        0.36 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    343.48 μs |    430.51 μs |    23.598 μs |     ? |       ? |  5.8594 |  4.8828 |  186.92 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,156.43 μs |  2,314.41 μs |   126.861 μs |     ? |       ? | 62.5000 | 54.6875 | 1926.83 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,417.38 μs** |    **287.89 μs** |    **15.780 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,342.47 μs |    424.08 μs |    23.245 μs |  0.25 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,425.41 μs** |    **106.69 μs** |     **5.848 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,365.92 μs |    272.71 μs |    14.948 μs |  0.25 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,461.57 μs** |    **131.66 μs** |     **7.217 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,102.98 μs |     52.10 μs |     2.856 μs |  0.20 |    0.00 |       - |       - |    1.22 KB |        0.60 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,438.79 μs** |    **244.12 μs** |    **13.381 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,106.27 μs |     90.61 μs |     4.967 μs |  0.20 |    0.00 |       - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171.499 ms** | **49.188 ms** | **2.6962 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.613 ms | 47.658 ms | 2.6123 ms | 0.005 |  408.34 KB |        5.47 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,169.299 ms** | **86.029 ms** | **4.7156 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.511 ms | 23.731 ms | 1.3008 ms | 0.005 |  593.45 KB |        2.37 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.692 ms** |  **9.856 ms** | **0.5402 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.525 ms | 76.760 ms | 4.2075 ms | 0.005 |  814.52 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.278 ms** | **16.808 ms** | **0.9213 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.057 ms | 11.380 ms | 0.6238 ms | 0.005 | 2575.95 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.651 ms** | **21.956 ms** | **1.2035 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.461 ms | 26.439 ms | 1.4492 ms | 0.002 |  181.28 KB |       75.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.018 ms** | **21.738 ms** | **1.1915 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.240 ms | 16.837 ms | 0.9229 ms | 0.002 |  183.16 KB |       43.98 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.219 ms** | **20.293 ms** | **1.1123 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.640 ms | 47.620 ms | 2.6102 ms | 0.003 |  187.52 KB |       77.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.661 ms** | **14.318 ms** | **0.7848 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.979 ms |  1.131 ms | 0.0620 ms | 0.002 |  183.87 KB |       43.99 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.560 μs |   1.030 μs | 0.0565 μs | 14.577 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.340 μs |  33.737 μs | 1.8492 μs |  9.338 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.075 μs |   4.172 μs | 0.2287 μs |  9.948 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.763 μs |   1.594 μs | 0.0874 μs | 26.739 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  9.248 μs |   1.585 μs | 0.0869 μs |  9.198 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.300 μs |  62.840 μs | 3.4445 μs | 19.316 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 23.881 μs | 177.377 μs | 9.7226 μs | 18.484 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.534 μs |   2.339 μs | 0.1282 μs |  4.588 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.473 μs |  33.954 μs | 1.8612 μs | 10.475 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,820.3 ns |   582.8 ns |  31.94 ns |  0.44 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,312.0 ns |   729.7 ns |  40.00 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,255.2 ns | 2,052.7 ns | 112.52 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,618.0 ns | 1,742.9 ns |  95.54 ns |  0.63 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    780.3 ns |   557.4 ns |  30.55 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,958.7 ns | 8,364.7 ns | 458.50 ns |  9.84 |    0.11 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,161.3 ns |   459.1 ns |  25.17 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,894.0 ns | 2,291.5 ns | 125.61 ns |  0.94 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.211 μs |   2.979 μs |  0.1633 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   505.192 μs |  59.151 μs |  3.2423 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.748 μs |  17.779 μs |  0.9745 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,780.973 μs | 305.904 μs | 16.7676 μs |    1280 B |


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