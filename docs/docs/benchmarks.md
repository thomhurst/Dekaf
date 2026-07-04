---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 10:00 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,116.66 μs** |   **521.442 μs** |  **28.582 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,469.77 μs | 3,797.525 μs | 208.155 μs |  0.24 |    0.03 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,438.64 μs** | **1,727.789 μs** |  **94.706 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,394.38 μs |   378.193 μs |  20.730 μs |  0.32 |    0.00 |  15.6250 |       - |  339.61 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,261.86 μs** |   **844.757 μs** |  **46.304 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,241.70 μs | 1,924.132 μs | 105.468 μs |  0.20 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,699.53 μs** | **4,793.053 μs** | **262.723 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,467.32 μs | 1,200.949 μs |  65.828 μs |  0.51 |    0.01 |  15.6250 |       - |  361.81 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **147.43 μs** |    **51.345 μs** |   **2.814 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |    **42.4 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.57 μs |    87.212 μs |   4.780 μs |  0.44 |    0.03 |        - |       - |     9.4 KB |        0.22 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,360.01 μs** |   **573.156 μs** |  **31.417 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |   **425.7 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    615.07 μs |   350.546 μs |  19.215 μs |  0.45 |    0.02 |        - |       - |   87.75 KB |        0.21 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    198.34 μs |   262.428 μs |  14.385 μs |     ? |       ? |   0.9766 |       - |  101.78 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,967.55 μs | 4,792.882 μs | 262.714 μs |     ? |       ? |   7.8125 |       - |  963.49 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,416.20 μs** |    **26.076 μs** |   **1.429 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,166.74 μs |   286.577 μs |  15.708 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,418.02 μs** |    **29.581 μs** |   **1.621 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,159.55 μs |   226.892 μs |  12.437 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,428.41 μs** |   **123.875 μs** |   **6.790 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,185.06 μs |   221.279 μs |  12.129 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,424.86 μs** |     **5.523 μs** |   **0.303 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,232.35 μs |   337.756 μs |  18.514 μs |  0.23 |    0.00 |        - |       - |    1.15 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,165.902 ms** | **53.161 ms** | **2.9139 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.086 ms | 16.823 ms | 0.9221 ms | 0.005 |  592.02 KB |        7.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.671 ms** | **63.239 ms** | **3.4663 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.370 ms | 11.059 ms | 0.6062 ms | 0.004 |  776.56 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.160 ms** |  **7.641 ms** | **0.4188 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.207 ms | 21.448 ms | 1.1757 ms | 0.004 | 1067.22 KB |        1.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163.385 ms** | **26.296 ms** | **1.4414 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.576 ms | 51.719 ms | 2.8349 ms | 0.005 | 2761.97 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.557 ms** | **32.722 ms** | **1.7936 ms** | **1.000** |    **3.34 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.478 ms | 15.856 ms | 0.8691 ms | 0.002 |  188.09 KB |       56.25 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.899 ms** | **28.299 ms** | **1.5511 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.005 ms | 26.765 ms | 1.4671 ms | 0.002 |  195.45 KB |       46.94 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,154.965 ms** |  **6.573 ms** | **0.3603 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.348 ms |  8.313 ms | 0.4556 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.532 ms** | **25.671 ms** | **1.4071 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.014 ms | 19.688 ms | 1.0792 ms | 0.002 |  188.44 KB |       45.08 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.837 μs |   0.3160 μs |  0.0173 μs | 14.827 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.655 μs |   2.1077 μs |  0.1155 μs |  9.658 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.270 μs |   1.1291 μs |  0.0619 μs | 10.250 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.773 μs |   4.0098 μs |  0.2198 μs | 26.700 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.992 μs |   2.9064 μs |  0.1593 μs |  8.931 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.465 μs |   3.1539 μs |  0.1729 μs | 19.431 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.307 μs |   5.9158 μs |  0.3243 μs | 18.223 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.057 μs |  45.6774 μs |  2.5037 μs | 21.519 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  8.231 μs |  99.6994 μs |  5.4649 μs |  5.696 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 17.211 μs | 206.1047 μs | 11.2973 μs | 10.769 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,690.2 ns |  6,866.9 ns | 376.40 ns |  0.40 |    0.08 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,272.3 ns |  4,311.9 ns | 236.35 ns |  0.30 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,468.7 ns |  2,009.6 ns | 110.15 ns |  0.35 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,719.0 ns |  6,701.9 ns | 367.35 ns |  0.65 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    735.3 ns |    640.7 ns |  35.12 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,535.0 ns | 12,531.6 ns | 686.90 ns | 10.11 |    0.15 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,208.2 ns |    538.2 ns |  29.50 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,953.7 ns |  1,950.7 ns | 106.93 ns |  0.94 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.16 μs |   1.794 μs |  0.098 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   515.79 μs | 368.837 μs | 20.217 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.01 μs |   4.617 μs |  0.253 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,682.70 μs | 435.926 μs | 23.895 μs |    1280 B |


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