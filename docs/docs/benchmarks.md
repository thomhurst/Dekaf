---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 16:18 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,254.77 μs** |   **179.97 μs** |   **9.865 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,342.87 μs | 1,516.43 μs |  83.121 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,423.46 μs** |   **626.85 μs** |  **34.360 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,334.60 μs |   502.59 μs |  27.549 μs |  0.31 |    0.00 |  15.6250 |       - |  339.44 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,281.36 μs** |   **649.34 μs** |  **35.593 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,400.32 μs | 2,819.75 μs | 154.560 μs |  0.22 |    0.02 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,889.13 μs** |   **991.60 μs** |  **54.353 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,631.73 μs | 9,420.63 μs | 516.376 μs |  0.51 |    0.03 |  15.6250 |       - |  361.76 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.70 μs** |    **29.56 μs** |   **1.620 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **42.23 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     48.85 μs |    56.57 μs |   3.101 μs |  0.35 |    0.02 |        - |       - |     9.9 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,465.96 μs** |   **371.57 μs** |  **20.367 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **421.12 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    745.44 μs | 2,867.39 μs | 157.172 μs |  0.51 |    0.09 |        - |       - |   86.58 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    226.92 μs |   337.12 μs |  18.479 μs |     ? |       ? |   0.9766 |       - |  105.22 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,137.71 μs | 3,704.64 μs | 203.064 μs |     ? |       ? |   7.8125 |       - |  993.81 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,430.33 μs** |    **27.17 μs** |   **1.489 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,252.49 μs |   254.17 μs |  13.932 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,423.13 μs** |    **71.47 μs** |   **3.918 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,306.69 μs |   987.84 μs |  54.147 μs |  0.24 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,426.90 μs** |    **81.38 μs** |   **4.461 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,341.36 μs |   156.74 μs |   8.591 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,429.50 μs** |    **93.85 μs** |   **5.144 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,345.25 μs |   395.75 μs |  21.692 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.299 ms** | **31.776 ms** | **1.7418 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.691 ms | 46.639 ms | 2.5564 ms | 0.005 |  596.79 KB |        8.00 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.853 ms** | **10.969 ms** | **0.6013 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.263 ms | 37.367 ms | 2.0482 ms | 0.005 |  779.04 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.995 ms** |  **7.669 ms** | **0.4204 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.887 ms | 54.983 ms | 3.0138 ms | 0.006 |  997.27 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,168.152 ms** | **81.003 ms** | **4.4400 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.165 ms | 17.888 ms | 0.9805 ms | 0.005 | 2763.87 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.145 ms** | **17.791 ms** | **0.9752 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.978 ms |  7.212 ms | 0.3953 ms | 0.002 |   187.8 KB |       78.05 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.733 ms** |  **6.923 ms** | **0.3795 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.850 ms | 11.892 ms | 0.6518 ms | 0.002 |  196.75 KB |       47.25 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.987 ms** | **31.370 ms** | **1.7195 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.145 ms | 19.134 ms | 1.0488 ms | 0.002 |  202.77 KB |       84.27 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.581 ms** | **18.498 ms** | **1.0139 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.218 ms | 15.255 ms | 0.8362 ms | 0.002 |  187.03 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.703 μs |  1.2909 μs | 0.0708 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.923 μs |  8.7654 μs | 0.4805 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.962 μs | 17.1666 μs | 0.9410 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.024 μs |  0.9043 μs | 0.0496 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.223 μs |  2.0096 μs | 0.1102 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.184 μs | 10.8729 μs | 0.5960 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.219 μs | 13.4952 μs | 0.7397 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.870 μs | 22.9219 μs | 1.2564 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.101 μs | 10.1836 μs | 0.5582 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.623 μs | 16.4724 μs | 0.9029 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,135.3 ns |  2,161.2 ns | 118.5 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,260.7 ns |  4,833.7 ns | 265.0 ns |  0.32 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,481.3 ns |  2,295.4 ns | 125.8 ns |  0.37 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,493.8 ns |  4,763.3 ns | 261.1 ns |  0.63 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    591.0 ns |  2,687.5 ns | 147.3 ns |  0.15 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,907.3 ns |  8,119.5 ns | 445.1 ns |  9.04 |    0.74 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,995.3 ns |  6,997.6 ns | 383.6 ns |  1.01 |    0.12 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,729.0 ns | 10,618.1 ns | 582.0 ns |  0.94 |    0.15 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Median       | Allocated |
|------------------------ |-------------:|-----------:|-----------:|-------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    16.221 μs | 160.789 μs |  8.8134 μs |    11.668 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   524.970 μs | 322.634 μs | 17.6846 μs |   517.285 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.592 μs |   8.252 μs |  0.4523 μs |     8.432 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,565.525 μs | 154.311 μs |  8.4583 μs | 1,567.692 μs |    1280 B |


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