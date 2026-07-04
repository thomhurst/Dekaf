---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 19:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,132.28 μs** | **1,075.51 μs** |  **58.952 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,352.75 μs |   421.37 μs |  23.097 μs |  0.22 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,409.84 μs** |   **951.38 μs** |  **52.148 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,340.27 μs |   527.67 μs |  28.923 μs |  0.32 |    0.00 |  15.6250 |       - |  339.53 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,201.44 μs** | **1,192.35 μs** |  **65.357 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,579.82 μs | 8,683.09 μs | 475.950 μs |  0.25 |    0.07 |        - |       - |   36.34 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,891.11 μs** | **1,564.74 μs** |  **85.769 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,881.28 μs | 8,331.02 μs | 456.651 μs |  0.53 |    0.03 |  15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.51 μs** |    **38.14 μs** |   **2.091 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.22 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.26 μs |    73.94 μs |   4.053 μs |  0.47 |    0.02 |        - |       - |    9.17 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,601.64 μs** | **1,563.81 μs** |  **85.718 μs** |  **1.00** |    **0.07** |  **25.3906** |       **-** |  **423.24 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    731.72 μs |   959.51 μs |  52.594 μs |  0.46 |    0.04 |        - |       - |  136.71 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    205.53 μs |   152.92 μs |   8.382 μs |     ? |       ? |   0.9766 |       - |   101.1 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,314.79 μs** |   **737.49 μs** |  **40.424 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** | **1226.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,037.94 μs |   933.28 μs |  51.156 μs |  0.88 |    0.02 |   7.8125 |       - | 1039.22 KB |        0.85 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,430.39 μs** |   **299.56 μs** |  **16.420 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,408.00 μs |   386.74 μs |  21.199 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,423.71 μs** |    **28.72 μs** |   **1.575 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,398.38 μs |   271.24 μs |  14.868 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.95 μs** |    **47.99 μs** |   **2.631 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,411.00 μs |   356.24 μs |  19.527 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,462.71 μs** |   **414.23 μs** |  **22.705 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,382.87 μs |   345.56 μs |  18.941 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.950 ms** |  **20.125 ms** | **1.1031 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.736 ms |  27.720 ms | 1.5194 ms | 0.005 | 591.39 KB |        7.93 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.879 ms** |   **6.779 ms** | **0.3716 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.575 ms |  23.526 ms | 1.2895 ms | 0.005 | 776.22 KB |        3.10 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.289 ms** |  **20.896 ms** | **1.1454 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.767 ms | 144.648 ms | 7.9287 ms | 0.006 | 995.16 KB |        1.65 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.268 ms** |  **35.805 ms** | **1.9626 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.432 ms |  37.359 ms | 2.0478 ms | 0.005 | 2759.8 KB |        1.17 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.090 ms** |  **17.204 ms** | **0.9430 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.535 ms |  12.963 ms | 0.7105 ms | 0.002 | 184.34 KB |       76.61 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,158.066 ms** |  **13.298 ms** | **0.7289 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.555 ms |   9.496 ms | 0.5205 ms | 0.002 | 187.82 KB |       45.11 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,154.695 ms** |  **33.863 ms** | **1.8562 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.528 ms |  14.084 ms | 0.7720 ms | 0.002 | 184.75 KB |       76.78 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.464 ms** |  **47.864 ms** | **2.6236 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.127 ms |   8.023 ms | 0.4398 ms | 0.002 | 186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.719 μs |  1.4504 μs | 0.0795 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.183 μs |  4.4936 μs | 0.2463 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.641 μs |  4.7269 μs | 0.2591 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 36.819 μs | 86.2678 μs | 4.7286 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.254 μs |  0.4162 μs | 0.0228 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.267 μs |  0.9480 μs | 0.0520 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.117 μs |  6.9196 μs | 0.3793 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.682 μs |  5.2150 μs | 0.2859 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.092 μs | 12.8459 μs | 0.7041 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.798 μs |  8.9168 μs | 0.4888 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,450.3 ns | 1,427.3 ns |  78.23 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,372.7 ns | 2,041.4 ns | 111.89 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,406.7 ns | 1,734.6 ns |  95.08 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,699.0 ns | 2,201.1 ns | 120.65 ns |  0.60 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    771.0 ns |   657.8 ns |  36.06 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,731.7 ns | 6,226.5 ns | 341.30 ns |  8.85 |    0.51 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,501.0 ns | 5,285.9 ns | 289.74 ns |  1.00 |    0.08 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,921.3 ns | 2,505.1 ns | 137.31 ns |  0.87 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.878 μs |  12.398 μs |  0.6796 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   544.025 μs | 176.999 μs |  9.7019 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.618 μs |  18.264 μs |  1.0011 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,679.877 μs | 432.045 μs | 23.6818 μs |    1280 B |


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