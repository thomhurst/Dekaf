---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 03:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,090.63 μs** |    **405.15 μs** |  **22.208 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,227.67 μs |  1,667.24 μs |  91.387 μs |  0.20 |    0.01 |       - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,435.99 μs** |  **1,786.18 μs** |  **97.907 μs** |  **1.00** |    **0.02** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,351.75 μs |    466.50 μs |  25.571 μs |  0.32 |    0.00 | 15.6250 |       - |  339.75 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,624.01 μs** |    **207.00 μs** |  **11.346 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,491.16 μs |  6,570.23 μs | 360.137 μs |  0.23 |    0.05 |       - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,425.87 μs** |  **1,570.48 μs** |  **86.083 μs** |  **1.00** |    **0.01** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,494.97 μs |  7,143.84 μs | 391.578 μs |  0.52 |    0.03 | 15.6250 |       - |  369.45 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.66 μs** |     **62.87 μs** |   **3.446 μs** |  **1.00** |    **0.03** |  **2.4414** |       **-** |   **42.17 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.95 μs |    103.65 μs |   5.681 μs |  0.50 |    0.04 |  0.4883 |       - |   13.84 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,400.11 μs** |    **997.34 μs** |  **54.668 μs** |  **1.00** |    **0.05** | **25.3906** |       **-** |   **414.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    668.12 μs |    841.94 μs |  46.149 μs |  0.48 |    0.03 |  3.9063 |       - |  112.17 KB |        0.27 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    320.37 μs |    158.44 μs |   8.685 μs |     ? |       ? |  6.8359 |  5.8594 |  190.12 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,119.37 μs |  6,372.33 μs | 349.289 μs |     ? |       ? | 70.3125 | 62.5000 | 1979.69 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,398.75 μs** |     **45.49 μs** |   **2.493 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,380.07 μs |    512.52 μs |  28.093 μs |  0.26 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,881.17 μs** | **15,332.69 μs** | **840.436 μs** |  **1.01** |    **0.17** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,370.83 μs |    333.64 μs |  18.288 μs |  0.24 |    0.03 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,403.88 μs** |     **50.76 μs** |   **2.782 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,358.34 μs |    446.05 μs |  24.450 μs |  0.25 |    0.00 |       - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,405.09 μs** |    **112.58 μs** |   **6.171 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,313.75 μs |    408.68 μs |  22.401 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.687 ms** | **20.011 ms** | **1.0968 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13.954 ms | 35.248 ms | 1.9320 ms | 0.004 |  401.83 KB |        5.39 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.763 ms** | **23.980 ms** | **1.3144 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.449 ms | 49.891 ms | 2.7347 ms | 0.004 |  590.22 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.099 ms** | **47.029 ms** | **2.5778 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.703 ms | 41.421 ms | 2.2704 ms | 0.005 |  825.84 KB |        1.37 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.117 ms** | **13.490 ms** | **0.7394 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.425 ms | 36.981 ms | 2.0270 ms | 0.005 | 2646.48 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.833 ms** | **67.507 ms** | **3.7003 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.161 ms | 18.053 ms | 0.9895 ms | 0.002 |  180.56 KB |       75.04 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.854 ms** | **19.170 ms** | **1.0508 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.415 ms |  6.396 ms | 0.3506 ms | 0.002 |  184.81 KB |       44.38 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.135 ms** | **49.376 ms** | **2.7065 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.712 ms |  9.907 ms | 0.5431 ms | 0.002 |  216.93 KB |       90.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.674 ms** |  **4.127 ms** | **0.2262 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.414 ms | 19.104 ms | 1.0472 ms | 0.002 |  183.19 KB |       43.83 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 21.594 μs | 75.245 μs | 4.1244 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.273 μs |  2.976 μs | 0.1631 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 12.071 μs | 13.939 μs | 0.7641 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.710 μs |  1.905 μs | 0.1044 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  9.676 μs |  7.690 μs | 0.4215 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.372 μs | 60.734 μs | 3.3291 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.278 μs |  9.568 μs | 0.5244 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.739 μs |  3.115 μs | 0.1707 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.484 μs |  5.563 μs | 0.3049 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,282.0 ns |     795.2 ns |     43.59 ns |  0.27 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,384.2 ns |     737.3 ns |     40.41 ns |  0.29 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,599.8 ns |   4,490.7 ns |    246.15 ns |  0.34 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,170.0 ns |   7,493.2 ns |    410.73 ns |  0.67 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    807.3 ns |   1,391.8 ns |     76.29 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 48,861.5 ns | 289,092.2 ns | 15,846.12 ns | 10.33 |    2.94 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,738.7 ns |   4,504.4 ns |    246.90 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,659.3 ns |   1,594.7 ns |     87.41 ns |  0.99 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.622 μs |   6.408 μs |  0.3513 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   565.165 μs | 864.438 μs | 47.3828 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.286 μs |   1.277 μs |  0.0700 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,655.040 μs | 137.038 μs |  7.5115 μs |    1280 B |


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