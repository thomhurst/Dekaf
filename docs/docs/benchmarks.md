---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 11:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,134.90 μs** |   **241.82 μs** |  **13.255 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,281.02 μs |   920.55 μs |  50.458 μs |  0.21 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,340.21 μs** |   **732.91 μs** |  **40.173 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,342.49 μs |   310.89 μs |  17.041 μs |  0.32 |    0.00 |  15.6250 |       - |  339.88 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,369.26 μs** |   **256.27 μs** |  **14.047 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,295.40 μs | 2,364.72 μs | 129.618 μs |  0.20 |    0.02 |        - |       - |   36.87 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,428.62 μs** | **6,284.95 μs** | **344.499 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,863.02 μs | 9,578.85 μs | 525.049 μs |  0.55 |    0.04 |  15.6250 |       - |  369.35 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **142.95 μs** |    **96.93 μs** |   **5.313 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **42.04 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     66.65 μs |    61.26 μs |   3.358 μs |  0.47 |    0.03 |   0.4883 |       - |   14.85 KB |        0.35 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,445.16 μs** |    **50.19 μs** |   **2.751 μs** |  **1.00** |    **0.00** |  **23.4375** |       **-** |  **416.78 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    686.59 μs | 1,651.24 μs |  90.510 μs |  0.48 |    0.05 |   5.8594 |  3.9063 |  159.58 KB |        0.38 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    321.60 μs |   144.43 μs |   7.917 μs |     ? |       ? |   6.8359 |  5.8594 |  197.92 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,203.92 μs | 1,970.94 μs | 108.034 μs |     ? |       ? |  62.5000 | 54.6875 | 1894.18 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,398.31 μs** |   **121.52 μs** |   **6.661 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,111.64 μs |   101.77 μs |   5.578 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,407.38 μs** |   **146.37 μs** |   **8.023 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,383.88 μs |   217.95 μs |  11.947 μs |  0.26 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,412.33 μs** |    **41.49 μs** |   **2.274 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,340.80 μs |   518.61 μs |  28.427 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,408.88 μs** |    **63.94 μs** |   **3.505 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,224.55 μs |   566.55 μs |  31.055 μs |  0.23 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.985 ms** |  **29.697 ms** |  **1.6278 ms** | **3,169.851 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.470 ms |  39.845 ms |  2.1841 ms |    18.611 ms | 0.006 |  417.62 KB |        5.60 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.674 ms** |   **8.059 ms** |  **0.4417 ms** | **3,166.540 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.047 ms |  39.078 ms |  2.1420 ms |    12.889 ms | 0.004 |   590.8 KB |        2.36 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.280 ms** |   **6.807 ms** |  **0.3731 ms** | **3,166.098 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.655 ms | 218.040 ms | 11.9515 ms |    15.155 ms | 0.007 |  892.51 KB |        1.48 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.192 ms** |  **29.948 ms** |  **1.6415 ms** | **3,165.254 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.954 ms |  56.216 ms |  3.0814 ms |    16.938 ms | 0.005 | 2576.47 KB |        1.09 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.504 ms** |  **33.777 ms** |  **1.8515 ms** | **3,157.410 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.737 ms |  23.756 ms |  1.3022 ms |     6.348 ms | 0.002 |  197.47 KB |       82.06 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.362 ms** |   **6.073 ms** |  **0.3329 ms** | **3,156.298 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.577 ms |   3.124 ms |  0.1712 ms |     5.632 ms | 0.002 |  183.32 KB |       44.02 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.519 ms** |  **47.096 ms** |  **2.5815 ms** | **3,156.789 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.753 ms |   6.394 ms |  0.3505 ms |     6.894 ms | 0.002 |  226.45 KB |       94.11 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.532 ms** |   **6.724 ms** |  **0.3685 ms** | **3,156.494 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.971 ms |  19.276 ms |  1.0566 ms |     5.484 ms | 0.002 |  259.57 KB |       62.10 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.999 μs |   9.8168 μs | 0.5381 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.333 μs |  30.2518 μs | 1.6582 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.046 μs |   0.8999 μs | 0.0493 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.071 μs | 111.1307 μs | 6.0914 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 11.007 μs |  68.6083 μs | 3.7607 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.257 μs |   1.2047 μs | 0.0660 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.830 μs |   6.1200 μs | 0.3355 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.449 μs |   1.1038 μs | 0.0605 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.203 μs |   8.6351 μs | 0.4733 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,328.3 ns | 1,395.4 ns |  76.49 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,302.2 ns |   173.4 ns |   9.50 ns |  0.32 |    0.00 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,619.7 ns | 5,087.2 ns | 278.84 ns |  0.40 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,121.7 ns | 4,876.7 ns | 267.31 ns |  0.77 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    714.3 ns |   105.3 ns |   5.77 ns |  0.18 |    0.00 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,323.3 ns | 6,117.5 ns | 335.32 ns |  9.70 |    0.13 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,053.7 ns | 1,004.8 ns |  55.08 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,177.0 ns | 8,854.3 ns | 485.33 ns |  1.03 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.18 μs |  33.974 μs |  1.862 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   564.58 μs | 461.050 μs | 25.272 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.08 μs |   4.540 μs |  0.249 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,682.97 μs | 136.771 μs |  7.497 μs |    1280 B |


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