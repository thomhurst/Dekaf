---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 15:36 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,375.38 μs** |   **797.103 μs** |  **43.692 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,603.29 μs | 3,823.925 μs | 209.602 μs |  0.25 |    0.03 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,514.37 μs** | **2,246.651 μs** | **123.147 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,404.25 μs |   888.591 μs |  48.707 μs |  0.32 |    0.01 |  15.6250 |       - |  339.51 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,194.79 μs** |   **142.134 μs** |   **7.791 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,267.55 μs | 1,515.053 μs |  83.045 μs |  0.20 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,163.64 μs** | **3,497.302 μs** | **191.699 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,586.77 μs | 4,327.193 μs | 237.188 μs |  0.50 |    0.02 |  15.6250 |       - |  361.74 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.73 μs** |    **90.554 μs** |   **4.964 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **42.29 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     52.94 μs |     7.108 μs |   0.390 μs |  0.38 |    0.01 |   0.2441 |       - |    9.41 KB |        0.22 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,399.35 μs** |   **664.138 μs** |  **36.404 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **398.07 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    596.22 μs | 1,514.358 μs |  83.007 μs |  0.43 |    0.05 |   1.9531 |       - |   87.97 KB |        0.22 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    199.07 μs |   280.476 μs |  15.374 μs |     ? |       ? |   0.9766 |       - |  102.66 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,035.76 μs |   389.060 μs |  21.326 μs |     ? |       ? |   7.8125 |       - |  973.49 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,418.03 μs** |    **38.088 μs** |   **2.088 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,362.66 μs |   495.752 μs |  27.174 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,420.07 μs** |    **37.382 μs** |   **2.049 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,375.18 μs |   343.429 μs |  18.825 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,426.62 μs** |    **91.870 μs** |   **5.036 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,347.00 μs |    72.830 μs |   3.992 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,420.09 μs** |    **48.343 μs** |   **2.650 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,352.24 μs |   285.584 μs |  15.654 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.671 ms** | **22.059 ms** | **1.2091 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.067 ms | 45.105 ms | 2.4724 ms | 0.006 |  591.24 KB |        7.92 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.530 ms** | **22.784 ms** | **1.2489 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.966 ms | 11.115 ms | 0.6093 ms | 0.004 |  776.43 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.697 ms** |  **3.558 ms** | **0.1950 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.554 ms | 83.930 ms | 4.6005 ms | 0.006 |  993.91 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.760 ms** |  **8.460 ms** | **0.4637 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.192 ms | 50.340 ms | 2.7593 ms | 0.006 | 2763.42 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,158.948 ms** | **96.284 ms** | **5.2776 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.439 ms | 18.152 ms | 0.9950 ms | 0.002 |  185.52 KB |       77.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.191 ms** | **42.977 ms** | **2.3557 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.702 ms |  4.658 ms | 0.2553 ms | 0.002 |  186.32 KB |       44.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.855 ms** | **36.839 ms** | **2.0193 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.703 ms | 24.195 ms | 1.3262 ms | 0.002 |     186 KB |       77.30 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.505 ms** |  **5.164 ms** | **0.2830 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.810 ms | 11.282 ms | 0.6184 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.768 μs |   1.196 μs |  0.0656 μs | 25.758 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.054 μs |   2.107 μs |  0.1155 μs | 10.095 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.436 μs |   5.999 μs |  0.3288 μs | 10.348 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.854 μs | 197.665 μs | 10.8347 μs | 26.649 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.480 μs |  69.821 μs |  3.8271 μs | 15.614 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.493 μs |  68.213 μs |  3.7390 μs | 20.359 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.009 μs |   3.433 μs |  0.1881 μs | 17.973 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 26.359 μs | 191.774 μs | 10.5118 μs | 20.819 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.432 μs |   2.317 μs |  0.1270 μs |  4.359 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.766 μs |   2.631 μs |  0.1442 μs | 10.726 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,333.0 ns |  2,554.1 ns |   140.00 ns |  0.27 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,256.7 ns |  1,267.9 ns |    69.50 ns |  0.25 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,423.0 ns |  1,796.8 ns |    98.49 ns |  0.28 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,585.3 ns |    921.3 ns |    50.50 ns |  0.51 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    894.7 ns |  3,400.4 ns |   186.39 ns |  0.18 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 43,113.7 ns | 90,053.9 ns | 4,936.16 ns |  8.58 |    0.89 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,026.7 ns |  3,375.5 ns |   185.02 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,561.7 ns |  3,562.0 ns |   195.24 ns |  0.91 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev     | Allocated |
|------------------------ |-------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.021 μs |     4.763 μs |  0.2611 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   577.399 μs | 1,057.329 μs | 57.9558 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.743 μs |     2.107 μs |  0.1155 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,762.597 μs |   419.445 μs | 22.9912 μs |    1280 B |


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