---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 11:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,096.43 μs** |   **590.33 μs** |  **32.358 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,318.59 μs | 1,475.07 μs |  80.854 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,292.36 μs** | **1,212.50 μs** |  **66.461 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,339.30 μs | 1,073.19 μs |  58.825 μs |  0.32 |    0.01 |  15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,562.70 μs** |   **505.44 μs** |  **27.705 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,285.83 μs | 2,431.08 μs | 133.256 μs |  0.20 |    0.02 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,941.56 μs** | **1,068.26 μs** |  **58.555 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,848.68 μs | 1,550.93 μs |  85.012 μs |  0.49 |    0.01 |  15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.86 μs** |    **23.72 μs** |   **1.300 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **42.06 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     74.18 μs |   118.24 μs |   6.481 μs |  0.54 |    0.04 |        - |       - |   10.19 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    696.50 μs | 1,162.55 μs |  63.723 μs |     ? |       ? |        - |       - |   55.24 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    208.72 μs |   404.54 μs |  22.174 μs |     ? |       ? |   0.9766 |       - |  100.34 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,088.78 μs | 1,949.88 μs | 106.879 μs |     ? |       ? |   7.8125 |       - |   988.5 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,400.46 μs** |    **72.29 μs** |   **3.962 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,368.83 μs |   162.66 μs |   8.916 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,399.43 μs** |    **28.23 μs** |   **1.547 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,352.84 μs |   130.24 μs |   7.139 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,406.49 μs** |    **35.77 μs** |   **1.961 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,328.99 μs |   797.90 μs |  43.736 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,399.73 μs** |    **17.14 μs** |   **0.939 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,286.03 μs |   330.29 μs |  18.105 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.130 ms** | **10.256 ms** | **0.5622 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.636 ms | 38.633 ms | 2.1176 ms | 0.005 |  593.17 KB |        7.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.073 ms** |  **9.831 ms** | **0.5389 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.126 ms | 27.622 ms | 1.5141 ms | 0.004 |  776.52 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.311 ms** |  **6.174 ms** | **0.3384 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.259 ms | 47.814 ms | 2.6208 ms | 0.005 |  995.88 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163.199 ms** | **23.619 ms** | **1.2946 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.095 ms | 16.184 ms | 0.8871 ms | 0.005 | 2763.51 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.035 ms** | **22.456 ms** | **1.2309 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.794 ms |  6.767 ms | 0.3709 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.279 ms** | **15.140 ms** | **0.8299 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.838 ms | 11.696 ms | 0.6411 ms | 0.002 |   187.8 KB |       45.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.615 ms** | **13.665 ms** | **0.7490 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.256 ms | 21.236 ms | 1.1640 ms | 0.002 |  267.16 KB |      111.03 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.944 ms** | **29.017 ms** | **1.5905 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.587 ms | 21.787 ms | 1.1942 ms | 0.002 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.607 μs |  0.8360 μs | 0.0458 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.361 μs | 29.2884 μs | 1.6054 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.015 μs |  4.0131 μs | 0.2200 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.360 μs | 85.1876 μs | 4.6694 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.889 μs |  2.2811 μs | 0.1250 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.404 μs | 69.7611 μs | 3.8238 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.838 μs |  5.8066 μs | 0.3183 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.950 μs | 11.8311 μs | 0.6485 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.438 μs |  4.7776 μs | 0.2619 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.209 μs | 24.2163 μs | 1.3274 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,385.3 ns | 3,076.3 ns | 168.62 ns |  0.36 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,413.3 ns | 1,766.0 ns |  96.80 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,512.0 ns | 2,832.2 ns | 155.24 ns |  0.39 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,514.3 ns |   486.2 ns |  26.65 ns |  0.64 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    767.0 ns |   965.4 ns |  52.92 ns |  0.20 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,116.0 ns | 8,838.1 ns | 484.45 ns | 10.02 |    0.34 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,905.8 ns | 2,689.5 ns | 147.42 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,883.8 ns | 2,848.6 ns | 156.14 ns |  1.00 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev     | Allocated |
|------------------------ |-------------:|------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.001 μs |   0.9654 μs |  0.0529 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   566.360 μs | 922.2256 μs | 50.5503 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.946 μs |  10.6480 μs |  0.5837 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,629.721 μs | 141.6378 μs |  7.7636 μs |    1280 B |


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