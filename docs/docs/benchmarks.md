---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 10:20 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,588.94 μs** |    **656.28 μs** |    **35.973 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,603.55 μs |  2,556.09 μs |   140.108 μs |  0.24 |    0.02 |       - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,694.26 μs** |  **1,460.64 μs** |    **80.062 μs** |  **1.00** |    **0.01** | **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,388.52 μs |    894.84 μs |    49.049 μs |  0.31 |    0.01 | 15.6250 |       - |  339.89 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,381.47 μs** |    **931.50 μs** |    **51.059 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,314.97 μs |  3,335.17 μs |   182.812 μs |  0.21 |    0.02 |       - |       - |    36.9 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **14,557.67 μs** | **28,673.22 μs** | **1,571.676 μs** |  **1.01** |    **0.13** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,565.43 μs |  7,408.35 μs |   406.077 μs |  0.45 |    0.05 | 15.6250 |       - |   369.5 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **147.39 μs** |     **58.88 μs** |     **3.227 μs** |  **1.00** |    **0.03** |  **2.4414** |       **-** |   **42.07 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     72.25 μs |    115.22 μs |     6.316 μs |  0.49 |    0.04 |  0.4883 |       - |    13.3 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,498.41 μs** |  **1,008.43 μs** |    **55.275 μs** |  **1.00** |    **0.04** | **23.4375** |       **-** |  **415.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    817.21 μs |  1,561.16 μs |    85.573 μs |  0.55 |    0.05 |  3.9063 |       - |   92.17 KB |        0.22 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    347.07 μs |    227.21 μs |    12.454 μs |     ? |       ? |  6.8359 |  5.8594 |  210.37 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,412.26 μs |  2,157.24 μs |   118.246 μs |     ? |       ? | 70.3125 | 62.5000 | 1956.28 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,687.02 μs** |    **144.71 μs** |     **7.932 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,268.63 μs |     73.61 μs |     4.035 μs |  0.22 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,957.98 μs** |  **7,883.09 μs** |   **432.099 μs** |  **1.00** |    **0.09** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,161.77 μs |  1,160.48 μs |    63.610 μs |  0.20 |    0.02 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,689.50 μs** |    **242.97 μs** |    **13.318 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,114.39 μs |     16.71 μs |     0.916 μs |  0.20 |    0.00 |       - |       - |    1.22 KB |        0.60 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,697.06 μs** |    **255.32 μs** |    **13.995 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,391.83 μs |    226.15 μs |    12.396 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.756 ms** | **10.457 ms** | **0.5732 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.419 ms | 17.808 ms | 0.9761 ms | 0.005 |  406.71 KB |        5.45 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.694 ms** | **12.168 ms** | **0.6670 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.273 ms | 11.272 ms | 0.6179 ms | 0.005 |  598.88 KB |        2.39 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.747 ms** |  **6.052 ms** | **0.3317 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.230 ms | 50.459 ms | 2.7658 ms | 0.005 |  809.45 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.822 ms** | **22.789 ms** | **1.2491 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    19.886 ms | 33.682 ms | 1.8462 ms | 0.006 | 2575.58 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.266 ms** | **27.623 ms** | **1.5141 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.923 ms | 17.459 ms | 0.9570 ms | 0.002 |  182.58 KB |       75.88 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.809 ms** | **31.474 ms** | **1.7252 ms** | **1.000** |   **28.21 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.612 ms | 48.964 ms | 2.6839 ms | 0.002 |   183.2 KB |        6.49 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.754 ms** | **10.372 ms** | **0.5685 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.220 ms | 11.461 ms | 0.6282 ms | 0.002 |  181.43 KB |       75.40 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.173 ms** | **19.346 ms** | **1.0604 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.417 ms | 30.604 ms | 1.6775 ms | 0.002 |  185.15 KB |       44.30 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 15.602 μs |  6.751 μs | 0.3700 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.265 μs |  2.787 μs | 0.1528 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.016 μs |  2.593 μs | 0.1421 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 28.109 μs | 32.029 μs | 1.7556 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.981 μs |  1.009 μs | 0.0553 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.384 μs |  1.393 μs | 0.0764 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.051 μs |  8.976 μs | 0.4920 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.693 μs |  2.285 μs | 0.1253 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.637 μs |  5.265 μs | 0.2886 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,286.0 ns | 3,591.6 ns | 196.87 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,368.0 ns | 2,740.0 ns | 150.19 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,416.2 ns | 2,743.9 ns | 150.40 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,092.5 ns | 1,169.2 ns |  64.09 ns |  0.74 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    888.2 ns | 2,009.6 ns | 110.15 ns |  0.21 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,296.3 ns | 6,873.9 ns | 376.78 ns |  9.36 |    0.38 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,203.3 ns | 3,603.8 ns | 197.54 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,734.2 ns | 3,815.3 ns | 209.13 ns |  0.89 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.007 μs |  22.390 μs |  1.2272 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   927.657 μs | 200.077 μs | 10.9669 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.652 μs |   4.550 μs |  0.2494 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,686.923 μs | 564.673 μs | 30.9516 μs |    1280 B |


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