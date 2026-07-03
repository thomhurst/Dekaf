---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 02:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,076.36 μs** |   **672.35 μs** |  **36.854 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,389.88 μs | 1,613.43 μs |  88.438 μs |  0.23 |    0.01 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,256.95 μs** |   **249.12 μs** |  **13.655 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,306.85 μs |   490.56 μs |  26.889 μs |  0.32 |    0.00 |  15.6250 |       - |  339.94 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,643.64 μs** |   **458.25 μs** |  **25.118 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,193.27 μs | 1,169.52 μs |  64.106 μs |  0.18 |    0.01 |        - |       - |   36.93 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,581.10 μs** | **3,028.92 μs** | **166.025 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,295.40 μs | 7,201.98 μs | 394.765 μs |  0.54 |    0.03 |  15.6250 |       - |  369.41 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **122.91 μs** |    **58.11 μs** |   **3.185 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.83 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.36 μs |    94.61 μs |   5.186 μs |  0.52 |    0.04 |   0.7324 |  0.4883 |   18.86 KB |        0.45 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,204.34 μs** |   **630.81 μs** |  **34.577 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |  **410.27 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    709.45 μs | 1,253.24 μs |  68.694 μs |  0.59 |    0.05 |   3.9063 |       - |  101.83 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    326.56 μs |   349.78 μs |  19.173 μs |     ? |       ? |   6.8359 |  5.8594 |  198.95 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,411.16 μs | 2,823.60 μs | 154.771 μs |     ? |       ? |  62.5000 | 54.6875 | 1861.17 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,404.90 μs** |    **37.22 μs** |   **2.040 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,353.27 μs |   269.40 μs |  14.767 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,404.79 μs** |    **65.88 μs** |   **3.611 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,355.40 μs |   341.14 μs |  18.699 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,409.25 μs** |    **20.07 μs** |   **1.100 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,103.67 μs |    47.89 μs |   2.625 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.72 μs** |    **40.28 μs** |   **2.208 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,101.43 μs |    13.23 μs |   0.725 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.773 ms** | **10.813 ms** | **0.5927 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.676 ms | 49.334 ms | 2.7041 ms | 0.005 |  412.02 KB |        5.52 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.302 ms** | **11.014 ms** | **0.6037 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.367 ms | 21.106 ms | 1.1569 ms | 0.004 |  596.91 KB |        2.38 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.895 ms** | **14.275 ms** | **0.7825 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.751 ms | 30.222 ms | 1.6566 ms | 0.005 |  806.85 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.841 ms** | **14.375 ms** | **0.7879 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.660 ms | 44.210 ms | 2.4233 ms | 0.005 | 2573.89 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.734 ms** | **20.295 ms** | **1.1124 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.174 ms | 19.085 ms | 1.0461 ms | 0.002 |  185.58 KB |       77.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.069 ms** | **32.652 ms** | **1.7897 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.800 ms | 14.183 ms | 0.7774 ms | 0.002 |  197.83 KB |       47.51 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.867 ms** | **42.439 ms** | **2.3262 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.997 ms | 14.235 ms | 0.7803 ms | 0.002 |  181.13 KB |       75.28 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.317 ms** |  **5.343 ms** | **0.2928 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.420 ms | 10.284 ms | 0.5637 ms | 0.002 |  184.79 KB |       44.21 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.493 μs |   5.187 μs |  0.2843 μs | 14.547 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.574 μs |  38.358 μs |  2.1025 μs |  9.582 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.070 μs |   3.459 μs |  0.1896 μs | 10.004 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.648 μs |   2.456 μs |  0.1346 μs | 26.704 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.969 μs |   3.140 μs |  0.1721 μs |  8.941 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 32.586 μs | 215.471 μs | 11.8107 μs | 25.994 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 21.767 μs |   4.454 μs |  0.2442 μs | 21.860 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.618 μs |   3.635 μs |  0.1992 μs |  4.508 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.813 μs |  15.564 μs |  0.8531 μs | 12.033 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.303 μs |  1.6511 μs | 0.0905 μs |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.389 μs |  0.2107 μs | 0.0115 μs |  0.33 |    0.00 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.803 μs |  2.2067 μs | 0.1210 μs |  0.43 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.913 μs |  1.9336 μs | 0.1060 μs |  0.69 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.081 μs |  4.1721 μs | 0.2287 μs |  0.26 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40.429 μs | 10.6810 μs | 0.5855 μs |  9.60 |    0.16 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.211 μs |  1.0048 μs | 0.0551 μs |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.050 μs |  2.4725 μs | 0.1355 μs |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.85 μs |   6.058 μs |  0.332 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   525.83 μs | 383.986 μs | 21.048 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.02 μs |   4.548 μs |  0.249 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,681.50 μs | 645.486 μs | 35.381 μs |    1280 B |


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