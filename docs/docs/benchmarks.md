---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 13:49 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,355.36 μs** |  **1,040.51 μs** |    **57.034 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,450.51 μs |  4,540.85 μs |   248.900 μs |  0.23 |    0.03 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,439.23 μs** |    **635.35 μs** |    **34.826 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,288.64 μs |    350.40 μs |    19.207 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |   339.4 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,230.42 μs** |  **1,136.27 μs** |    **62.283 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,309.39 μs |  5,357.75 μs |   293.676 μs |  0.21 |    0.04 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,972.59 μs** |  **9,479.79 μs** |   **519.619 μs** |  **1.00** |    **0.05** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,257.04 μs | 22,464.23 μs | 1,231.340 μs |  0.56 |    0.08 |  15.6250 |       - |  361.85 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.22 μs** |     **42.13 μs** |     **2.309 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.11 μs |    128.28 μs |     7.031 μs |  0.45 |    0.04 |        - |       - |    8.26 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,448.54 μs** |  **1,105.30 μs** |    **60.585 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |  **420.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    755.13 μs |  2,128.80 μs |   116.687 μs |  0.52 |    0.07 |        - |       - |  185.71 KB |        0.44 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    249.59 μs |    921.19 μs |    50.494 μs |     ? |       ? |   0.9766 |       - |   99.93 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,986.47 μs |  2,789.87 μs |   152.922 μs |     ? |       ? |   7.8125 |       - | 1005.59 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,431.17 μs** |    **113.56 μs** |     **6.225 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,111.25 μs |     16.41 μs |     0.900 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,426.80 μs** |    **135.72 μs** |     **7.440 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,114.14 μs |     57.42 μs |     3.148 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,436.69 μs** |    **156.64 μs** |     **8.586 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,114.27 μs |     26.80 μs |     1.469 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,424.15 μs** |     **35.47 μs** |     **1.944 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,236.45 μs |    417.81 μs |    22.901 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.843 ms** |  **24.288 ms** | **1.3313 ms** | **3,169.150 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.211 ms |  41.694 ms | 2.2854 ms |    15.183 ms | 0.005 |  592.26 KB |        7.94 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.515 ms** |  **30.444 ms** | **1.6688 ms** | **3,165.973 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.539 ms |  11.722 ms | 0.6425 ms |    14.425 ms | 0.005 |  787.07 KB |        3.14 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.510 ms** |  **25.894 ms** | **1.4193 ms** | **3,165.916 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.217 ms | 161.929 ms | 8.8759 ms |    15.786 ms | 0.006 |  994.81 KB |        1.65 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.001 ms** |  **22.940 ms** | **1.2574 ms** | **3,166.153 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.446 ms |  19.799 ms | 1.0853 ms |    17.883 ms | 0.006 | 2764.34 KB |        1.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.149 ms** |  **31.758 ms** | **1.7408 ms** | **3,157.466 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.380 ms |  22.982 ms | 1.2597 ms |     5.808 ms | 0.002 |  184.44 KB |       76.65 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.423 ms** |  **40.920 ms** | **2.2429 ms** | **3,156.657 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.065 ms |  28.364 ms | 1.5547 ms |     7.081 ms | 0.002 |  187.73 KB |       45.08 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.016 ms** |   **7.121 ms** | **0.3903 ms** | **3,157.138 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.880 ms |  12.720 ms | 0.6972 ms |     6.910 ms | 0.002 |  184.59 KB |       76.71 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.783 ms** |  **44.955 ms** | **2.4641 ms** | **3,155.445 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.754 ms |  17.557 ms | 0.9624 ms |     6.834 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.741 μs |  1.4033 μs | 0.0769 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.474 μs |  5.7370 μs | 0.3145 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.360 μs |  1.4412 μs | 0.0790 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.843 μs |  2.8517 μs | 0.1563 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.867 μs |  2.0397 μs | 0.1118 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.245 μs |  0.3649 μs | 0.0200 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.987 μs |  3.9817 μs | 0.2183 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.093 μs | 14.0854 μs | 0.7721 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.162 μs |  8.5948 μs | 0.4711 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.346 μs |  2.8634 μs | 0.1570 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,242.7 ns |   791.1 ns |  43.36 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,186.8 ns | 1,114.3 ns |  61.08 ns |  0.29 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,479.7 ns | 3,244.8 ns | 177.86 ns |  0.36 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,516.2 ns |   759.5 ns |  41.63 ns |  0.61 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    854.3 ns | 2,490.3 ns | 136.50 ns |  0.21 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,008.3 ns | 6,310.1 ns | 345.88 ns |  9.78 |    0.65 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,109.2 ns | 5,879.0 ns | 322.25 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,215.0 ns | 7,358.5 ns | 403.34 ns |  1.03 |    0.11 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.94 μs |   6.089 μs |  0.334 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   946.17 μs | 183.487 μs | 10.058 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.69 μs |  31.770 μs |  1.741 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,664.53 μs | 472.808 μs | 25.916 μs |    1280 B |


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