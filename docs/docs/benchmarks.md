---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 20:30 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,119.24 μs** |   **305.48 μs** |  **16.744 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,232.35 μs |   599.97 μs |  32.886 μs |  0.20 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,345.84 μs** | **1,345.80 μs** |  **73.768 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,349.99 μs | 1,080.48 μs |  59.225 μs |  0.32 |    0.01 |  15.6250 |       - |   339.5 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,356.01 μs** | **1,316.38 μs** |  **72.155 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,273.54 μs | 2,633.65 μs | 144.359 μs |  0.20 |    0.02 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,645.98 μs** | **9,419.67 μs** | **516.324 μs** |  **1.00** |    **0.05** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,659.94 μs | 6,789.84 μs | 372.174 μs |  0.53 |    0.03 |  15.6250 |       - |  361.62 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.13 μs** |   **194.22 μs** |  **10.646 μs** |  **1.00** |    **0.10** |   **2.4414** |       **-** |   **39.91 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.32 μs |   258.68 μs |  14.179 μs |  0.54 |    0.10 |        - |       - |    8.51 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,420.25 μs** | **1,037.34 μs** |  **56.860 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |     **421 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    617.45 μs |   523.34 μs |  28.686 μs |  0.44 |    0.02 |        - |       - |   90.88 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    205.48 μs |   552.04 μs |  30.259 μs |     ? |       ? |   0.9766 |       - |  102.25 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,888.66 μs | 3,710.57 μs | 203.389 μs |     ? |       ? |   7.8125 |       - | 1024.74 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,420.44 μs** |    **64.99 μs** |   **3.562 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,401.63 μs |   325.20 μs |  17.825 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,421.33 μs** |    **79.87 μs** |   **4.378 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,355.98 μs |   419.70 μs |  23.005 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.83 μs** |   **192.44 μs** |  **10.548 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,350.84 μs |   633.00 μs |  34.697 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,432.71 μs** |    **34.49 μs** |   **1.891 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,311.89 μs |    97.99 μs |   5.371 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.115 ms** | **34.687 ms** | **1.9013 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.451 ms | 75.184 ms | 4.1211 ms | 0.006 |  595.91 KB |        7.99 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.052 ms** | **12.665 ms** | **0.6942 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.725 ms | 34.624 ms | 1.8978 ms | 0.005 |  778.94 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.979 ms** | **10.762 ms** | **0.5899 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    22.075 ms | 52.187 ms | 2.8606 ms | 0.007 |  997.55 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.617 ms** | **23.585 ms** | **1.2928 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.990 ms | 23.001 ms | 1.2608 ms | 0.005 | 2783.22 KB |        1.18 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.063 ms** | **30.464 ms** | **1.6698 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.545 ms | 13.403 ms | 0.7347 ms | 0.002 |  184.45 KB |       76.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.541 ms** | **11.613 ms** | **0.6365 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.182 ms |  2.827 ms | 0.1550 ms | 0.002 |  195.53 KB |       46.96 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.948 ms** | **25.970 ms** | **1.4235 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.697 ms | 10.408 ms | 0.5705 ms | 0.002 |   184.7 KB |       76.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.663 ms** | **19.476 ms** | **1.0676 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.401 ms | 23.036 ms | 1.2627 ms | 0.002 |  206.41 KB |       49.38 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.594 μs |  9.654 μs | 0.5292 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.996 μs |  4.135 μs | 0.2266 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.063 μs |  6.800 μs | 0.3727 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.706 μs |  2.755 μs | 0.1510 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.196 μs |  2.583 μs | 0.1416 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.380 μs | 21.691 μs | 1.1890 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.652 μs | 45.475 μs | 2.4926 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.861 μs |  7.416 μs | 0.4065 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.405 μs |  7.182 μs | 0.3937 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.793 μs |  7.515 μs | 0.4119 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,289.3 ns |  2,587.8 ns |   141.85 ns |  0.30 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,275.3 ns |  1,695.1 ns |    92.92 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,405.5 ns |  5,039.3 ns |   276.22 ns |  0.33 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,620.7 ns |  2,949.2 ns |   161.66 ns |  0.61 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    684.3 ns |  1,519.1 ns |    83.27 ns |  0.16 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 44,354.7 ns | 87,619.4 ns | 4,802.71 ns | 10.39 |    1.04 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,273.3 ns |  3,112.2 ns |   170.59 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,132.8 ns |  5,756.2 ns |   315.52 ns |  0.97 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.614 μs |  20.542 μs |  1.1260 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   514.649 μs | 195.501 μs | 10.7161 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.241 μs |   3.564 μs |  0.1954 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,553.483 μs | 260.286 μs | 14.2671 μs |    1280 B |


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