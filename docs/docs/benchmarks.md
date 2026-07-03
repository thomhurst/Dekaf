---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 20:47 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,098.99 μs** |   **320.29 μs** |  **17.556 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,339.88 μs | 1,262.39 μs |  69.196 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,157.12 μs** |   **795.82 μs** |  **43.622 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,311.84 μs | 1,256.33 μs |  68.864 μs |  0.32 |    0.01 |  15.6250 |       - |   339.5 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,625.80 μs** |   **126.62 μs** |   **6.941 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,343.85 μs | 5,639.51 μs | 309.121 μs |  0.20 |    0.04 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,788.46 μs** | **2,481.24 μs** | **136.005 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,757.55 μs |   836.50 μs |  45.851 μs |  0.49 |    0.01 |  15.6250 |       - |  361.67 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **129.99 μs** |   **176.58 μs** |   **9.679 μs** |  **1.00** |    **0.09** |   **2.4414** |       **-** |   **40.85 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.77 μs |    95.96 μs |   5.260 μs |  0.46 |    0.05 |        - |       - |    7.03 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,680.09 μs** | **2,346.20 μs** | **128.603 μs** |  **1.00** |    **0.10** |  **25.3906** |       **-** |  **422.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    616.24 μs |   264.62 μs |  14.505 μs |  0.37 |    0.03 |        - |       - |   71.24 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    205.86 μs |   292.39 μs |  16.027 μs |     ? |       ? |   0.9766 |       - |  100.06 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,959.53 μs | 1,895.97 μs | 103.924 μs |     ? |       ? |   7.8125 |       - | 1012.57 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,440.49 μs** |   **773.61 μs** |  **42.404 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,360.67 μs |   385.69 μs |  21.141 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,405.51 μs** |    **56.35 μs** |   **3.089 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,369.66 μs |   379.95 μs |  20.826 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,460.47 μs** |   **243.36 μs** |  **13.339 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,107.11 μs |    77.89 μs |   4.270 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,443.43 μs** |   **143.69 μs** |   **7.876 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,105.46 μs |    60.75 μs |   3.330 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.897 ms** | **17.632 ms** | **0.9665 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.829 ms | 49.568 ms | 2.7170 ms | 0.005 | 593.36 KB |        7.95 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.657 ms** | **13.557 ms** | **0.7431 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.890 ms | 20.505 ms | 1.1240 ms | 0.005 | 786.45 KB |        3.14 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.457 ms** | **37.371 ms** | **2.0484 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.649 ms | 93.769 ms | 5.1398 ms | 0.006 | 1072.1 KB |        1.78 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.169 ms** | **32.488 ms** | **1.7808 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.468 ms | 43.954 ms | 2.4093 ms | 0.005 | 2761.4 KB |        1.17 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.075 ms** |  **4.141 ms** | **0.2270 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.521 ms | 21.857 ms | 1.1981 ms | 0.002 | 184.32 KB |       76.60 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.504 ms** |  **9.268 ms** | **0.5080 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.401 ms | 21.654 ms | 1.1870 ms | 0.002 |  186.3 KB |       44.74 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.505 ms** | **53.784 ms** | **2.9481 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.247 ms |  9.913 ms | 0.5434 ms | 0.002 | 189.09 KB |       78.58 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.455 ms** | **23.106 ms** | **1.2665 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.215 ms |  8.466 ms | 0.4641 ms | 0.002 | 188.32 KB |       45.06 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.532 μs |   1.645 μs |  0.0902 μs | 15.539 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.818 μs |   9.292 μs |  0.5093 μs |  9.545 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 17.087 μs | 191.596 μs | 10.5020 μs | 11.418 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.580 μs |   4.930 μs |  0.2702 μs | 32.710 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.087 μs |   3.623 μs |  0.1986 μs |  9.004 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.358 μs |   6.450 μs |  0.3536 μs | 22.304 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.454 μs |  24.348 μs |  1.3346 μs | 17.291 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.981 μs |  25.749 μs |  1.4114 μs | 19.380 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.891 μs |   1.827 μs |  0.1002 μs |  4.928 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.169 μs |  11.793 μs |  0.6464 μs | 12.299 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,242.0 ns |  4,034.3 ns | 221.1 ns |  0.30 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,201.5 ns |  4,251.2 ns | 233.0 ns |  0.29 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,332.3 ns |  2,229.9 ns | 122.2 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,379.7 ns |  3,140.5 ns | 172.1 ns |  0.57 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    764.5 ns |  2,908.5 ns | 159.4 ns |  0.18 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,310.3 ns | 13,201.7 ns | 723.6 ns |  8.42 |    0.64 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,212.8 ns |  6,450.4 ns | 353.6 ns |  1.00 |    0.10 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,067.8 ns | 11,613.1 ns | 636.6 ns |  0.97 |    0.15 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev     | Allocated |
|------------------------ |-------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.634 μs |     4.782 μs |  0.2621 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   558.340 μs | 1,171.256 μs | 64.2005 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.698 μs |     8.141 μs |  0.4462 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,580.291 μs |   357.336 μs | 19.5868 μs |    1280 B |


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