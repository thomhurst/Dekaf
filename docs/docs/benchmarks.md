---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 06:28 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,207.10 μs** | **1,734.351 μs** |  **95.066 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,299.84 μs | 1,598.908 μs |  87.642 μs |  0.21 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,240.78 μs** | **1,538.466 μs** |  **84.329 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,243.61 μs |   431.641 μs |  23.660 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.89 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,596.47 μs** | **1,235.509 μs** |  **67.722 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,348.90 μs | 3,824.440 μs | 209.630 μs |  0.20 |    0.03 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,868.66 μs** | **1,473.804 μs** |  **80.784 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,313.19 μs | 4,787.581 μs | 262.423 μs |  0.53 |    0.02 |  15.6250 |       - |  369.35 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.29 μs** |    **58.384 μs** |   **3.200 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.24 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     61.87 μs |    66.565 μs |   3.649 μs |  0.49 |    0.03 |   0.4883 |  0.2441 |   16.81 KB |        0.40 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,263.82 μs** |   **810.177 μs** |  **44.409 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |  **412.72 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    744.96 μs |   503.900 μs |  27.620 μs |  0.59 |    0.03 |   3.9063 |       - |  169.04 KB |        0.41 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    349.06 μs |   788.991 μs |  43.247 μs |     ? |       ? |   5.8594 |  4.8828 |  181.57 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,286.12 μs | 3,460.132 μs | 189.661 μs |     ? |       ? |  62.5000 | 54.6875 | 1919.76 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,407.11 μs** |    **51.277 μs** |   **2.811 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,363.93 μs |     9.533 μs |   0.523 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,424.15 μs** |   **159.071 μs** |   **8.719 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,108.47 μs |   132.354 μs |   7.255 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,410.31 μs** |    **38.565 μs** |   **2.114 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,106.56 μs |    33.805 μs |   1.853 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,440.63 μs** |    **17.338 μs** |   **0.950 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,103.48 μs |    19.220 μs |   1.054 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.830 ms** |  **7.990 ms** | **0.4380 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.963 ms | 51.249 ms | 2.8091 ms | 0.006 |  410.11 KB |        5.50 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.572 ms** |  **7.813 ms** | **0.4283 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.671 ms | 38.231 ms | 2.0956 ms | 0.005 |  604.34 KB |        2.41 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.524 ms** | **17.801 ms** | **0.9757 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.976 ms | 30.762 ms | 1.6862 ms | 0.005 |  809.23 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.777 ms** |  **2.082 ms** | **0.1141 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.379 ms | 36.948 ms | 2.0252 ms | 0.005 | 2647.19 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.686 ms** | **17.083 ms** | **0.9364 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.066 ms | 25.014 ms | 1.3711 ms | 0.002 |  182.48 KB |       75.83 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.482 ms** |  **9.552 ms** | **0.5236 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.342 ms | 17.141 ms | 0.9396 ms | 0.002 |  200.38 KB |       48.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.032 ms** | **14.779 ms** | **0.8101 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.409 ms | 12.052 ms | 0.6606 ms | 0.002 |  186.02 KB |       77.31 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.714 ms** | **10.775 ms** | **0.5906 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.761 ms |  9.750 ms | 0.5344 ms | 0.002 |  184.31 KB |       44.10 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.604 μs |  1.8462 μs | 0.1012 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.664 μs |  5.2229 μs | 0.2863 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.646 μs |  2.7552 μs | 0.1510 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.483 μs |  1.9256 μs | 0.1055 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.950 μs |  0.2787 μs | 0.0153 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.311 μs |  2.7281 μs | 0.1495 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.712 μs | 10.9745 μs | 0.6015 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.586 μs |  3.2948 μs | 0.1806 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.615 μs | 15.9220 μs | 0.8727 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,654.0 ns |   5,584.8 ns |    306.12 ns |  0.35 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,326.7 ns |   3,482.1 ns |    190.87 ns |  0.28 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,576.7 ns |   1,754.7 ns |     96.18 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,429.3 ns |   1,099.7 ns |     60.28 ns |  0.73 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    821.0 ns |   1,094.6 ns |     60.00 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46,267.0 ns | 223,067.7 ns | 12,227.09 ns |  9.83 |    2.26 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,711.7 ns |   2,600.0 ns |    142.51 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,254.5 ns |  11,147.0 ns |    611.00 ns |  0.90 |    0.11 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.97 μs |   1.430 μs |  0.078 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   567.13 μs | 469.917 μs | 25.758 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.34 μs |   2.177 μs |  0.119 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,661.03 μs | 493.014 μs | 27.024 μs |    1280 B |


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