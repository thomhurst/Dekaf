---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 14:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,254.94 μs** |   **474.49 μs** |  **26.008 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,420.64 μs | 2,625.59 μs | 143.918 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,299.16 μs** |   **381.84 μs** |  **20.930 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,325.62 μs | 1,970.12 μs | 107.989 μs |  0.32 |    0.01 |  15.6250 |       - |  339.84 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,614.82 μs** |   **671.26 μs** |  **36.794 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,173.70 μs |   866.81 μs |  47.513 μs |  0.18 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,848.71 μs** | **2,737.24 μs** | **150.037 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,694.95 μs |   866.31 μs |  47.485 μs |  0.48 |    0.01 |  15.6250 |       - |  361.83 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **122.92 μs** |    **38.99 μs** |   **2.137 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.79 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     56.01 μs |    62.98 μs |   3.452 μs |  0.46 |    0.03 |        - |       - |    9.45 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,252.36 μs** | **1,881.69 μs** | **103.142 μs** |  **1.00** |    **0.10** |  **25.3906** |       **-** |  **421.72 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    580.70 μs |   371.66 μs |  20.372 μs |  0.47 |    0.03 |        - |       - |   68.54 KB |        0.16 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    215.90 μs |   390.81 μs |  21.421 μs |     ? |       ? |   0.4883 |       - |    12.2 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,071.01 μs | 4,375.84 μs | 239.854 μs |     ? |       ? |   7.8125 |       - |  949.95 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,432.13 μs** |   **240.81 μs** |  **13.199 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,123.16 μs |   479.60 μs |  26.288 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,414.99 μs** |   **165.59 μs** |   **9.077 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,101.89 μs |    24.68 μs |   1.353 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,442.24 μs** |   **741.42 μs** |  **40.640 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,105.23 μs |    49.04 μs |   2.688 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,419.86 μs** |   **215.41 μs** |  **11.807 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,108.22 μs |    89.28 μs |   4.894 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.569 ms** | **38.103 ms** | **2.0886 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.195 ms | 61.432 ms | 3.3673 ms | 0.005 | 601.99 KB |        8.07 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.501 ms** |  **5.743 ms** | **0.3148 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.044 ms | 21.129 ms | 1.1581 ms | 0.005 | 780.45 KB |        3.12 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.114 ms** | **22.674 ms** | **1.2428 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.481 ms | 51.481 ms | 2.8218 ms | 0.006 | 994.45 KB |        1.65 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.242 ms** |  **7.618 ms** | **0.4175 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.531 ms | 50.665 ms | 2.7771 ms | 0.006 | 2763.4 KB |        1.17 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.059 ms** | **14.261 ms** | **0.7817 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     8.014 ms | 45.693 ms | 2.5046 ms | 0.003 | 188.99 KB |       78.54 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.785 ms** | **35.374 ms** | **1.9390 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.436 ms |  1.457 ms | 0.0799 ms | 0.002 | 186.32 KB |       44.74 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.133 ms** | **11.891 ms** | **0.6518 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.542 ms | 11.560 ms | 0.6336 ms | 0.002 |    186 KB |       77.30 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.751 ms** | **11.591 ms** | **0.6353 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.111 ms | 10.145 ms | 0.5561 ms | 0.002 | 188.72 KB |       45.15 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.619 μs |   4.919 μs |  0.2696 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.920 μs |   4.384 μs |  0.2403 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.263 μs |   5.563 μs |  0.3049 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 39.416 μs | 199.739 μs | 10.9484 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.201 μs |   2.809 μs |  0.1540 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.244 μs |   5.033 μs |  0.2759 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.024 μs |  46.844 μs |  2.5677 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.280 μs |  46.715 μs |  2.5606 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.763 μs |  10.185 μs |  0.5583 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.050 μs |   4.353 μs |  0.2386 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,272.2 ns |   2,841.6 ns |    155.76 ns |  0.32 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,408.3 ns |   3,492.6 ns |    191.44 ns |  0.35 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,406.8 ns |   1,807.0 ns |     99.05 ns |  0.35 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,473.5 ns |   4,107.9 ns |    225.17 ns |  0.61 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    904.5 ns |   2,917.1 ns |    159.90 ns |  0.22 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 43,445.3 ns | 281,757.6 ns | 15,444.09 ns | 10.80 |    3.51 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,060.8 ns |   9,082.7 ns |    497.86 ns |  1.01 |    0.15 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,772.3 ns |  13,506.5 ns |    740.34 ns |  0.94 |    0.19 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.316 μs |   8.578 μs |  0.4702 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.936 μs | 230.752 μs | 12.6483 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.279 μs |   5.338 μs |  0.2926 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,625.642 μs | 365.490 μs | 20.0338 μs |    1280 B |


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