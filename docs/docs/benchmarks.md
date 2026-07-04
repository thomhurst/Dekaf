---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 04:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,387.04 μs** | **1,382.52 μs** |  **75.781 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,512.53 μs | 1,262.69 μs |  69.212 μs |  0.24 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,507.57 μs** | **1,342.73 μs** |  **73.600 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,352.57 μs |   165.15 μs |   9.053 μs |  0.31 |    0.00 |  15.6250 |       - |  339.55 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,175.74 μs** |   **709.33 μs** |  **38.881 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,560.07 μs |   771.62 μs |  42.295 μs |  0.25 |    0.01 |        - |       - |   36.35 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,938.60 μs** | **1,541.05 μs** |  **84.470 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,606.33 μs | 5,298.28 μs | 290.416 μs |  0.51 |    0.02 |  15.6250 |       - |  361.69 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **156.91 μs** |   **142.27 μs** |   **7.799 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |   **41.85 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     50.56 μs |    85.85 μs |   4.706 μs |  0.32 |    0.03 |        - |       - |   10.49 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,432.98 μs** |   **879.62 μs** |  **48.215 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **423.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    699.60 μs |   663.43 μs |  36.365 μs |  0.49 |    0.03 |        - |       - |   95.53 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    213.03 μs |    86.52 μs |   4.743 μs |     ? |       ? |   0.9766 |       - |  101.19 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,043.87 μs |   307.23 μs |  16.840 μs |     ? |       ? |   7.8125 |       - |  909.47 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,455.60 μs** |   **307.39 μs** |  **16.849 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,307.52 μs |   687.52 μs |  37.685 μs |  0.24 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,476.00 μs** |    **78.26 μs** |   **4.290 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,357.66 μs |   308.71 μs |  16.921 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,466.99 μs** |    **49.21 μs** |   **2.697 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,363.27 μs |   142.17 μs |   7.793 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,464.42 μs** |   **234.58 μs** |  **12.858 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,369.92 μs |   457.27 μs |  25.065 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.708 ms** | **29.877 ms** | **1.6377 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.955 ms | 37.898 ms | 2.0773 ms | 0.006 |  607.21 KB |        8.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.860 ms** | **21.919 ms** | **1.2015 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.342 ms | 36.413 ms | 1.9959 ms | 0.005 |  784.98 KB |        3.13 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.918 ms** | **10.429 ms** | **0.5717 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.193 ms | 61.208 ms | 3.3550 ms | 0.006 |  995.47 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.471 ms** | **19.724 ms** | **1.0812 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.288 ms | 43.893 ms | 2.4059 ms | 0.005 | 2760.51 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.321 ms** | **47.622 ms** | **2.6103 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.537 ms |  7.318 ms | 0.4011 ms | 0.002 |  186.62 KB |       77.56 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.174 ms** | **35.449 ms** | **1.9431 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.057 ms | 27.602 ms | 1.5129 ms | 0.002 |  204.55 KB |       49.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,158.400 ms** | **16.245 ms** | **0.8905 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.223 ms |  3.734 ms | 0.2047 ms | 0.002 |  256.59 KB |      106.63 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.557 ms** | **19.972 ms** | **1.0947 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.270 ms | 22.056 ms | 1.2089 ms | 0.002 |  188.41 KB |       45.08 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.952 μs |  3.4993 μs | 0.1918 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.367 μs |  3.1815 μs | 0.1744 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.463 μs |  6.1336 μs | 0.3362 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.814 μs |  1.0997 μs | 0.0603 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.234 μs |  0.9362 μs | 0.0513 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.299 μs |  3.8790 μs | 0.2126 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.830 μs |  8.1053 μs | 0.4443 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.396 μs |  6.5699 μs | 0.3601 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.519 μs |  2.7463 μs | 0.1505 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.710 μs | 17.6912 μs | 0.9697 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,269.7 ns |     690.7 ns |     37.86 ns |  0.27 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,801.8 ns |  10,313.3 ns |    565.31 ns |  0.38 |    0.13 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,499.5 ns |     762.5 ns |     41.80 ns |  0.31 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,845.3 ns |   5,541.1 ns |    303.73 ns |  0.60 |    0.14 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    755.3 ns |     105.3 ns |      5.77 ns |  0.16 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46,843.8 ns | 203,150.6 ns | 11,135.37 ns |  9.80 |    2.97 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,029.7 ns |  27,077.4 ns |  1,484.20 ns |  1.05 |    0.36 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,061.0 ns |   3,070.5 ns |    168.31 ns |  0.85 |    0.19 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev     | Allocated |
|------------------------ |------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.63 μs |     7.852 μs |   0.430 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   554.07 μs |   200.625 μs |  10.997 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.13 μs |     4.323 μs |   0.237 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,831.84 μs | 3,590.625 μs | 196.814 μs |    1280 B |


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