---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 20:09 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,193.63 μs** |   **458.93 μs** |  **25.156 μs** |  **6,184.94 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,347.22 μs | 3,139.59 μs | 172.092 μs |  1,305.07 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,382.44 μs** |   **955.42 μs** |  **52.370 μs** |  **7,369.41 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,382.57 μs |   604.54 μs |  33.137 μs |  2,393.90 μs |  0.32 |    0.00 |  15.6250 |       - |  339.68 KB |        0.32 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,232.78 μs** |   **718.66 μs** |  **39.392 μs** |  **6,214.03 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,253.43 μs | 1,742.33 μs |  95.503 μs |  1,231.89 μs |  0.20 |    0.01 |        - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,755.42 μs** | **4,133.34 μs** | **226.563 μs** | **12,845.18 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,356.23 μs | 1,971.87 μs | 108.085 μs |  6,316.64 μs |  0.50 |    0.01 |  15.6250 |       - |  361.67 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.29 μs** |    **89.91 μs** |   **4.928 μs** |    **139.56 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **41.53 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     81.61 μs |   593.66 μs |  32.540 μs |     63.46 μs |  0.58 |    0.20 |        - |       - |     6.7 KB |        0.16 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,370.22 μs** |   **645.62 μs** |  **35.388 μs** |  **1,381.21 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |  **415.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    724.98 μs | 1,074.78 μs |  58.912 μs |    703.30 μs |  0.53 |    0.04 |        - |       - |   96.77 KB |        0.23 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    212.95 μs |   176.92 μs |   9.698 μs |    214.87 μs |     ? |       ? |        - |       - |   93.52 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,249.73 μs | 3,280.43 μs | 179.812 μs |  2,238.43 μs |     ? |       ? |   7.8125 |       - |  988.74 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,421.03 μs** |    **46.88 μs** |   **2.570 μs** |  **5,420.13 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,115.34 μs |    38.38 μs |   2.104 μs |  1,114.57 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,416.98 μs** |   **111.34 μs** |   **6.103 μs** |  **5,415.55 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,119.23 μs |    89.53 μs |   4.907 μs |  1,117.07 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,422.77 μs** |    **40.57 μs** |   **2.224 μs** |  **5,421.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,114.65 μs |    11.05 μs |   0.605 μs |  1,114.93 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,422.20 μs** |    **40.74 μs** |   **2.233 μs** |  **5,421.91 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,115.98 μs |    14.14 μs |   0.775 μs |  1,116.32 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.861 ms** |  **25.305 ms** |  **1.3870 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.949 ms |  23.156 ms |  1.2693 ms | 0.005 |  597.26 KB |        8.00 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.316 ms** |   **8.001 ms** |  **0.4386 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.798 ms |  12.034 ms |  0.6596 ms | 0.004 |   779.3 KB |        3.11 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.923 ms** |   **8.501 ms** |  **0.4660 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.765 ms |  48.115 ms |  2.6373 ms | 0.006 |  997.55 KB |        1.66 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.342 ms** |   **8.012 ms** |  **0.4392 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.270 ms |  37.144 ms |  2.0360 ms | 0.005 | 2765.41 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,138.497 ms** | **541.540 ms** | **29.6836 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.304 ms |   2.578 ms |  0.1413 ms | 0.002 |  195.99 KB |       81.45 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.079 ms** |   **6.523 ms** |  **0.3576 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.382 ms |   8.684 ms |  0.4760 ms | 0.002 |  199.97 KB |       48.02 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.364 ms** |  **21.092 ms** |  **1.1561 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.230 ms |  20.369 ms |  1.1165 ms | 0.002 |  186.17 KB |       77.37 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.555 ms** |  **33.266 ms** |  **1.8234 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.904 ms |  33.077 ms |  1.8131 ms | 0.002 |  261.48 KB |       62.56 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.951 μs | 253.6306 μs | 13.9024 μs | 25.960 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.859 μs |  28.8834 μs |  1.5832 μs | 12.614 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.357 μs |   4.5071 μs |  0.2470 μs | 10.320 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 31.205 μs | 141.1097 μs |  7.7347 μs | 26.870 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.980 μs |   1.1068 μs |  0.0607 μs |  8.966 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 29.212 μs | 275.6386 μs | 15.1087 μs | 20.488 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.169 μs |  10.7907 μs |  0.5915 μs | 17.142 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.843 μs |   4.7041 μs |  0.2578 μs | 24.986 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.614 μs |   0.7952 μs |  0.0436 μs |  4.593 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.790 μs |   2.2418 μs |  0.1229 μs | 10.840 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,245.8 ns |   640.7 ns |  35.12 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,270.8 ns |   379.8 ns |  20.82 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,425.3 ns | 2,490.3 ns | 136.50 ns |  0.37 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,461.5 ns | 1,296.1 ns |  71.04 ns |  0.63 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    672.0 ns |   547.3 ns |  30.00 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,317.0 ns | 2,150.9 ns | 117.90 ns | 10.61 |    0.50 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,902.5 ns | 3,788.0 ns | 207.63 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,884.2 ns | 1,967.7 ns | 107.86 ns |  1.00 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Median      | Allocated |
|------------------------ |------------:|-----------:|----------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.11 μs |   5.407 μs |  0.296 μs |    11.09 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   530.24 μs | 234.975 μs | 12.880 μs |   528.92 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    19.86 μs | 315.850 μs | 17.313 μs |    10.32 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,696.42 μs | 152.642 μs |  8.367 μs | 1,693.44 μs |    1280 B |


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