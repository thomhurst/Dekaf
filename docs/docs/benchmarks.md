---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 08:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,073.06 μs** |    **144.46 μs** |   **7.919 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,322.24 μs |    915.64 μs |  50.190 μs |  0.22 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,312.70 μs** |  **1,462.19 μs** |  **80.148 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,330.99 μs |    284.64 μs |  15.602 μs |  0.32 |    0.00 |  15.6250 |       - |  339.84 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,563.09 μs** |    **771.37 μs** |  **42.282 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,160.08 μs |    702.72 μs |  38.518 μs |  0.18 |    0.01 |        - |       - |   36.93 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,165.66 μs** |  **3,352.26 μs** | **183.748 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,915.34 μs | 11,186.53 μs | 613.171 μs |  0.57 |    0.04 |  15.6250 |       - |  369.42 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.44 μs** |    **132.82 μs** |   **7.280 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |      **42 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     60.11 μs |     25.09 μs |   1.375 μs |  0.42 |    0.02 |   0.7324 |  0.4883 |   18.01 KB |        0.43 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,398.58 μs** |    **876.17 μs** |  **48.026 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |  **418.13 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    702.89 μs |    636.41 μs |  34.884 μs |  0.50 |    0.03 |   3.9063 |       - |   116.7 KB |        0.28 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    300.91 μs |    514.46 μs |  28.199 μs |     ? |       ? |   6.8359 |  5.8594 |  204.23 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,318.77 μs |  3,068.55 μs | 168.198 μs |     ? |       ? |  62.5000 | 54.6875 | 1892.67 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,443.96 μs** |    **167.83 μs** |   **9.199 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,114.47 μs |     65.70 μs |   3.602 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,410.45 μs** |    **357.86 μs** |  **19.615 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,114.09 μs |     39.76 μs |   2.179 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,460.58 μs** |    **683.27 μs** |  **37.452 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,466.73 μs |  1,305.32 μs |  71.549 μs |  0.27 |    0.01 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,420.86 μs** |     **36.89 μs** |   **2.022 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,383.22 μs |    673.79 μs |  36.933 μs |  0.26 |    0.01 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error         | StdDev      | Median       | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|--------------:|------------:|-------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.647 ms** |    **14.3212 ms** |   **0.7850 ms** | **3,168.525 ms** | **1.000** |    **0.00** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.068 ms |    52.8507 ms |   2.8969 ms |    17.887 ms | 0.005 |    0.00 |  410.99 KB |        5.51 |
|                      |            |              |             |              |               |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.331 ms** |    **10.2860 ms** |   **0.5638 ms** | **3,166.122 ms** | **1.000** |    **0.00** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.915 ms |    21.6880 ms |   1.1888 ms |    12.343 ms | 0.004 |    0.00 |  590.52 KB |        2.36 |
|                      |            |              |             |              |               |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.230 ms** |    **22.1458 ms** |   **1.2139 ms** | **3,168.373 ms** | **1.000** |    **0.00** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.983 ms |    49.2926 ms |   2.7019 ms |    16.281 ms | 0.005 |    0.00 |  809.52 KB |        1.34 |
|                      |            |              |             |              |               |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.475 ms** |    **36.8760 ms** |   **2.0213 ms** | **3,166.250 ms** | **1.000** |    **0.00** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.939 ms |    29.7790 ms |   1.6323 ms |    14.322 ms | 0.005 |    0.00 | 2653.74 KB |        1.12 |
|                      |            |              |             |              |               |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.797 ms** |    **28.4066 ms** |   **1.5571 ms** | **3,154.581 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.443 ms |     7.7502 ms |   0.4248 ms |     5.552 ms | 0.002 |    0.00 |  180.22 KB |       74.90 |
|                      |            |              |             |              |               |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.141 ms** |     **0.9724 ms** |   **0.0533 ms** | **3,156.133 ms** | **1.000** |    **0.00** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.306 ms |    21.6915 ms |   1.1890 ms |     5.720 ms | 0.002 |    0.00 |  183.19 KB |       43.99 |
|                      |            |              |             |              |               |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,154.837 ms** |    **24.5888 ms** |   **1.3478 ms** | **3,155.403 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.717 ms |     4.3913 ms |   0.2407 ms |     6.731 ms | 0.002 |    0.00 |  200.96 KB |       83.52 |
|                      |            |              |             |              |               |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.196 ms** |    **11.8779 ms** |   **0.6511 ms** | **3,157.862 ms** |  **1.00** |    **0.00** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |    72.708 ms | 2,141.6466 ms | 117.3909 ms |     4.990 ms |  0.02 |    0.03 |  260.41 KB |       62.30 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.284 μs |   2.740 μs |  0.1502 μs | 14.327 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.799 μs |   2.393 μs |  0.1311 μs |  9.819 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.100 μs |   2.538 μs |  0.1391 μs | 10.180 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.607 μs |   1.889 μs |  0.1035 μs | 26.640 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.813 μs |   1.755 μs |  0.0962 μs |  8.796 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 23.307 μs |  64.388 μs |  3.5293 μs | 25.187 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 26.904 μs | 288.875 μs | 15.8342 μs | 18.344 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.422 μs |   1.552 μs |  0.0850 μs |  4.419 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.290 μs |   8.800 μs |  0.4823 μs | 10.200 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,258.7 ns |     557.4 ns |     30.55 ns |  0.28 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,392.0 ns |   4,186.2 ns |    229.46 ns |  0.31 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,312.0 ns |     182.4 ns |     10.00 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,363.0 ns |  11,463.8 ns |    628.37 ns |  0.76 |    0.13 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    779.8 ns |   3,585.9 ns |    196.55 ns |  0.18 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 49,472.8 ns | 295,468.6 ns | 16,195.63 ns | 11.12 |    3.18 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,451.8 ns |   3,213.9 ns |    176.16 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,618.8 ns |   5,231.5 ns |    286.76 ns |  1.04 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.45 μs |   1.561 μs |  0.086 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   539.27 μs | 907.587 μs | 49.748 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.83 μs |  28.619 μs |  1.569 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,722.97 μs |  69.856 μs |  3.829 μs |    1280 B |


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