---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 12:45 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,650.77 μs** |  **1,367.499 μs** |  **74.957 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,385.12 μs |  1,558.377 μs |  85.420 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,645.65 μs** |  **1,643.439 μs** |  **90.082 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,386.84 μs |  1,286.876 μs |  70.538 μs |  0.31 |    0.01 |  15.6250 |       - |  339.42 KB |        0.32 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,231.41 μs** |    **310.401 μs** |  **17.014 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,231.13 μs |  2,070.720 μs | 113.503 μs |  0.20 |    0.02 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,759.75 μs** |  **3,859.369 μs** | **211.545 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,838.24 μs | 11,912.638 μs | 652.972 μs |  0.54 |    0.04 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **147.13 μs** |     **49.089 μs** |   **2.691 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.14 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     75.55 μs |    156.317 μs |   8.568 μs |  0.51 |    0.05 |        - |       - |    8.63 KB |        0.20 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,527.18 μs** |  **4,079.659 μs** | **223.620 μs** |  **1.01** |    **0.18** |  **25.3906** |       **-** |  **420.42 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    703.16 μs |  1,854.290 μs | 101.640 μs |  0.47 |    0.08 |        - |       - |  103.99 KB |        0.25 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |            **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    227.86 μs |    559.479 μs |  30.667 μs |     ? |       ? |   0.9766 |       - |   99.38 KB |           ? |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |            **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,334.24 μs |  6,558.844 μs | 359.512 μs |     ? |       ? |   7.8125 |       - | 1017.57 KB |           ? |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,550.50 μs** |    **301.312 μs** |  **16.516 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,119.92 μs |     59.523 μs |   3.263 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,536.63 μs** |    **249.999 μs** |  **13.703 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,117.83 μs |      7.247 μs |   0.397 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,556.91 μs** |    **377.486 μs** |  **20.691 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,120.61 μs |     70.523 μs |   3.866 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |               |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,538.23 μs** |    **250.937 μs** |  **13.755 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.06 μs |    112.004 μs |   6.139 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.356 ms** |  **11.861 ms** |  **0.6501 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    20.491 ms |  11.764 ms |  0.6448 ms | 0.006 |  592.26 KB |        7.94 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.969 ms** |  **26.634 ms** |  **1.4599 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.465 ms |   9.697 ms |  0.5315 ms | 0.006 |  776.13 KB |        3.10 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.193 ms** |   **7.102 ms** |  **0.3893 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.619 ms |  24.197 ms |  1.3263 ms | 0.006 |  994.51 KB |        1.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.655 ms** |  **24.210 ms** |  **1.3270 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.530 ms |  25.017 ms |  1.3713 ms | 0.006 | 2761.67 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,140.400 ms** | **534.090 ms** | **29.2753 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.942 ms |  37.795 ms |  2.0717 ms | 0.003 |  184.45 KB |       76.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,158.384 ms** |  **30.257 ms** |  **1.6585 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.020 ms |  13.924 ms |  0.7632 ms | 0.002 |   186.5 KB |       44.79 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.474 ms** |  **34.549 ms** |  **1.8938 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     9.635 ms |  45.517 ms |  2.4949 ms | 0.003 |     186 KB |       77.30 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.840 ms** |  **25.194 ms** |  **1.3810 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.403 ms |  12.952 ms |  0.7099 ms | 0.002 |  188.38 KB |       45.07 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 12.248 μs |  2.061 μs | 0.1130 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  7.659 μs |  2.317 μs | 0.1270 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.921 μs | 18.050 μs | 0.9894 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.268 μs |  4.936 μs | 0.2706 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  7.014 μs |  2.360 μs | 0.1294 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 16.443 μs |  1.005 μs | 0.0551 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 14.779 μs | 35.401 μs | 1.9404 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 15.617 μs |  9.575 μs | 0.5248 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.332 μs |  7.544 μs | 0.4135 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.526 μs |  5.262 μs | 0.2884 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |    998.7 ns | 2,229.4 ns | 122.20 ns |  0.34 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |    978.7 ns | 4,532.9 ns | 248.46 ns |  0.33 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,132.0 ns | 2,527.9 ns | 138.56 ns |  0.38 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  1,849.7 ns | 5,215.7 ns | 285.89 ns |  0.63 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    528.0 ns | 1,058.6 ns |  58.03 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 27,786.5 ns | 3,883.0 ns | 212.84 ns |  9.42 |    0.77 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  2,967.8 ns | 5,355.2 ns | 293.54 ns |  1.01 |    0.12 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,282.0 ns | 4,630.0 ns | 253.79 ns |  1.11 |    0.12 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.026 μs |   7.126 μs |  0.3906 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   611.191 μs | 494.810 μs | 27.1222 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     7.194 μs |  22.227 μs |  1.2183 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,890.561 μs | 897.793 μs | 49.2110 μs |    1280 B |


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