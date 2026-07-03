---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 18:54 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,067.13 μs** |    **194.06 μs** |    **10.637 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,359.38 μs |  1,767.22 μs |    96.867 μs |  0.22 |    0.01 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,337.11 μs** |    **966.50 μs** |    **52.977 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,208.57 μs |    115.97 μs |     6.356 μs |  0.30 |    0.00 | 11.7188 |       - |  339.49 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **7,246.16 μs** | **20,736.94 μs** | **1,136.662 μs** |  **1.02** |    **0.19** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,122.30 μs |    372.18 μs |    20.400 μs |  0.16 |    0.02 |       - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,714.88 μs** |  **1,057.30 μs** |    **57.954 μs** |  **1.00** |    **0.01** | **78.1250** | **46.8750** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,388.49 μs |    885.90 μs |    48.559 μs |  0.50 |    0.01 |  7.8125 |       - |  361.53 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **122.23 μs** |     **51.54 μs** |     **2.825 μs** |  **1.00** |    **0.03** |  **1.4648** |       **-** |   **38.22 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    55.31 μs |     41.16 μs |     2.256 μs |  0.45 |    0.02 |       - |       - |    5.87 KB |        0.15 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,213.42 μs** |    **337.99 μs** |    **18.526 μs** |  **1.00** |    **0.02** | **15.6250** |       **-** |  **421.69 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   651.49 μs |    845.03 μs |    46.319 μs |  0.54 |    0.03 |       - |       - |   41.92 KB |        0.10 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   183.97 μs |    339.99 μs |    18.636 μs |     ? |       ? |       - |       - |   11.56 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,732.84 μs |  1,678.24 μs |    91.990 μs |     ? |       ? |       - |       - |  932.56 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,387.67 μs** |    **156.53 μs** |     **8.580 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,310.50 μs |    292.61 μs |    16.039 μs |  0.24 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,397.90 μs** |    **133.23 μs** |     **7.303 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,367.13 μs |  2,253.64 μs |   123.530 μs |  0.25 |    0.02 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,386.60 μs** |    **222.33 μs** |    **12.186 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,295.14 μs |    197.29 μs |    10.814 μs |  0.24 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,405.87 μs** |    **364.90 μs** |    **20.001 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,311.58 μs |     90.15 μs |     4.941 μs |  0.24 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.713 ms** |  **19.474 ms** | **1.0674 ms** | **3,169.127 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.753 ms |  40.206 ms | 2.2038 ms |    14.770 ms | 0.005 |  590.36 KB |        7.91 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.992 ms** |  **29.380 ms** | **1.6104 ms** | **3,165.345 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.501 ms |  43.557 ms | 2.3875 ms |    16.345 ms | 0.005 |  772.38 KB |        3.08 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.988 ms** |   **1.344 ms** | **0.0737 ms** | **3,166.012 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.677 ms |  43.079 ms | 2.3613 ms |    14.330 ms | 0.005 |  992.61 KB |        1.65 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.913 ms** |  **17.132 ms** | **0.9391 ms** | **3,166.428 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.878 ms |  58.118 ms | 3.1857 ms |    15.480 ms | 0.005 | 2758.37 KB |        1.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.823 ms** |  **40.326 ms** | **2.2104 ms** | **3,155.654 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     8.643 ms | 106.987 ms | 5.8643 ms |     5.314 ms | 0.003 |  182.24 KB |       75.74 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.751 ms** |  **11.617 ms** | **0.6368 ms** | **3,156.686 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.052 ms |   1.043 ms | 0.0572 ms |     5.073 ms | 0.002 |   184.7 KB |       44.35 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.497 ms** |   **8.765 ms** | **0.4804 ms** | **3,157.394 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.594 ms |  14.153 ms | 0.7758 ms |     6.630 ms | 0.002 |  272.48 KB |      113.24 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.707 ms** |  **33.041 ms** | **1.8111 ms** | **3,158.301 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.004 ms |  16.216 ms | 0.8888 ms |     5.832 ms | 0.002 |   184.8 KB |       44.21 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.480 μs |   3.860 μs |  0.2116 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.090 μs |  20.534 μs |  1.1255 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.613 μs |   5.438 μs |  0.2981 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 40.055 μs | 239.212 μs | 13.1120 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.047 μs |   2.718 μs |  0.1490 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.209 μs |   9.904 μs |  0.5429 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.788 μs |  19.062 μs |  1.0448 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.937 μs |  20.131 μs |  1.1034 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.712 μs |   5.211 μs |  0.2856 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.529 μs |  19.832 μs |  1.0871 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,225.3 ns | 2,562.8 ns | 140.48 ns |  0.28 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,243.5 ns | 2,294.4 ns | 125.77 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,316.8 ns | 2,516.1 ns | 137.91 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,660.7 ns | 2,851.7 ns | 156.31 ns |  0.62 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    680.8 ns |   846.0 ns |  46.37 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,870.3 ns | 9,268.1 ns | 508.02 ns |  8.33 |    0.28 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,312.0 ns | 2,832.2 ns | 155.24 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,976.3 ns | 6,424.3 ns | 352.14 ns |  0.92 |    0.08 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.889 μs |  21.950 μs |  1.2032 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   515.802 μs | 290.465 μs | 15.9214 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.449 μs |   7.450 μs |  0.4084 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,569.434 μs | 403.580 μs | 22.1216 μs |    1280 B |


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