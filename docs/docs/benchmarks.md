---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 16:40 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,155.84 μs** | **16,916.13 μs** |   **927.230 μs** | **5,684.85 μs** |  **1.01** |    **0.18** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,087.42 μs |     33.83 μs |     1.854 μs | 1,086.42 μs |  0.18 |    0.02 |   1.9531 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,116.48 μs** | **15,545.42 μs** |   **852.097 μs** | **6,706.97 μs** |  **1.01** |    **0.14** |  **62.5000** | **23.4375** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 1,128.55 μs |    372.56 μs |    20.421 μs | 1,134.82 μs |  0.16 |    0.02 |  19.5313 |  3.9063 |  339.33 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,355.23 μs** | **11,082.02 μs** |   **607.443 μs** | **6,018.55 μs** |  **1.01** |    **0.11** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,100.99 μs |     34.22 μs |     1.876 μs | 1,101.19 μs |  0.17 |    0.01 |   1.9531 |       - |    36.3 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,015.15 μs** |  **5,444.84 μs** |   **298.450 μs** | **6,960.85 μs** |  **1.00** |    **0.05** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,539.79 μs | 21,391.63 μs | 1,172.547 μs | 3,931.14 μs |  0.65 |    0.15 |  19.5313 |  3.9063 |   361.7 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **92.32 μs** |    **492.48 μs** |    **26.995 μs** |   **107.50 μs** |  **1.07** |    **0.43** |   **2.4414** |       **-** |   **40.66 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    25.19 μs |     30.27 μs |     1.659 μs |    24.25 μs |  0.29 |    0.09 |   0.2441 |       - |    9.35 KB |        0.23 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   264.98 μs |    300.85 μs |    16.491 μs |   271.28 μs |     ? |       ? |   2.9297 |  0.9766 |  184.42 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   592.76 μs |    433.46 μs |    23.759 μs |   593.05 μs |     ? |       ? |   0.7324 |       - |   12.99 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,954.82 μs | 40,199.43 μs | 2,203.466 μs | 2,171.77 μs |     ? |       ? |   3.9063 |       - |  106.23 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,218.14 μs** |     **74.79 μs** |     **4.099 μs** | **5,218.87 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,245.88 μs |  2,340.61 μs |   128.297 μs | 1,174.00 μs |  0.24 |    0.02 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,410.06 μs** |  **6,118.15 μs** |   **335.356 μs** | **5,217.05 μs** |  **1.00** |    **0.07** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,189.81 μs |    216.58 μs |    11.871 μs | 1,190.11 μs |  0.22 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,394.44 μs** |  **4,583.22 μs** |   **251.222 μs** | **5,274.78 μs** |  **1.00** |    **0.06** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,404.11 μs |  6,216.05 μs |   340.723 μs | 1,217.84 μs |  0.26 |    0.06 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,225.27 μs** |    **167.44 μs** |     **9.178 μs** | **5,228.25 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,236.73 μs |    487.29 μs |    26.710 μs | 1,243.23 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,162.271 ms** |  **12.111 ms** |  **0.6638 ms** | **3,162.616 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    22.046 ms | 329.169 ms | 18.0429 ms |    13.738 ms | 0.007 |  595.82 KB |        7.99 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.660 ms** |  **84.540 ms** |  **4.6339 ms** | **3,163.750 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    10.487 ms |  12.872 ms |  0.7056 ms |    10.810 ms | 0.003 |  779.28 KB |        3.11 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,162.191 ms** |   **6.380 ms** |  **0.3497 ms** | **3,162.116 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    11.291 ms |  27.378 ms |  1.5007 ms |    10.458 ms | 0.004 |  997.91 KB |        1.66 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,162.320 ms** |  **39.745 ms** |  **2.1786 ms** | **3,161.880 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    12.298 ms |   4.378 ms |  0.2399 ms |    12.424 ms | 0.004 | 2805.89 KB |        1.19 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.596 ms** |  **11.704 ms** |  **0.6415 ms** | **3,154.575 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     4.937 ms |  16.095 ms |  0.8822 ms |     4.432 ms | 0.002 |  185.78 KB |       77.21 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.241 ms** |  **46.998 ms** |  **2.5761 ms** | **3,156.102 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.315 ms |   6.859 ms |  0.3759 ms |     4.331 ms | 0.001 |  186.35 KB |       44.75 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.634 ms** |  **29.472 ms** |  **1.6155 ms** | **3,155.473 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     4.573 ms |  10.788 ms |  0.5913 ms |     4.762 ms | 0.001 |  261.17 KB |      108.54 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.943 ms** |  **16.138 ms** |  **0.8846 ms** | **3,155.706 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     4.188 ms |   6.793 ms |  0.3724 ms |     4.020 ms | 0.001 |  227.77 KB |       54.49 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.128 μs |  54.816 μs |  3.0046 μs | 30.838 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.390 μs |   2.936 μs |  0.1609 μs | 10.340 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.704 μs |   1.991 μs |  0.1091 μs | 10.740 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.931 μs |   1.026 μs |  0.0562 μs | 26.921 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.017 μs |   2.107 μs |  0.1155 μs |  9.058 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.855 μs |  78.619 μs |  4.3093 μs | 20.418 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 26.119 μs | 253.134 μs | 13.8751 μs | 18.325 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.766 μs |   8.623 μs |  0.4727 μs | 20.720 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.599 μs |   2.586 μs |  0.1418 μs |  4.549 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.932 μs |   2.828 μs |  0.1550 μs | 10.995 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,384.5 ns | 1,663.8 ns |  91.20 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,413.0 ns | 2,392.6 ns | 131.15 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,333.0 ns |   965.4 ns |  52.92 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,831.7 ns | 6,576.0 ns | 360.45 ns |  0.67 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    787.7 ns |   379.8 ns |  20.82 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,452.3 ns | 5,121.9 ns | 280.75 ns |  9.56 |    0.31 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,237.5 ns | 2,936.0 ns | 160.93 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,054.0 ns | 1,183.3 ns |  64.86 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.345 μs |   8.985 μs |  0.4925 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   532.111 μs | 468.624 μs | 25.6869 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.033 μs |  16.408 μs |  0.8994 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,673.871 μs | 333.788 μs | 18.2960 μs |    1280 B |


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