---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 05:26 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,151.06 μs** |   **226.28 μs** | **12.403 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,230.26 μs | 1,151.05 μs | 63.093 μs |  0.20 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,385.29 μs** |   **662.30 μs** | **36.303 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,272.43 μs |   283.24 μs | 15.525 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.46 KB |        0.32 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,255.21 μs** |   **419.99 μs** | **23.021 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,301.87 μs | 1,137.49 μs | 62.350 μs |  0.21 |    0.01 |        - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,746.95 μs** | **1,235.66 μs** | **67.731 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,627.42 μs | 1,533.26 μs | 84.043 μs |  0.52 |    0.01 |  15.6250 |       - |  361.67 KB |        0.19 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.76 μs** |    **51.38 μs** |  **2.816 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.29 μs |    85.10 μs |  4.664 μs |  0.43 |    0.03 |   0.2441 |       - |    9.96 KB |        0.24 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,486.61 μs** | **1,673.43 μs** | **91.726 μs** |  **1.00** |    **0.07** |  **23.4375** |       **-** |   **396.8 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    616.75 μs |   281.18 μs | 15.413 μs |  0.42 |    0.02 |        - |       - |   63.91 KB |        0.16 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |        **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    216.00 μs |   355.88 μs | 19.507 μs |     ? |       ? |   0.9766 |       - |   99.76 KB |           ? |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |        **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,843.78 μs |   492.74 μs | 27.009 μs |     ? |       ? |   7.8125 |       - | 1019.28 KB |           ? |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,435.03 μs** |   **176.41 μs** |  **9.670 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,118.21 μs |    38.11 μs |  2.089 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,448.78 μs** |   **212.98 μs** | **11.674 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,121.64 μs |   138.29 μs |  7.580 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,435.54 μs** |    **94.66 μs** |  **5.189 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,117.12 μs |    13.93 μs |  0.764 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,434.01 μs** |   **153.55 μs** |  **8.417 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,119.18 μs |   129.60 μs |  7.104 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.770 ms** |  **7.886 ms** | **0.4323 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.627 ms | 21.627 ms | 1.1854 ms | 0.005 |  593.78 KB |        7.96 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.332 ms** | **39.053 ms** | **2.1406 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.233 ms | 46.506 ms | 2.5491 ms | 0.005 |  777.08 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.446 ms** | **11.806 ms** | **0.6471 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.150 ms | 61.136 ms | 3.3511 ms | 0.005 | 1078.34 KB |        1.79 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.141 ms** | **10.435 ms** | **0.5720 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.111 ms | 10.275 ms | 0.5632 ms | 0.005 | 2762.72 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.396 ms** | **25.550 ms** | **1.4005 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.139 ms | 16.752 ms | 0.9182 ms | 0.002 |  185.59 KB |       77.13 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.714 ms** |  **6.450 ms** | **0.3536 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.770 ms |  8.683 ms | 0.4759 ms | 0.002 |  196.72 KB |       47.24 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.445 ms** | **32.797 ms** | **1.7977 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.837 ms | 14.761 ms | 0.8091 ms | 0.002 |  184.69 KB |       76.75 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.331 ms** | **34.626 ms** | **1.8980 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.584 ms | 27.982 ms | 1.5338 ms | 0.002 |  261.42 KB |       62.55 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.322 μs |   4.967 μs |  0.2723 μs | 15.252 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.086 μs |   3.872 μs |  0.2122 μs |  9.989 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.396 μs |   7.912 μs |  0.4337 μs | 11.636 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 38.962 μs | 206.911 μs | 11.3415 μs | 32.439 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.195 μs |  46.678 μs |  2.5586 μs |  8.732 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.156 μs |   2.563 μs |  0.1405 μs | 22.143 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.071 μs |  19.520 μs |  1.0700 μs | 20.790 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.979 μs |  12.821 μs |  0.7028 μs | 19.598 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  6.216 μs |  54.647 μs |  2.9954 μs |  4.506 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.442 μs |  11.028 μs |  0.6045 μs | 10.296 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,241.0 ns |  3,378.8 ns | 185.20 ns |  0.29 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,329.0 ns |  6,132.9 ns | 336.17 ns |  0.31 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,341.5 ns |  4,676.2 ns | 256.32 ns |  0.32 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,397.3 ns |  2,954.9 ns | 161.97 ns |  0.57 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    798.3 ns |  1,063.8 ns |  58.31 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,893.7 ns | 14,953.4 ns | 819.65 ns |  8.49 |    0.79 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,259.8 ns |  8,559.7 ns | 469.18 ns |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,048.7 ns | 12,773.1 ns | 700.14 ns |  0.96 |    0.17 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev      | Allocated |
|------------------------ |-------------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.460 μs |    10.697 μs |   0.5863 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   627.490 μs | 2,892.737 μs | 158.5607 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.357 μs |     7.210 μs |   0.3952 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,585.694 μs |   442.840 μs |  24.2736 μs |    1280 B |


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