---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 07:10 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,262.50 μs** |    **259.38 μs** |  **14.217 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,380.32 μs |  1,185.27 μs |  64.968 μs |  0.22 |    0.01 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,448.16 μs** |  **1,603.06 μs** |  **87.869 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,318.78 μs |    577.33 μs |  31.645 μs |  0.31 |    0.00 |  15.6250 |       - |  339.73 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,690.39 μs** | **15,924.74 μs** | **872.889 μs** |  **1.01** |    **0.16** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,351.95 μs |  1,392.76 μs |  76.342 μs |  0.20 |    0.02 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,596.48 μs** |  **3,813.38 μs** | **209.024 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,932.04 μs |  7,555.84 μs | 414.161 μs |  0.55 |    0.03 |  15.6250 |       - |  369.45 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.65 μs** |     **45.31 μs** |   **2.484 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.92 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     69.35 μs |    140.30 μs |   7.690 μs |  0.49 |    0.05 |   0.4883 |       - |   15.37 KB |        0.37 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,402.28 μs** |  **1,274.33 μs** |  **69.851 μs** |  **1.00** |    **0.06** |  **23.4375** |       **-** |  **411.36 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    696.72 μs |    332.36 μs |  18.218 μs |  0.50 |    0.02 |   3.9063 |       - |  117.77 KB |        0.29 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    317.79 μs |    180.61 μs |   9.900 μs |     ? |       ? |   6.8359 |  5.8594 |  206.12 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,454.58 μs |  8,926.51 μs | 489.292 μs |     ? |       ? |  62.5000 | 54.6875 |  1942.9 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,490.56 μs** |    **240.00 μs** |  **13.155 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,343.99 μs |    409.66 μs |  22.455 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,473.87 μs** |    **113.14 μs** |   **6.202 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,343.81 μs |    569.21 μs |  31.201 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,476.47 μs** |     **86.01 μs** |   **4.715 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,324.11 μs |    525.67 μs |  28.814 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,522.14 μs** |    **846.73 μs** |  **46.412 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,270.69 μs |    442.57 μs |  24.259 μs |  0.23 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.080 ms** | **19.883 ms** | **1.0899 ms** | **3,170.036 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.918 ms | 58.770 ms | 3.2214 ms |    16.902 ms | 0.005 |  406.98 KB |        5.45 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.869 ms** | **17.069 ms** | **0.9356 ms** | **3,166.854 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.164 ms | 33.272 ms | 1.8238 ms |    14.457 ms | 0.004 |  601.12 KB |        2.40 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.030 ms** | **36.298 ms** | **1.9896 ms** | **3,166.478 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.556 ms | 71.171 ms | 3.9011 ms |    19.616 ms | 0.006 |  881.66 KB |        1.46 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.160 ms** |  **8.425 ms** | **0.4618 ms** | **3,166.018 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.389 ms | 38.370 ms | 2.1032 ms |    14.381 ms | 0.005 | 2576.16 KB |        1.09 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.527 ms** | **39.869 ms** | **2.1854 ms** | **3,156.920 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.347 ms | 31.120 ms | 1.7058 ms |     5.538 ms | 0.002 |  182.55 KB |       75.87 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.693 ms** | **33.175 ms** | **1.8185 ms** | **3,156.136 ms** | **1.000** |    **4.77 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.034 ms |  1.520 ms | 0.0833 ms |     6.060 ms | 0.002 |  184.34 KB |       38.62 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.794 ms** | **40.012 ms** | **2.1932 ms** | **3,156.337 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.665 ms | 75.762 ms | 4.1528 ms |     6.525 ms | 0.003 |  190.59 KB |       79.20 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.232 ms** | **34.675 ms** | **1.9006 ms** | **3,156.314 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.350 ms | 17.724 ms | 0.9715 ms |     7.677 ms | 0.002 |     184 KB |       44.02 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.741 μs |  1.8274 μs | 0.1002 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.531 μs |  3.6642 μs | 0.2008 μs |         - |
| &#39;Write 100 CompactStrings&#39;                |  9.988 μs |  2.4619 μs | 0.1349 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.884 μs |  3.5964 μs | 0.1971 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.867 μs |  0.7297 μs | 0.0400 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.307 μs |  3.2533 μs | 0.1783 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 19.901 μs | 61.7014 μs | 3.3821 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.458 μs |  2.4010 μs | 0.1316 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.582 μs |  4.8439 μs | 0.2655 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  2,128.0 ns | 25,280.7 ns | 1,385.72 ns |  1,343.0 ns |  0.53 |    0.30 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,346.3 ns |  2,381.0 ns |   130.51 ns |  1,333.0 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,575.7 ns |  3,113.2 ns |   170.65 ns |  1,492.0 ns |  0.39 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,497.0 ns | 31,986.6 ns | 1,753.29 ns |  2,525.0 ns |  0.87 |    0.38 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    869.8 ns |    278.7 ns |    15.28 ns |    866.5 ns |  0.22 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,496.3 ns | 11,118.2 ns |   609.43 ns | 40,797.0 ns | 10.13 |    0.33 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,001.3 ns |  2,516.9 ns |   137.96 ns |  3,948.0 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,025.8 ns |  1,069.0 ns |    58.59 ns |  4,002.5 ns |  1.01 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.238 μs |   5.996 μs |  0.3286 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   532.128 μs | 339.670 μs | 18.6184 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.477 μs |  20.926 μs |  1.1470 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,691.620 μs | 880.503 μs | 48.2633 μs |    1280 B |


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