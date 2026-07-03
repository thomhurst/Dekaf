---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 00:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,051.09 μs** |   **714.21 μs** |  **39.148 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,340.19 μs |   986.07 μs |  54.050 μs |  0.22 |    0.01 |       - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,179.74 μs** |   **650.61 μs** |  **35.662 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,261.76 μs |   435.68 μs |  23.881 μs |  0.32 |    0.00 | 11.7188 |       - |  339.96 KB |        0.32 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,580.19 μs** | **1,058.16 μs** |  **58.001 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,108.49 μs |   142.26 μs |   7.798 μs |  0.17 |    0.00 |       - |       - |   36.95 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,284.02 μs** | **3,908.63 μs** | **214.245 μs** |  **1.00** |    **0.03** | **78.1250** | **31.2500** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,013.73 μs | 1,581.96 μs |  86.713 μs |  0.54 |    0.01 |  7.8125 |       - |  369.53 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **123.19 μs** |    **50.82 μs** |   **2.786 μs** |  **1.00** |    **0.03** |  **1.4648** |       **-** |    **36.7 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    59.99 μs |    77.31 μs |   4.238 μs |  0.49 |    0.03 |  0.2441 |       - |    9.52 KB |        0.26 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,241.50 μs** |   **280.89 μs** |  **15.397 μs** |  **1.00** |    **0.02** | **15.6250** |       **-** |  **421.61 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   708.95 μs |   959.89 μs |  52.615 μs |  0.57 |    0.04 |       - |       - |   42.66 KB |        0.10 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   305.09 μs |   274.77 μs |  15.061 μs |     ? |       ? |  3.9063 |  2.9297 |  201.29 KB |           ? |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,820.09 μs |   615.61 μs |  33.744 μs |     ? |       ? | 39.0625 | 31.2500 | 1774.19 KB |           ? |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,404.97 μs** |   **588.13 μs** |  **32.238 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,349.02 μs |   305.32 μs |  16.736 μs |  0.25 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,404.03 μs** |   **617.43 μs** |  **33.844 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,270.74 μs |   367.93 μs |  20.168 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,356.55 μs** |    **38.85 μs** |   **2.130 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,278.44 μs |   222.45 μs |  12.193 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,354.68 μs** |   **136.89 μs** |   **7.503 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,276.38 μs |   497.92 μs |  27.293 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.292 ms** | **14.201 ms** | **0.7784 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.871 ms | 50.109 ms | 2.7466 ms | 0.005 |  440.04 KB |        5.90 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.234 ms** | **12.620 ms** | **0.6918 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.576 ms | 40.239 ms | 2.2057 ms | 0.004 |  854.13 KB |        3.41 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.386 ms** | **15.781 ms** | **0.8650 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.589 ms | 40.859 ms | 2.2396 ms | 0.005 |  1069.3 KB |        1.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.777 ms** | **10.176 ms** | **0.5578 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.805 ms | 39.859 ms | 2.1848 ms | 0.005 | 5651.36 KB |        2.39 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.705 ms** | **30.260 ms** | **1.6586 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.254 ms |  4.178 ms | 0.2290 ms | 0.002 |  243.76 KB |      101.30 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.951 ms** |  **8.375 ms** | **0.4591 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.440 ms |  8.504 ms | 0.4661 ms | 0.002 |  693.73 KB |      166.60 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.253 ms** | **65.321 ms** | **3.5805 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.428 ms |  1.876 ms | 0.1028 ms | 0.002 |  694.37 KB |      288.57 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.202 ms** |  **1.357 ms** | **0.0744 ms** | **1.000** |    **5.05 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.970 ms | 13.409 ms | 0.7350 ms | 0.002 | 2230.35 KB |      441.24 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.785 μs | 5.0719 μs | 0.2780 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.764 μs | 3.3192 μs | 0.1819 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.033 μs | 4.4580 μs | 0.2444 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.691 μs | 1.5747 μs | 0.0863 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.899 μs | 0.5574 μs | 0.0306 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.225 μs | 0.7625 μs | 0.0418 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.821 μs | 7.3410 μs | 0.4024 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.315 μs | 2.9492 μs | 0.1617 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.401 μs | 4.6201 μs | 0.2532 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,266.3 ns |  1,463.3 ns |    80.21 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,495.7 ns |  4,238.7 ns |   232.34 ns |  0.37 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,335.0 ns |    701.1 ns |    38.43 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,472.8 ns |  1,004.8 ns |    55.08 ns |  0.61 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    710.3 ns |    640.7 ns |    35.12 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 52,165.3 ns | 73,987.5 ns | 4,055.51 ns | 12.84 |    0.92 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,064.5 ns |  2,019.0 ns |   110.67 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,922.5 ns |  1,139.3 ns |    62.45 ns |  0.97 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.642 μs |   2.811 μs |  0.1541 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   531.042 μs | 228.273 μs | 12.5124 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.070 μs |  11.958 μs |  0.6555 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,719.299 μs | 218.044 μs | 11.9517 μs |    1280 B |


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