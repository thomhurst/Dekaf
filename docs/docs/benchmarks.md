---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 21:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,130.13 μs** |    **234.15 μs** |  **12.834 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,403.20 μs |  2,297.11 μs | 125.912 μs |  0.23 |    0.02 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,361.78 μs** |  **2,252.09 μs** | **123.445 μs** |  **1.00** |    **0.02** | **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,381.83 μs |    823.11 μs |  45.118 μs |  0.32 |    0.01 | 15.6250 |       - |  339.56 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,247.72 μs** |    **855.48 μs** |  **46.892 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,254.86 μs |  1,886.43 μs | 103.401 μs |  0.20 |    0.01 |       - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,768.64 μs** | **13,648.31 μs** | **748.110 μs** |  **1.00** |    **0.07** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,429.37 μs |  8,255.40 μs | 452.506 μs |  0.50 |    0.04 | 15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.71 μs** |     **61.78 μs** |   **3.387 μs** |  **1.00** |    **0.03** |  **2.4414** |       **-** |   **42.14 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     73.01 μs |    147.85 μs |   8.104 μs |  0.53 |    0.05 |       - |       - |    9.57 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,411.52 μs** |    **994.95 μs** |  **54.537 μs** |  **1.00** |    **0.05** | **23.4375** |       **-** |  **412.76 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    616.88 μs |    493.64 μs |  27.058 μs |  0.44 |    0.02 |       - |       - |   65.67 KB |        0.16 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    204.21 μs |    350.16 μs |  19.194 μs |     ? |       ? |  0.9766 |       - |   101.7 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,108.63 μs |  1,756.86 μs |  96.300 μs |     ? |       ? |  7.8125 |       - | 1005.27 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,417.02 μs** |    **174.28 μs** |   **9.553 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,115.39 μs |     42.58 μs |   2.334 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,422.98 μs** |    **182.48 μs** |  **10.003 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,119.22 μs |     98.19 μs |   5.382 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,433.26 μs** |    **524.74 μs** |  **28.763 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,114.91 μs |     34.62 μs |   1.898 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,429.22 μs** |    **252.06 μs** |  **13.816 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,393.56 μs |    272.13 μs |  14.916 μs |  0.26 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.358 ms** | **35.377 ms** | **1.9392 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.131 ms | 17.737 ms | 0.9722 ms | 0.005 |  603.22 KB |        8.08 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.583 ms** | **94.240 ms** | **5.1656 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.944 ms | 49.502 ms | 2.7133 ms | 0.005 |  778.66 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.205 ms** |  **6.839 ms** | **0.3748 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.022 ms | 59.808 ms | 3.2783 ms | 0.006 |  997.58 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.686 ms** |  **3.629 ms** | **0.1989 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.171 ms | 12.138 ms | 0.6653 ms | 0.005 | 2763.79 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.566 ms** | **23.592 ms** | **1.2932 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.158 ms | 28.000 ms | 1.5348 ms | 0.002 |  195.73 KB |       81.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.728 ms** |  **3.561 ms** | **0.1952 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.913 ms | 11.504 ms | 0.6306 ms | 0.002 |   186.6 KB |       44.81 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.079 ms** | **19.788 ms** | **1.0847 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.023 ms | 19.598 ms | 1.0743 ms | 0.002 |  202.69 KB |       84.23 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.879 ms** | **27.421 ms** | **1.5031 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.554 ms |  1.030 ms | 0.0565 ms | 0.002 |  187.04 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.851 μs |   2.681 μs | 0.1469 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.352 μs |   5.752 μs | 0.3153 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.329 μs |   4.589 μs | 0.2516 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 30.628 μs | 116.203 μs | 6.3695 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.827 μs |   1.931 μs | 0.1058 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.561 μs | 103.108 μs | 5.6517 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.837 μs |   8.347 μs | 0.4575 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.820 μs |   8.198 μs | 0.4494 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.774 μs |   5.712 μs | 0.3131 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.710 μs |   2.468 μs | 0.1353 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,376.3 ns |    822.7 ns |    45.09 ns |  1,373.0 ns |  0.26 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  2,071.0 ns | 24,069.2 ns | 1,319.31 ns |  1,443.0 ns |  0.38 |    0.23 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,422.3 ns |  3,841.6 ns |   210.57 ns |  1,332.0 ns |  0.26 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,587.8 ns |  2,374.0 ns |   130.13 ns |  2,594.5 ns |  0.48 |    0.11 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    714.2 ns |    288.7 ns |    15.82 ns |    710.5 ns |  0.13 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,234.0 ns | 11,346.5 ns |   621.94 ns | 41,207.0 ns |  7.65 |    1.75 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,627.2 ns | 24,844.4 ns | 1,361.80 ns |  5,850.5 ns |  1.04 |    0.33 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,853.8 ns |  4,338.4 ns |   237.80 ns |  3,736.5 ns |  0.71 |    0.17 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.15 μs |   4.155 μs |  0.228 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   530.30 μs |  89.915 μs |  4.929 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.08 μs |   6.849 μs |  0.375 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,653.23 μs | 494.819 μs | 27.123 μs |    1280 B |


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