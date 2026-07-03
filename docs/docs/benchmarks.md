---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 01:02 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,096.34 μs** |   **287.30 μs** |  **15.748 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,375.84 μs |   620.32 μs |  34.002 μs |  0.23 |    0.00 |        - |       - |   34.93 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,184.56 μs** |   **350.54 μs** |  **19.214 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,241.48 μs |    90.49 μs |   4.960 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.88 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,623.73 μs** |   **860.83 μs** |  **47.185 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,193.67 μs | 1,366.04 μs |  74.877 μs |  0.18 |    0.01 |        - |       - |   36.94 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,605.81 μs** | **1,476.45 μs** |  **80.929 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,451.80 μs | 9,664.20 μs | 529.727 μs |  0.56 |    0.04 |  15.6250 |       - |  369.43 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **150.61 μs** |   **330.64 μs** |  **18.124 μs** |  **1.01** |    **0.15** |   **2.5635** |       **-** |   **42.08 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     73.03 μs |    59.53 μs |   3.263 μs |  0.49 |    0.05 |   0.4883 |       - |   14.36 KB |        0.34 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,217.64 μs** |   **610.91 μs** |  **33.486 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **406.83 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    714.79 μs | 1,366.72 μs |  74.914 μs |  0.59 |    0.06 |   3.9063 |       - |  110.29 KB |        0.27 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    325.69 μs |   193.41 μs |  10.602 μs |     ? |       ? |   5.8594 |  4.8828 |  188.95 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,260.79 μs | 2,880.41 μs | 157.885 μs |     ? |       ? |  62.5000 | 54.6875 | 1941.76 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,432.20 μs** |   **927.59 μs** |  **50.844 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,192.49 μs |   243.73 μs |  13.360 μs |  0.22 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,429.67 μs** |    **82.41 μs** |   **4.517 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,105.26 μs |    60.82 μs |   3.334 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,457.46 μs** |   **227.11 μs** |  **12.448 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,336.25 μs |   299.66 μs |  16.426 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.21 μs** |    **35.98 μs** |   **1.972 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,126.95 μs |   714.71 μs |  39.175 μs |  0.21 |    0.01 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.363 ms** | **29.787 ms** | **1.6327 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.706 ms | 50.290 ms | 2.7565 ms | 0.005 |  441.11 KB |        5.91 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.234 ms** |  **7.524 ms** | **0.4124 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.731 ms |  7.149 ms | 0.3919 ms | 0.004 |  849.88 KB |        3.39 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.122 ms** | **10.103 ms** | **0.5538 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.828 ms | 37.286 ms | 2.0438 ms | 0.005 | 1070.83 KB |        1.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.522 ms** | **13.711 ms** | **0.7515 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.634 ms | 32.316 ms | 1.7713 ms | 0.005 | 5651.33 KB |        2.39 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.984 ms** | **42.455 ms** | **2.3271 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.677 ms |  6.750 ms | 0.3700 ms | 0.002 |   245.2 KB |      101.90 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.959 ms** | **16.068 ms** | **0.8807 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.385 ms |  1.764 ms | 0.0967 ms | 0.002 |  693.77 KB |      166.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.841 ms** | **35.092 ms** | **1.9235 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.468 ms | 13.332 ms | 0.7308 ms | 0.002 |  694.31 KB |      288.55 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.964 ms** |  **3.755 ms** | **0.2058 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.742 ms | 20.439 ms | 1.1203 ms | 0.002 | 2230.45 KB |      533.64 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 15.400 μs |   2.207 μs | 0.1210 μs | 15.443 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.878 μs |   5.169 μs | 0.2833 μs |  9.724 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 15.582 μs | 139.178 μs | 7.6288 μs | 11.446 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.729 μs |  11.153 μs | 0.6114 μs | 32.548 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 10.796 μs |  59.511 μs | 3.2620 μs |  8.973 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.756 μs |   7.589 μs | 0.4160 μs | 21.733 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.167 μs |  16.316 μs | 0.8944 μs | 17.787 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.623 μs |   9.728 μs | 0.5332 μs |  4.356 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.586 μs |   7.489 μs | 0.4105 μs | 10.516 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,555.7 ns | 2,645.7 ns | 145.02 ns |  0.38 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,392.3 ns | 1,025.6 ns |  56.22 ns |  0.34 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,214.7 ns | 3,759.7 ns | 206.08 ns |  0.30 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,933.0 ns | 1,374.6 ns |  75.35 ns |  0.72 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    775.0 ns | 1,015.3 ns |  55.65 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 36,145.7 ns | 9,011.6 ns | 493.95 ns |  8.83 |    0.95 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,133.0 ns | 8,902.3 ns | 487.96 ns |  1.01 |    0.15 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,832.3 ns | 9,127.0 ns | 500.28 ns |  0.94 |    0.15 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.525 μs |  20.611 μs | 1.1298 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   512.246 μs |  26.228 μs | 1.4377 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.311 μs |   8.221 μs | 0.4506 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,583.789 μs | 140.759 μs | 7.7155 μs |    1280 B |


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