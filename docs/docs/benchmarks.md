---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 11:44 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,149.74 μs** |   **455.29 μs** |  **24.956 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,315.88 μs | 1,069.12 μs |  58.602 μs |  0.21 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,351.14 μs** | **1,027.31 μs** |  **56.310 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,344.53 μs |   230.39 μs |  12.628 μs |  0.32 |    0.00 |  15.6250 |       - |  339.81 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,343.10 μs** | **1,175.39 μs** |  **64.427 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,198.46 μs | 1,276.58 μs |  69.974 μs |  0.19 |    0.01 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,193.88 μs** | **4,387.34 μs** | **240.485 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,299.08 μs | 2,893.64 μs | 158.610 μs |  0.52 |    0.01 |  15.6250 |       - |   369.5 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.61 μs** |    **71.46 μs** |   **3.917 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.84 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.47 μs |   115.96 μs |   6.356 μs |  0.45 |    0.04 |   0.4883 |  0.2441 |    17.3 KB |        0.40 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,402.15 μs** |   **365.49 μs** |  **20.034 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |  **410.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    860.59 μs | 2,289.76 μs | 125.510 μs |  0.61 |    0.08 |  11.7188 |  7.8125 |  367.25 KB |        0.89 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    306.38 μs |    88.33 μs |   4.842 μs |     ? |       ? |   6.8359 |  5.8594 |  201.55 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,228.24 μs | 7,382.74 μs | 404.673 μs |     ? |       ? |  62.5000 | 54.6875 | 1788.37 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,412.29 μs** |   **147.83 μs** |   **8.103 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,352.69 μs |   664.07 μs |  36.400 μs |  0.25 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,401.61 μs** |    **93.50 μs** |   **5.125 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,345.27 μs |   359.34 μs |  19.696 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,416.97 μs** |   **390.99 μs** |  **21.431 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,302.34 μs |   522.11 μs |  28.619 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,432.72 μs** |   **253.67 μs** |  **13.904 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,284.81 μs |   348.99 μs |  19.129 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.334 ms** | **37.361 ms** | **2.0479 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.337 ms | 13.214 ms | 0.7243 ms | 0.005 |    407 KB |        5.45 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.170 ms** |  **6.205 ms** | **0.3401 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.712 ms | 59.759 ms | 3.2756 ms | 0.005 | 590.77 KB |        2.36 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.873 ms** | **24.870 ms** | **1.3632 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.831 ms | 48.659 ms | 2.6672 ms | 0.006 |  846.7 KB |        1.41 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.423 ms** | **19.858 ms** | **1.0885 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.501 ms | 53.322 ms | 2.9227 ms | 0.005 | 2576.3 KB |        1.09 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.765 ms** | **23.172 ms** | **1.2702 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.377 ms | 11.567 ms | 0.6340 ms | 0.002 | 181.21 KB |       75.31 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.490 ms** | **11.750 ms** | **0.6441 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.656 ms | 12.849 ms | 0.7043 ms | 0.002 | 192.18 KB |       46.15 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.385 ms** | **16.823 ms** | **0.9221 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.696 ms |  7.678 ms | 0.4208 ms | 0.002 | 199.48 KB |       82.90 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.337 ms** |  **8.499 ms** | **0.4659 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.827 ms | 13.268 ms | 0.7273 ms | 0.002 |  185.3 KB |       44.33 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 20.488 μs |  94.035 μs |  5.1544 μs | 23.404 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 16.777 μs | 228.950 μs | 12.5495 μs |  9.607 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.110 μs |   3.023 μs |  0.1657 μs | 10.100 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.854 μs |   4.028 μs |  0.2208 μs | 26.841 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  9.278 μs |   2.219 μs |  0.1217 μs |  9.338 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.478 μs |  64.487 μs |  3.5348 μs | 19.477 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.883 μs |   2.212 μs |  0.1212 μs | 17.863 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.562 μs |   2.784 μs |  0.1526 μs |  4.629 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 12.186 μs |   3.711 μs |  0.2034 μs | 12.283 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,346.3 ns |   379.8 ns |  20.82 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,409.3 ns | 2,004.3 ns | 109.87 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,366.3 ns |   379.8 ns |  20.82 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,585.3 ns | 1,611.3 ns |  88.32 ns |  0.61 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    765.3 ns |   759.5 ns |  41.63 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,197.3 ns | 9,754.2 ns | 534.66 ns |  9.50 |    0.35 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,238.2 ns | 3,232.8 ns | 177.20 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,869.7 ns | 3,224.2 ns | 176.73 ns |  0.91 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.278 μs |   2.231 μs |  0.1223 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   568.716 μs | 553.173 μs | 30.3213 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.538 μs |   8.624 μs |  0.4727 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,658.521 μs | 310.710 μs | 17.0311 μs |    1280 B |


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