---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 17:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,180.81 μs** |   **322.07 μs** |  **17.654 μs** |  **1.00** |    **0.00** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,386.23 μs |   495.24 μs |  27.146 μs |  0.22 |    0.00 |        - |        - |   35.03 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,384.34 μs** | **1,299.17 μs** |  **71.212 μs** |  **1.00** |    **0.01** |  **62.5000** |  **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,400.44 μs |   538.68 μs |  29.527 μs |  0.33 |    0.00 |  15.6250 |        - |  340.07 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,284.56 μs** |   **317.73 μs** |  **17.416 μs** |  **1.00** |    **0.00** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,354.15 μs | 2,753.55 μs | 150.931 μs |  0.22 |    0.02 |        - |        - |   37.22 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,952.14 μs** | **3,734.72 μs** | **204.713 μs** |  **1.00** |    **0.02** | **109.3750** |  **31.2500** | **1937.94 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,948.24 μs | 4,184.92 μs | 229.389 μs |  0.54 |    0.02 |  15.6250 |        - |  372.24 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.99 μs** |    **90.13 μs** |   **4.940 μs** |  **1.00** |    **0.04** |   **2.4414** |        **-** |   **42.17 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     73.95 μs |   199.01 μs |  10.909 μs |  0.54 |    0.07 |   0.9766 |   0.4883 |   22.92 KB |        0.54 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,388.16 μs** |   **275.65 μs** |  **15.109 μs** |  **1.00** |    **0.01** |  **23.4375** |        **-** |  **407.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    744.67 μs | 1,767.27 μs |  96.870 μs |  0.54 |    0.06 |   7.8125 |   3.9063 |  218.54 KB |        0.54 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    369.99 μs |   426.59 μs |  23.383 μs |     ? |       ? |  11.7188 |  10.7422 |  263.42 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,614.80 μs | 3,787.05 μs | 207.581 μs |     ? |       ? | 117.1875 | 109.3750 | 2574.65 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,426.87 μs** |    **38.62 μs** |   **2.117 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,396.78 μs |   326.82 μs |  17.914 μs |  0.26 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,432.43 μs** |   **129.58 μs** |   **7.102 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,378.69 μs |   320.82 μs |  17.585 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,671.39 μs** | **7,368.61 μs** | **403.898 μs** |  **1.00** |    **0.09** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,368.05 μs |   381.18 μs |  20.894 μs |  0.24 |    0.01 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,434.61 μs** |    **35.63 μs** |   **1.953 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,249.09 μs |   685.85 μs |  37.594 μs |  0.23 |    0.01 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.894 ms** | **18.265 ms** | **1.0011 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.141 ms | 35.640 ms | 1.9536 ms | 0.005 |  408.38 KB |        5.47 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.581 ms** | **22.107 ms** | **1.2117 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.872 ms | 37.279 ms | 2.0434 ms | 0.004 |  594.32 KB |        2.37 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.712 ms** | **18.457 ms** | **1.0117 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.303 ms | 37.924 ms | 2.0787 ms | 0.005 |  810.15 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.388 ms** | **11.767 ms** | **0.6450 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.583 ms | 20.697 ms | 1.1345 ms | 0.004 | 2576.24 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.131 ms** | **14.008 ms** | **0.7678 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.221 ms | 19.599 ms | 1.0743 ms | 0.002 |  185.82 KB |       77.22 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.645 ms** | **18.425 ms** | **1.0099 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.425 ms | 13.591 ms | 0.7450 ms | 0.002 |  184.65 KB |       44.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.615 ms** | **13.646 ms** | **0.7480 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.259 ms | 30.490 ms | 1.6713 ms | 0.002 |  181.48 KB |       75.42 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.411 ms** | **22.653 ms** | **1.2417 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.457 ms | 10.450 ms | 0.5728 ms | 0.002 |  183.92 KB |       44.00 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 20.170 μs |  80.494 μs | 4.4121 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.323 μs |   5.594 μs | 0.3066 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.961 μs |  26.087 μs | 1.4299 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.875 μs |   5.080 μs | 0.2785 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.958 μs |   2.841 μs | 0.1557 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.751 μs | 134.449 μs | 7.3696 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.495 μs |  18.950 μs | 1.0387 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.117 μs |  30.904 μs | 1.6939 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.613 μs |   4.851 μs | 0.2659 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.502 μs |  14.162 μs | 0.7763 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,368.0 ns |   2,186.7 ns |    119.9 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,460.5 ns |   2,718.1 ns |    149.0 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,384.0 ns |   2,571.5 ns |    141.0 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,807.7 ns |   4,688.7 ns |    257.0 ns |  0.65 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    542.2 ns |   4,145.5 ns |    227.2 ns |  0.13 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 44,963.3 ns | 286,683.5 ns | 15,714.1 ns | 10.41 |    3.20 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,329.7 ns |   4,638.4 ns |    254.2 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,032.7 ns |   7,793.4 ns |    427.2 ns |  0.93 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.112 μs |  15.891 μs |  0.8710 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   547.874 μs | 200.849 μs | 11.0092 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.573 μs |   7.661 μs |  0.4199 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,578.469 μs | 403.434 μs | 22.1136 μs |    1280 B |


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