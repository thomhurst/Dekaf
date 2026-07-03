---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 17:50 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,195.38 μs** |   **576.40 μs** |  **31.595 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,381.06 μs | 1,244.00 μs |  68.188 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,409.73 μs** | **1,159.41 μs** |  **63.551 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,341.04 μs |   696.76 μs |  38.192 μs |  0.32 |    0.01 |  15.6250 |       - |  339.65 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,127.89 μs** |   **347.67 μs** |  **19.057 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,433.13 μs | 2,291.61 μs | 125.611 μs |  0.23 |    0.02 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,891.72 μs** |   **913.06 μs** |  **50.048 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,228.14 μs | 1,400.98 μs |  76.793 μs |  0.48 |    0.01 |  15.6250 |       - |  361.91 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.05 μs** |    **35.58 μs** |   **1.950 μs** |  **1.00** |    **0.02** |   **2.1973** |       **-** |    **38.9 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.06 μs |    71.61 μs |   3.925 μs |  0.46 |    0.02 |        - |       - |    9.52 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,481.11 μs** | **2,472.65 μs** | **135.534 μs** |  **1.01** |    **0.11** |  **23.4375** |       **-** |  **409.31 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    619.81 μs |   410.56 μs |  22.504 μs |  0.42 |    0.04 |        - |       - |   79.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    255.57 μs | 1,840.17 μs | 100.866 μs |     ? |       ? |   0.4883 |       - |   11.81 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,116.57 μs | 2,553.39 μs | 139.960 μs |     ? |       ? |   7.8125 |       - | 1028.79 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,422.98 μs** |    **85.84 μs** |   **4.705 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,118.37 μs |    38.11 μs |   2.089 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,462.47 μs** |   **217.68 μs** |  **11.932 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,117.65 μs |    17.46 μs |   0.957 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,457.56 μs** |    **57.40 μs** |   **3.146 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,293.30 μs | 1,681.50 μs |  92.168 μs |  0.24 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,460.38 μs** |   **181.33 μs** |   **9.940 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,394.31 μs |   237.29 μs |  13.006 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.770 ms** | **37.487 ms** | **2.0548 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.612 ms | 23.828 ms | 1.3061 ms | 0.005 |  420.16 KB |        5.63 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.729 ms** |  **6.748 ms** | **0.3699 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.787 ms | 36.850 ms | 2.0199 ms | 0.004 |  594.02 KB |        2.37 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.695 ms** | **19.228 ms** | **1.0540 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.771 ms | 41.463 ms | 2.2727 ms | 0.005 |  813.65 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.603 ms** | **24.604 ms** | **1.3486 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.205 ms | 51.090 ms | 2.8004 ms | 0.005 | 2580.58 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.016 ms** | **12.210 ms** | **0.6693 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.977 ms | 14.896 ms | 0.8165 ms | 0.002 |  182.35 KB |       75.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.360 ms** | **39.828 ms** | **2.1831 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.429 ms |  2.845 ms | 0.1560 ms | 0.002 |  193.36 KB |       46.44 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.971 ms** | **22.220 ms** | **1.2179 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.571 ms |  4.903 ms | 0.2688 ms | 0.002 |  254.56 KB |      105.79 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.854 ms** | **25.756 ms** | **1.4118 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.729 ms | 22.739 ms | 1.2464 ms | 0.002 |  261.06 KB |       62.46 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.751 μs |  2.2069 μs | 0.1210 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.487 μs |  3.0364 μs | 0.1664 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.276 μs |  3.2552 μs | 0.1784 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.903 μs |  4.9438 μs | 0.2710 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.919 μs |  2.0157 μs | 0.1105 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.613 μs |  8.9502 μs | 0.4906 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.279 μs | 19.4960 μs | 1.0686 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.372 μs | 14.3818 μs | 0.7883 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.534 μs |  0.7297 μs | 0.0400 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.673 μs |  7.1702 μs | 0.3930 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,532.3 ns |    805.7 ns |  44.16 ns |  0.37 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,281.2 ns |  1,846.2 ns | 101.19 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,280.8 ns |    557.4 ns |  30.55 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,479.8 ns |    374.0 ns |  20.50 ns |  0.59 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    730.7 ns |    660.4 ns |  36.20 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,665.3 ns | 10,518.8 ns | 576.57 ns |  9.70 |    0.19 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,191.7 ns |  1,395.4 ns |  76.49 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,890.0 ns |  1,906.2 ns | 104.48 ns |  0.93 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.031 μs |   7.231 μs |  0.3964 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   595.073 μs | 729.378 μs | 39.9797 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.477 μs |   4.984 μs |  0.2732 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,641.564 μs | 228.085 μs | 12.5021 μs |    1280 B |


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