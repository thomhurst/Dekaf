---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 16:20 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,163.25 μs** |   **275.98 μs** |  **15.128 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,249.50 μs |   136.38 μs |   7.475 μs |  0.20 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,379.82 μs** | **1,680.64 μs** |  **92.122 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,351.68 μs |   534.09 μs |  29.275 μs |  0.32 |    0.00 |  15.6250 |       - |  339.61 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,228.13 μs** | **1,070.49 μs** |  **58.677 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,332.83 μs | 2,853.15 μs | 156.391 μs |  0.21 |    0.02 |        - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,772.53 μs** | **1,985.79 μs** | **108.848 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,349.65 μs | 1,963.50 μs | 107.626 μs |  0.50 |    0.01 |  15.6250 |       - |  361.84 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.72 μs** |    **64.52 μs** |   **3.537 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.81 μs |   138.61 μs |   7.598 μs |  0.47 |    0.05 |        - |       - |    6.63 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,456.07 μs** |   **850.53 μs** |  **46.621 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |  **418.41 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    650.11 μs |   380.54 μs |  20.859 μs |  0.45 |    0.02 |        - |       - |   66.32 KB |        0.16 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    216.07 μs |   216.47 μs |  11.865 μs |     ? |       ? |   0.9766 |       - |   98.66 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,972.71 μs | 3,385.80 μs | 185.587 μs |     ? |       ? |   7.8125 |       - | 1002.97 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,419.34 μs** |    **68.14 μs** |   **3.735 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,396.38 μs |   534.39 μs |  29.292 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,561.46 μs** | **4,314.24 μs** | **236.478 μs** |  **1.00** |    **0.05** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,390.19 μs |   629.07 μs |  34.482 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,440.83 μs** |   **591.77 μs** |  **32.437 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,401.28 μs |   356.52 μs |  19.542 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,438.20 μs** |   **226.08 μs** |  **12.392 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,389.62 μs |   213.81 μs |  11.719 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.418 ms** | **21.182 ms** | **1.1611 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.308 ms | 27.780 ms | 1.5227 ms | 0.005 |  593.39 KB |        7.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.005 ms** | **19.117 ms** | **1.0479 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.277 ms | 21.360 ms | 1.1708 ms | 0.005 |  784.57 KB |        3.13 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.004 ms** |  **8.100 ms** | **0.4440 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.763 ms | 78.539 ms | 4.3050 ms | 0.006 |  1031.1 KB |        1.71 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.132 ms** | **11.835 ms** | **0.6487 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.546 ms | 28.943 ms | 1.5865 ms | 0.006 | 2761.63 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.283 ms** | **18.625 ms** | **1.0209 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.308 ms | 12.829 ms | 0.7032 ms | 0.002 |  184.41 KB |       76.64 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.286 ms** |  **2.102 ms** | **0.1152 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.221 ms | 18.523 ms | 1.0153 ms | 0.002 |  192.02 KB |       46.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.240 ms** | **16.270 ms** | **0.8918 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.475 ms | 15.192 ms | 0.8327 ms | 0.002 |  184.66 KB |       76.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.960 ms** | **15.497 ms** | **0.8495 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.002 ms | 20.238 ms | 1.1093 ms | 0.002 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.820 μs | 32.198 μs | 1.7649 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.577 μs |  2.290 μs | 0.1255 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.355 μs |  2.329 μs | 0.1277 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.165 μs |  4.105 μs | 0.2250 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.882 μs |  1.139 μs | 0.0624 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.238 μs |  3.340 μs | 0.1831 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.547 μs |  8.078 μs | 0.4428 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.804 μs |  8.800 μs | 0.4824 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.449 μs |  3.170 μs | 0.1738 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.626 μs |  4.268 μs | 0.2340 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,272.7 ns | 3,008.9 ns | 164.93 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,460.0 ns | 4,363.5 ns | 239.18 ns |  0.35 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,469.5 ns | 2,592.7 ns | 142.12 ns |  0.36 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,465.3 ns |   660.4 ns |  36.20 ns |  0.60 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    838.5 ns | 2,060.6 ns | 112.95 ns |  0.20 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,517.0 ns | 9,226.7 ns | 505.75 ns | 10.08 |    0.80 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,141.7 ns | 7,226.2 ns | 396.09 ns |  1.01 |    0.11 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,885.0 ns | 1,654.8 ns |  90.70 ns |  0.94 |    0.08 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.095 μs |   3.329 μs |  0.1825 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   538.054 μs | 417.336 μs | 22.8756 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.569 μs |   2.976 μs |  0.1631 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,672.773 μs | 322.499 μs | 17.6772 μs |    1280 B |


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