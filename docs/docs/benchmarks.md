---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 18:48 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,111.13 μs** |   **511.55 μs** |  **28.040 μs** |  **6,124.87 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,312.42 μs | 1,752.60 μs |  96.066 μs |  1,271.06 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,153.34 μs** |   **828.83 μs** |  **45.431 μs** |  **7,149.13 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,312.08 μs |   697.70 μs |  38.243 μs |  2,315.71 μs |  0.32 |    0.00 |  15.6250 |       - |  339.51 KB |        0.32 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,602.76 μs** |   **558.80 μs** |  **30.630 μs** |  **6,597.24 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,249.00 μs | 3,360.46 μs | 184.198 μs |  1,149.29 μs |  0.19 |    0.02 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,838.33 μs** | **7,935.15 μs** | **434.952 μs** | **11,589.78 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,679.11 μs | 1,438.48 μs |  78.848 μs |  5,686.73 μs |  0.48 |    0.02 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.12 μs** |    **44.36 μs** |   **2.431 μs** |    **142.94 μs** |  **1.00** |    **0.02** |   **2.5635** |       **-** |   **42.59 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     47.89 μs |    38.34 μs |   2.102 μs |     47.73 μs |  0.33 |    0.01 |   0.2441 |       - |    9.22 KB |        0.22 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,426.82 μs** | **1,747.67 μs** |  **95.796 μs** |  **1,461.50 μs** |  **1.00** |    **0.08** |  **25.3906** |       **-** |  **426.85 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    587.22 μs |   106.41 μs |   5.833 μs |    584.92 μs |  0.41 |    0.03 |        - |       - |   79.21 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    298.60 μs | 3,658.14 μs | 200.515 μs |    183.53 μs |     ? |       ? |   0.4883 |       - |   11.96 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,129.68 μs | 5,563.06 μs | 304.930 μs |  2,157.40 μs |     ? |       ? |   7.8125 |       - |  992.98 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,422.12 μs** |   **310.92 μs** |  **17.043 μs** |  **5,425.90 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,110.19 μs |   162.86 μs |   8.927 μs |  1,105.52 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,412.79 μs** |    **36.15 μs** |   **1.981 μs** |  **5,412.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,105.26 μs |    21.65 μs |   1.187 μs |  1,104.59 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,417.23 μs** |    **81.04 μs** |   **4.442 μs** |  **5,415.79 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,216.58 μs |   489.93 μs |  26.855 μs |  1,226.98 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,407.85 μs** |   **170.75 μs** |   **9.359 μs** |  **5,411.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,301.30 μs |   446.35 μs |  24.466 μs |  1,296.48 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.672 ms** | **19.426 ms** | **1.0648 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.388 ms | 31.978 ms | 1.7528 ms | 0.005 |  591.04 KB |        7.92 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.893 ms** | **24.666 ms** | **1.3520 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.532 ms |  6.946 ms | 0.3807 ms | 0.005 |  778.91 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.825 ms** | **37.636 ms** | **2.0629 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.423 ms | 27.446 ms | 1.5044 ms | 0.005 | 1049.82 KB |        1.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.429 ms** | **33.800 ms** | **1.8527 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.835 ms | 12.234 ms | 0.6706 ms | 0.006 | 2761.15 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.686 ms** | **20.256 ms** | **1.1103 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.059 ms | 39.715 ms | 2.1769 ms | 0.002 |  186.83 KB |       77.64 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.872 ms** | **24.390 ms** | **1.3369 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.138 ms |  9.818 ms | 0.5382 ms | 0.002 |  187.66 KB |       45.07 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.195 ms** |  **6.990 ms** | **0.3832 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.310 ms | 12.829 ms | 0.7032 ms | 0.002 |  238.81 KB |       99.25 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.304 ms** | **25.341 ms** | **1.3890 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.235 ms |  7.058 ms | 0.3869 ms | 0.002 |  189.24 KB |       45.28 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.799 μs | 22.088 μs | 1.2107 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.017 μs | 14.132 μs | 0.7746 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.359 μs |  4.692 μs | 0.2572 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.359 μs | 26.812 μs | 1.4696 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.346 μs |  1.550 μs | 0.0850 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.220 μs |  4.424 μs | 0.2425 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.923 μs | 16.175 μs | 0.8866 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.939 μs | 20.642 μs | 1.1315 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.493 μs |  8.884 μs | 0.4870 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.548 μs |  9.649 μs | 0.5289 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,278.7 ns |   4,458.1 ns |    244.4 ns |  1,232.0 ns |  0.24 |    0.15 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,247.7 ns |   3,054.6 ns |    167.4 ns |  1,151.0 ns |  0.23 |    0.14 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,339.3 ns |   2,382.9 ns |    130.6 ns |  1,333.0 ns |  0.25 |    0.15 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,487.3 ns |   5,406.6 ns |    296.4 ns |  2,504.0 ns |  0.46 |    0.27 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    637.2 ns |   2,001.3 ns |    109.7 ns |    600.5 ns |  0.12 |    0.07 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,398.8 ns |  24,168.2 ns |  1,324.7 ns | 34,971.5 ns |  6.60 |    3.83 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           | 10,394.8 ns | 206,162.8 ns | 11,300.5 ns |  3,875.5 ns |  1.94 |    2.36 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,826.3 ns |  10,791.0 ns |    591.5 ns |  3,646.0 ns |  0.71 |    0.43 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.25 μs |   9.248 μs |  0.507 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   517.34 μs | 129.916 μs |  7.121 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.85 μs |   5.425 μs |  0.297 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,550.79 μs | 399.334 μs | 21.889 μs |    1280 B |


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