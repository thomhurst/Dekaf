---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 21:13 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,050.18 μs** |  **1,212.90 μs** |    **66.483 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,298.21 μs |  1,299.20 μs |    71.213 μs |  0.21 |    0.01 |       - |       - |   34.78 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,260.12 μs** |  **1,681.18 μs** |    **92.151 μs** |  **1.00** |    **0.02** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,234.05 μs |    364.84 μs |    19.998 μs |  0.31 |    0.00 | 11.7188 |       - |  339.49 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **7,491.53 μs** | **28,254.70 μs** | **1,548.735 μs** |  **1.03** |    **0.25** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,113.51 μs |    122.83 μs |     6.733 μs |  0.15 |    0.02 |       - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,284.52 μs** |  **3,570.76 μs** |   **195.725 μs** |  **1.00** |    **0.03** | **78.1250** | **31.2500** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,691.10 μs |    637.56 μs |    34.947 μs |  0.51 |    0.01 |  7.8125 |       - |  361.76 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **126.11 μs** |     **64.95 μs** |     **3.560 μs** |  **1.00** |    **0.03** |  **1.7090** |       **-** |   **42.02 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    53.21 μs |     88.16 μs |     4.833 μs |  0.42 |    0.03 |       - |       - |    4.11 KB |        0.10 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,415.92 μs** |  **4,988.75 μs** |   **273.450 μs** |  **1.02** |    **0.23** | **15.6250** |       **-** |  **421.07 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   829.62 μs |  2,491.27 μs |   136.555 μs |  0.60 |    0.13 |       - |       - |   41.88 KB |        0.10 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   191.46 μs |    478.21 μs |    26.212 μs |     ? |       ? |       - |       - |   95.28 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,929.76 μs |  4,961.96 μs |   271.982 μs |     ? |       ? |       - |       - |  964.93 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,416.84 μs** |    **175.67 μs** |     **9.629 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,387.99 μs |    870.69 μs |    47.725 μs |  0.26 |    0.01 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,440.56 μs** |    **624.94 μs** |    **34.255 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,386.40 μs |    529.12 μs |    29.003 μs |  0.25 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,424.54 μs** |    **398.71 μs** |    **21.855 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,412.16 μs |    270.35 μs |    14.819 μs |  0.26 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,404.32 μs** |    **469.89 μs** |    **25.756 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,430.69 μs |    558.81 μs |    30.630 μs |  0.26 |    0.01 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.928 ms** |  **21.837 ms** | **1.1970 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.828 ms |  15.215 ms | 0.8340 ms | 0.005 |   597.3 KB |        8.00 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.057 ms** |   **5.071 ms** | **0.2779 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.734 ms |  31.775 ms | 1.7417 ms | 0.004 |  777.52 KB |        3.11 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.441 ms** |   **4.746 ms** | **0.2602 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.166 ms | 115.062 ms | 6.3069 ms | 0.006 |  995.47 KB |        1.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.543 ms** |  **11.645 ms** | **0.6383 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.610 ms |  46.376 ms | 2.5420 ms | 0.005 | 2763.01 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.280 ms** |  **23.634 ms** | **1.2955 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.339 ms |  19.542 ms | 1.0712 ms | 0.002 |  193.52 KB |       80.42 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.805 ms** |   **5.391 ms** | **0.2955 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.151 ms |   5.404 ms | 0.2962 ms | 0.002 |   186.3 KB |       44.74 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.395 ms** |  **24.903 ms** | **1.3650 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.220 ms |   4.230 ms | 0.2319 ms | 0.002 |  185.69 KB |       77.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.457 ms** |  **15.749 ms** | **0.8632 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.209 ms |   6.895 ms | 0.3779 ms | 0.002 |  188.32 KB |       45.06 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.911 μs |   1.735 μs |  0.0951 μs | 14.877 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.241 μs |   3.587 μs |  0.1966 μs |  9.128 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.319 μs |   1.095 μs |  0.0600 μs | 10.319 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 36.065 μs | 291.954 μs | 16.0030 μs | 26.961 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.893 μs |   1.099 μs |  0.0602 μs |  8.887 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.321 μs |   2.424 μs |  0.1329 μs | 19.372 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 27.805 μs | 325.293 μs | 17.8304 μs | 17.823 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.275 μs |   4.297 μs |  0.2356 μs | 20.198 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.667 μs |   3.567 μs |  0.1955 μs |  4.614 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.657 μs |   3.760 μs |  0.2061 μs | 10.761 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,836.3 ns | 2,009.6 ns | 110.15 ns |  0.44 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,299.3 ns |   200.1 ns |  10.97 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,516.3 ns | 1,967.7 ns | 107.86 ns |  0.37 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,591.3 ns |   559.4 ns |  30.66 ns |  0.62 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    770.7 ns |   912.2 ns |  50.00 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,091.3 ns | 7,863.7 ns | 431.04 ns |  9.90 |    0.36 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,157.5 ns | 3,180.9 ns | 174.36 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,916.3 ns | 3,170.4 ns | 173.78 ns |  0.94 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.10 μs |   5.225 μs |  0.286 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   532.94 μs | 232.815 μs | 12.761 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.16 μs |   3.148 μs |  0.173 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,687.73 μs | 351.029 μs | 19.241 μs |    1280 B |


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