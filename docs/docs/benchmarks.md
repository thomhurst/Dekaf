---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 01:40 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,427.96 μs** |    **590.01 μs** |  **32.340 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,401.45 μs |  1,501.91 μs |  82.325 μs |  0.22 |    0.01 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,544.22 μs** |    **535.34 μs** |  **29.344 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,372.20 μs |    341.28 μs |  18.707 μs |  0.31 |    0.00 |  15.6250 |       - |  339.77 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,250.79 μs** |    **891.94 μs** |  **48.890 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,313.77 μs |  2,555.11 μs | 140.054 μs |  0.21 |    0.02 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,379.54 μs** |    **724.79 μs** |  **39.728 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,062.16 μs | 15,115.06 μs | 828.507 μs |  0.57 |    0.06 |  15.6250 |       - |  369.56 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **152.33 μs** |    **160.18 μs** |   **8.780 μs** |  **1.00** |    **0.07** |   **2.4414** |       **-** |   **41.26 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     92.24 μs |    456.79 μs |  25.038 μs |  0.61 |    0.15 |   0.4883 |       - |   15.14 KB |        0.37 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,450.26 μs** |    **221.37 μs** |  **12.134 μs** |  **1.00** |    **0.01** |  **23.4375** |       **-** |  **418.57 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    736.10 μs |    593.69 μs |  32.542 μs |  0.51 |    0.02 |   3.9063 |       - |  106.13 KB |        0.25 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    334.94 μs |    180.87 μs |   9.914 μs |     ? |       ? |   5.8594 |  4.8828 |  189.85 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,164.04 μs |  3,046.00 μs | 166.962 μs |     ? |       ? |  70.3125 | 62.5000 | 2006.79 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,462.23 μs** |    **208.40 μs** |  **11.423 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,112.36 μs |     11.33 μs |   0.621 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.06 μs** |     **81.97 μs** |   **4.493 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,168.18 μs |    421.74 μs |  23.117 μs |  0.22 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,417.48 μs** |     **66.64 μs** |   **3.653 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,288.28 μs |    284.54 μs |  15.596 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,416.08 μs** |     **15.33 μs** |   **0.840 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,315.32 μs |    119.84 μs |   6.569 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.048 ms** | **14.854 ms** | **0.8142 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.650 ms | 36.387 ms | 1.9945 ms | 0.005 |  448.25 KB |        6.01 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.811 ms** |  **9.281 ms** | **0.5087 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.438 ms | 38.263 ms | 2.0973 ms | 0.005 |  851.27 KB |        3.40 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.500 ms** | **40.551 ms** | **2.2228 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.617 ms | 58.685 ms | 3.2167 ms | 0.005 | 1071.09 KB |        1.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.530 ms** | **13.911 ms** | **0.7625 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.950 ms | 65.190 ms | 3.5733 ms | 0.005 | 5652.95 KB |        2.39 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.857 ms** | **30.136 ms** | **1.6519 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.104 ms | 18.783 ms | 1.0296 ms | 0.002 |  244.99 KB |      101.81 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.739 ms** | **19.221 ms** | **1.0536 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.763 ms | 10.239 ms | 0.5612 ms | 0.002 |  694.98 KB |      166.90 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.194 ms** | **24.363 ms** | **1.3354 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.132 ms | 20.733 ms | 1.1364 ms | 0.003 |  693.21 KB |      288.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.624 ms** | **13.854 ms** | **0.7594 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.740 ms | 33.301 ms | 1.8254 ms | 0.002 | 2231.62 KB |      533.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 12.008 μs |  1.585 μs | 0.0869 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  8.393 μs | 24.391 μs | 1.3370 μs |         - |
| &#39;Write 100 CompactStrings&#39;                |  8.620 μs |  3.984 μs | 0.2184 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 25.509 μs |  3.733 μs | 0.2046 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  7.160 μs |  1.642 μs | 0.0900 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 18.628 μs | 69.407 μs | 3.8044 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 14.256 μs | 11.257 μs | 0.6170 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  3.472 μs |  6.436 μs | 0.3528 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; |  8.465 μs | 10.342 μs | 0.5669 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,052.0 ns |  3,644.2 ns | 199.7 ns |  0.33 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,148.7 ns |  4,814.2 ns | 263.9 ns |  0.36 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,199.0 ns |  4,641.1 ns | 254.4 ns |  0.38 |    0.08 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  1,815.7 ns |  5,460.0 ns | 299.3 ns |  0.57 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    557.7 ns |  2,114.5 ns | 115.9 ns |  0.18 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 28,179.3 ns | 12,120.5 ns | 664.4 ns |  8.91 |    0.80 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,185.0 ns |  5,708.9 ns | 312.9 ns |  1.01 |    0.12 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  2,915.0 ns |  8,898.4 ns | 487.7 ns |  0.92 |    0.16 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     9.518 μs |  13.027 μs |  0.7140 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   428.573 μs | 510.419 μs | 27.9778 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     7.008 μs |  11.740 μs |  0.6435 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,233.918 μs | 210.720 μs | 11.5503 μs |    1280 B |


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