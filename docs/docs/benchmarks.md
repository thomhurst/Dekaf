---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 23:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,096.36 μs** |   **456.27 μs** |  **25.010 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,395.85 μs | 1,853.14 μs | 101.577 μs |  0.23 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,222.09 μs** |   **547.90 μs** |  **30.032 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,303.59 μs |   532.47 μs |  29.186 μs |  0.32 |    0.00 |  15.6250 |       - |  339.71 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,635.54 μs** |   **110.58 μs** |   **6.061 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,161.04 μs |   556.35 μs |  30.495 μs |  0.17 |    0.00 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,902.66 μs** | **5,260.20 μs** | **288.329 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,630.52 μs |   949.07 μs |  52.022 μs |  0.47 |    0.01 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **142.59 μs** |   **225.34 μs** |  **12.351 μs** |  **1.00** |    **0.11** |   **2.4414** |       **-** |   **41.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.56 μs |   191.26 μs |  10.484 μs |  0.45 |    0.07 |        - |       - |    9.05 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,236.58 μs** |   **345.01 μs** |  **18.911 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **414.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    574.10 μs |   234.88 μs |  12.874 μs |  0.46 |    0.01 |        - |       - |   67.45 KB |        0.16 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    198.32 μs |   270.21 μs |  14.811 μs |     ? |       ? |   0.4883 |       - |   16.65 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,983.04 μs | 1,369.99 μs |  75.094 μs |     ? |       ? |   7.8125 |       - |  982.09 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,401.48 μs** |   **223.03 μs** |  **12.225 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,308.64 μs |   260.86 μs |  14.299 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,428.56 μs** | **1,008.37 μs** |  **55.272 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,308.88 μs |   336.71 μs |  18.456 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,403.64 μs** |   **154.16 μs** |   **8.450 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,302.81 μs |   237.97 μs |  13.044 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.27 μs** |   **149.26 μs** |   **8.181 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,309.32 μs |   213.33 μs |  11.693 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.643 ms** | **48.111 ms** | **2.6371 ms** | **3,169.296 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.643 ms | 50.960 ms | 2.7933 ms |    15.560 ms | 0.005 |  596.52 KB |        7.99 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.998 ms** | **19.022 ms** | **1.0427 ms** | **3,165.527 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.738 ms |  9.800 ms | 0.5372 ms |    14.752 ms | 0.005 |  782.66 KB |        3.13 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.884 ms** | **15.809 ms** | **0.8665 ms** | **3,166.111 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.024 ms | 67.242 ms | 3.6858 ms |    18.082 ms | 0.006 | 1067.16 KB |        1.77 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.620 ms** | **13.742 ms** | **0.7532 ms** | **3,165.696 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.630 ms | 21.346 ms | 1.1700 ms |    16.614 ms | 0.005 | 2836.38 KB |        1.20 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.544 ms** | **15.346 ms** | **0.8411 ms** | **3,157.352 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     8.097 ms | 23.968 ms | 1.3138 ms |     8.598 ms | 0.003 |  184.41 KB |       76.64 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.782 ms** |  **6.608 ms** | **0.3622 ms** | **3,154.686 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     8.371 ms | 85.123 ms | 4.6659 ms |     6.273 ms | 0.003 |  190.88 KB |       45.84 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.156 ms** | **15.157 ms** | **0.8308 ms** | **3,157.326 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.091 ms | 13.588 ms | 0.7448 ms |     7.308 ms | 0.002 |  184.66 KB |       76.74 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.966 ms** | **16.286 ms** | **0.8927 ms** | **3,156.495 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.102 ms |  2.992 ms | 0.1640 ms |     6.041 ms | 0.002 |  186.94 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 21.313 μs | 102.7871 μs | 5.6341 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.602 μs |   3.5964 μs | 0.1971 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.751 μs |  25.7774 μs | 1.4129 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.903 μs |   0.7478 μs | 0.0410 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.414 μs |   2.3834 μs | 0.1306 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.355 μs |  66.9563 μs | 3.6701 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.647 μs |   8.2101 μs | 0.4500 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.358 μs |  17.4839 μs | 0.9584 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.599 μs |   2.6413 μs | 0.1448 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.660 μs |  15.3562 μs | 0.8417 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,883.0 ns |  1,973.4 ns | 108.17 ns |  0.39 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,890.3 ns |  8,147.8 ns | 446.61 ns |  0.39 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,363.0 ns |    547.3 ns |  30.00 ns |  0.28 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,001.3 ns |  6,216.8 ns | 340.76 ns |  0.62 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    969.3 ns |  4,696.5 ns | 257.43 ns |  0.20 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,256.3 ns | 16,695.8 ns | 915.15 ns |  8.52 |    0.20 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,844.3 ns |  1,430.7 ns |  78.42 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,867.2 ns |  1,581.0 ns |  86.66 ns |  1.00 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Allocated |
|------------------------ |------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.75 μs |  13.14 μs |  0.720 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   559.49 μs | 354.04 μs | 19.406 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.33 μs |  31.44 μs |  1.723 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,661.63 μs | 216.38 μs | 11.861 μs |    1280 B |


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