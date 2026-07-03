---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 07:52 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,398.04 μs** | **1,104.16 μs** |  **60.523 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,303.81 μs | 1,855.04 μs | 101.681 μs |  0.20 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,520.18 μs** |   **979.91 μs** |  **53.712 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,376.48 μs |   229.21 μs |  12.564 μs |  0.32 |    0.00 |  15.6250 |       - |   339.9 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,183.50 μs** |   **473.52 μs** |  **25.955 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,462.69 μs | 3,705.40 μs | 203.106 μs |  0.24 |    0.03 |        - |       - |   36.93 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,961.14 μs** | **1,093.17 μs** |  **59.920 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,756.55 μs | 6,921.55 μs | 379.393 μs |  0.52 |    0.03 |  15.6250 |       - |  369.57 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.12 μs** |    **55.53 μs** |   **3.044 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.84 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     69.14 μs |    68.49 μs |   3.754 μs |  0.49 |    0.02 |   0.4883 |       - |   17.58 KB |        0.42 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,414.89 μs** |   **584.38 μs** |  **32.032 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **421.63 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    753.65 μs | 1,719.07 μs |  94.228 μs |  0.53 |    0.06 |   3.9063 |       - |   83.89 KB |        0.20 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    331.96 μs |   406.21 μs |  22.266 μs |     ? |       ? |   6.8359 |  5.8594 |  205.19 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,283.10 μs | 5,989.98 μs | 328.331 μs |     ? |       ? |  62.5000 | 54.6875 | 1879.47 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,467.09 μs** |    **68.42 μs** |   **3.750 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,395.81 μs |   233.30 μs |  12.788 μs |  0.26 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,454.09 μs** |   **112.13 μs** |   **6.146 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,376.33 μs |   118.30 μs |   6.485 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,552.99 μs** |   **457.63 μs** |  **25.084 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,378.21 μs |   221.47 μs |  12.139 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,517.55 μs** |   **133.28 μs** |   **7.305 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,343.61 μs |   416.08 μs |  22.807 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.787 ms** | **14.902 ms** | **0.8168 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.364 ms | 66.422 ms | 3.6408 ms | 0.005 |  409.48 KB |        5.49 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.146 ms** |  **9.189 ms** | **0.5037 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.516 ms | 27.704 ms | 1.5186 ms | 0.005 |  590.58 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.749 ms** | **29.296 ms** | **1.6058 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.093 ms | 97.803 ms | 5.3609 ms | 0.006 |  846.34 KB |        1.41 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.183 ms** | **42.980 ms** | **2.3559 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.349 ms | 27.036 ms | 1.4819 ms | 0.005 | 2649.73 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.627 ms** | **22.046 ms** | **1.2084 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.290 ms | 14.698 ms | 0.8057 ms | 0.002 |  193.66 KB |       80.48 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.119 ms** | **24.634 ms** | **1.3503 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.865 ms |  6.365 ms | 0.3489 ms | 0.002 |  185.49 KB |       44.55 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.296 ms** | **57.990 ms** | **3.1786 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.887 ms |  4.059 ms | 0.2225 ms | 0.002 |  217.65 KB |       90.45 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.432 ms** | **25.555 ms** | **1.4007 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.559 ms |  9.885 ms | 0.5418 ms | 0.002 |   183.9 KB |       44.00 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 18.180 μs |  89.2537 μs |  4.8923 μs | 15.829 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 12.244 μs |   3.0217 μs |  0.1656 μs | 12.227 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.928 μs |  22.8871 μs |  1.2545 μs | 10.280 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.607 μs |   0.1110 μs |  0.0061 μs | 26.610 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 16.721 μs | 248.6748 μs | 13.6307 μs |  8.886 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.133 μs |   2.6157 μs |  0.1434 μs | 19.166 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.008 μs |  12.9284 μs |  0.7086 μs | 17.764 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.614 μs |   1.5905 μs |  0.0872 μs |  4.654 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.094 μs |  12.9559 μs |  0.7102 μs | 10.730 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,847.0 ns |   7,256.0 ns |    397.73 ns |  2,014.0 ns |  0.45 |    0.09 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,572.0 ns |   3,003.3 ns |    164.62 ns |  1,482.0 ns |  0.38 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,443.3 ns |   1,568.4 ns |     85.97 ns |  1,433.0 ns |  0.35 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,622.7 ns |   2,176.5 ns |    119.30 ns |  2,586.0 ns |  0.64 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    734.0 ns |   1,355.1 ns |     74.28 ns |    761.0 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 48,525.0 ns | 220,614.3 ns | 12,092.61 ns | 41,629.0 ns | 11.81 |    2.65 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,120.7 ns |   4,944.4 ns |    271.02 ns |  4,207.0 ns |  1.00 |    0.08 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  7,029.0 ns |  98,353.6 ns |  5,391.09 ns |  3,947.0 ns |  1.71 |    1.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.79 μs |   6.554 μs |  0.359 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   522.04 μs | 436.815 μs | 23.943 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.12 μs |   3.114 μs |  0.171 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,686.83 μs | 505.395 μs | 27.702 μs |    1280 B |


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