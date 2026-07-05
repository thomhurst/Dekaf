---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 18:07 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,056.34 μs** |   **506.48 μs** |  **27.762 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,373.06 μs | 1,501.04 μs |  82.277 μs |  0.23 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,179.36 μs** | **1,562.93 μs** |  **85.670 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,231.80 μs |   307.92 μs |  16.878 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.53 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,637.06 μs** |   **871.36 μs** |  **47.762 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,136.03 μs |   426.32 μs |  23.368 μs |  0.17 |    0.00 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,175.31 μs** | **1,531.56 μs** |  **83.950 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,635.19 μs | 1,442.89 μs |  79.090 μs |  0.50 |    0.01 |  15.6250 |       - |  361.78 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.09 μs** |   **137.38 μs** |   **7.530 μs** |  **1.00** |    **0.07** |   **2.1973** |       **-** |    **35.9 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.23 μs |    55.88 μs |   3.063 μs |  0.42 |    0.03 |        - |       - |   10.63 KB |        0.30 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,418.99 μs** | **2,307.88 μs** | **126.503 μs** |  **1.01** |    **0.11** |  **25.3906** |       **-** |  **431.36 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    623.22 μs | 1,429.23 μs |  78.341 μs |  0.44 |    0.06 |        - |       - |   78.87 KB |        0.18 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    197.50 μs |   305.30 μs |  16.734 μs |     ? |       ? |   0.9766 |       - |   97.74 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,231.33 μs | 6,895.41 μs | 377.960 μs |     ? |       ? |   7.8125 |       - | 1014.47 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,398.87 μs** |    **83.38 μs** |   **4.570 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,360.61 μs |   374.99 μs |  20.554 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,399.28 μs** |    **64.72 μs** |   **3.548 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,356.17 μs |   363.71 μs |  19.936 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,399.68 μs** |    **51.85 μs** |   **2.842 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,366.61 μs |   511.82 μs |  28.055 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.39 μs** |   **113.09 μs** |   **6.199 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,103.76 μs |    56.59 μs |   3.102 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.624 ms** | **11.220 ms** | **0.6150 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.675 ms | 52.172 ms | 2.8597 ms | 0.006 |  597.16 KB |        8.00 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.879 ms** | **56.238 ms** | **3.0826 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.951 ms | 63.324 ms | 3.4710 ms | 0.005 |  778.63 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.633 ms** |  **3.456 ms** | **0.1895 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.917 ms | 45.459 ms | 2.4918 ms | 0.005 |  998.09 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.872 ms** | **15.872 ms** | **0.8700 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.497 ms | 28.519 ms | 1.5632 ms | 0.005 | 2763.88 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.827 ms** | **26.781 ms** | **1.4680 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.650 ms | 16.067 ms | 0.8807 ms | 0.002 |  184.41 KB |       76.64 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.576 ms** | **27.388 ms** | **1.5012 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.176 ms | 15.021 ms | 0.8233 ms | 0.002 |  187.86 KB |       45.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.309 ms** | **32.357 ms** | **1.7736 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.655 ms | 32.259 ms | 1.7682 ms | 0.002 |  328.71 KB |      136.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.978 ms** | **25.845 ms** | **1.4167 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.668 ms |  2.945 ms | 0.1614 ms | 0.002 |  187.13 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.922 μs |  3.5439 μs | 0.1943 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.276 μs | 14.3980 μs | 0.7892 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.490 μs |  7.2861 μs | 0.3994 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.648 μs | 29.0711 μs | 1.5935 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.986 μs |  0.9585 μs | 0.0525 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.531 μs |  1.5552 μs | 0.0852 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.067 μs |  3.4300 μs | 0.1880 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.436 μs |  4.7802 μs | 0.2620 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.732 μs |  2.1250 μs | 0.1165 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.656 μs |  5.4419 μs | 0.2983 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,232.0 ns |   729.7 ns |  40.00 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,285.0 ns |   467.6 ns |  25.63 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,443.0 ns | 1,590.5 ns |  87.18 ns |  0.35 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,549.5 ns | 1,621.5 ns |  88.88 ns |  0.61 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    928.7 ns | 3,981.7 ns | 218.25 ns |  0.22 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,641.3 ns | 4,382.7 ns | 240.23 ns |  9.55 |    0.16 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,153.7 ns | 1,381.4 ns |  75.72 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,004.7 ns | 1,117.8 ns |  61.27 ns |  0.96 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.202 μs |  18.043 μs |  0.9890 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   535.572 μs | 492.763 μs | 27.0100 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.942 μs |   1.934 μs |  0.1060 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,660.591 μs | 468.148 μs | 25.6608 μs |    1280 B |


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