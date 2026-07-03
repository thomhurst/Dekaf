---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 02:44 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,059.51 μs** |   **490.96 μs** |  **26.911 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,249.54 μs |   588.27 μs |  32.245 μs |  0.21 |    0.00 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,246.92 μs** | **2,292.01 μs** | **125.633 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,227.16 μs |   175.58 μs |   9.624 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.85 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,677.62 μs** |   **563.42 μs** |  **30.883 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,132.86 μs |   473.37 μs |  25.947 μs |  0.17 |    0.00 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,496.92 μs** | **2,055.99 μs** | **112.696 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,290.36 μs | 6,654.75 μs | 364.769 μs |  0.55 |    0.03 |  15.6250 |       - |  369.21 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.04 μs** |    **81.06 μs** |   **4.443 μs** |  **1.00** |    **0.04** |   **2.5635** |       **-** |   **42.29 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.62 μs |   172.53 μs |   9.457 μs |  0.47 |    0.07 |   0.4883 |  0.2441 |   15.95 KB |        0.38 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,300.28 μs** | **1,316.46 μs** |  **72.159 μs** |  **1.00** |    **0.07** |  **23.4375** |       **-** |  **409.94 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    654.93 μs | 1,347.31 μs |  73.850 μs |  0.50 |    0.05 |   3.9063 |       - |   110.9 KB |        0.27 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    338.02 μs |   344.91 μs |  18.906 μs |     ? |       ? |   6.8359 |  5.8594 |  199.21 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,199.26 μs |   832.64 μs |  45.640 μs |     ? |       ? |  62.5000 | 54.6875 | 1930.31 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,405.22 μs** |   **100.47 μs** |   **5.507 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,113.87 μs |    56.80 μs |   3.113 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,402.70 μs** |    **77.05 μs** |   **4.223 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,372.89 μs |   554.28 μs |  30.382 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,399.48 μs** |    **62.56 μs** |   **3.429 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,297.58 μs |   307.63 μs |  16.862 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,403.89 μs** |    **88.96 μs** |   **4.876 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.98 μs |   270.98 μs |  14.853 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.289 ms** | **18.605 ms** | **1.0198 ms** | **3,170.301 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.463 ms | 71.854 ms | 3.9386 ms |    14.061 ms | 0.005 |  403.55 KB |        5.41 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.548 ms** | **20.295 ms** | **1.1124 ms** | **3,166.063 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.460 ms |  9.228 ms | 0.5058 ms |    12.581 ms | 0.004 |   585.7 KB |        2.34 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.996 ms** | **10.722 ms** | **0.5877 ms** | **3,165.725 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    13.220 ms | 21.850 ms | 1.1977 ms |    13.161 ms | 0.004 |  897.35 KB |        1.49 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.744 ms** | **12.647 ms** | **0.6932 ms** | **3,165.844 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.557 ms | 20.812 ms | 1.1408 ms |    12.916 ms | 0.004 | 2571.17 KB |        1.09 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.282 ms** | **21.525 ms** | **1.1798 ms** | **3,154.386 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.020 ms |  6.605 ms | 0.3620 ms |     5.907 ms | 0.002 |   194.2 KB |       80.70 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.884 ms** | **15.542 ms** | **0.8519 ms** | **3,156.028 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.214 ms | 55.147 ms | 3.0228 ms |     5.728 ms | 0.002 |  182.51 KB |       43.83 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.559 ms** | **62.645 ms** | **3.4338 ms** | **3,156.544 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.136 ms | 23.689 ms | 1.2985 ms |     6.415 ms | 0.002 |  201.18 KB |       83.61 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.517 ms** | **17.000 ms** | **0.9318 ms** | **3,157.629 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.761 ms | 30.393 ms | 1.6659 ms |     6.323 ms | 0.002 |  183.22 KB |       43.84 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.657 μs |   0.8360 μs |  0.0458 μs | 14.667 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.465 μs |   4.0150 μs |  0.2201 μs |  9.472 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.289 μs |   7.2609 μs |  0.3980 μs | 10.138 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.887 μs |   3.0411 μs |  0.1667 μs | 26.941 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.947 μs |   2.1040 μs |  0.1153 μs |  8.987 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.473 μs |   0.5320 μs |  0.0292 μs | 19.457 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 25.943 μs | 260.3977 μs | 14.2733 μs | 17.727 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  5.523 μs |   1.8274 μs |  0.1002 μs |  5.560 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.817 μs |   8.3336 μs |  0.4568 μs | 11.071 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,305.3 ns |   3,231.5 ns |    177.13 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,405.7 ns |   1,873.9 ns |    102.71 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,513.7 ns |   1,419.1 ns |     77.78 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,783.5 ns |   4,050.2 ns |    222.01 ns |  0.65 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    767.8 ns |     450.7 ns |     24.70 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 49,190.2 ns | 184,220.9 ns | 10,097.77 ns | 11.54 |    2.10 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,268.7 ns |   3,640.1 ns |    199.53 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,201.0 ns |   8,951.5 ns |    490.66 ns |  0.99 |    0.11 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev     | Allocated |
|------------------------ |------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.18 μs |     3.475 μs |   0.191 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   569.09 μs |   525.846 μs |  28.823 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.11 μs |     4.612 μs |   0.253 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,724.39 μs | 2,691.079 μs | 147.507 μs |    1280 B |


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