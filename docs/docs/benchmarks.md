---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 06:51 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,298.58 μs** |   **981.413 μs** |  **53.795 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,427.92 μs | 2,698.393 μs | 147.908 μs |  0.23 |    0.02 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,399.87 μs** |   **123.405 μs** |   **6.764 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,350.03 μs |   189.972 μs |  10.413 μs |  0.32 |    0.00 |  15.6250 |       - |  339.57 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,360.23 μs** |   **884.061 μs** |  **48.458 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,237.39 μs |   589.389 μs |  32.306 μs |  0.19 |    0.00 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,445.43 μs** | **5,333.513 μs** | **292.348 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,989.58 μs | 2,223.059 μs | 121.853 μs |  0.48 |    0.01 |  15.6250 |       - |  361.73 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.96 μs** |    **20.024 μs** |   **1.098 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **41.66 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     78.17 μs |   497.001 μs |  27.242 μs |  0.57 |    0.17 |        - |       - |    7.74 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,244.74 μs** | **6,144.005 μs** | **336.774 μs** |  **1.05** |    **0.34** |  **19.5313** |       **-** |  **377.95 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    753.54 μs |   995.524 μs |  54.568 μs |  0.63 |    0.14 |        - |       - |   55.27 KB |        0.15 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    205.93 μs |   635.763 μs |  34.848 μs |     ? |       ? |   0.9766 |       - |   98.13 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,929.38 μs | 1,812.579 μs |  99.354 μs |     ? |       ? |   7.8125 |       - | 1018.48 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,435.76 μs** |   **288.662 μs** |  **15.823 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,323.12 μs |   487.542 μs |  26.724 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,446.40 μs** |    **81.529 μs** |   **4.469 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,267.74 μs |   490.736 μs |  26.899 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,418.87 μs** |   **159.034 μs** |   **8.717 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,109.84 μs |     5.205 μs |   0.285 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,433.37 μs** |   **160.732 μs** |   **8.810 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,114.95 μs |    54.638 μs |   2.995 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.781 ms** |  **39.680 ms** | **2.1750 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.253 ms |  38.044 ms | 2.0853 ms | 0.005 |  596.52 KB |        7.99 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.629 ms** |  **72.430 ms** | **3.9701 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.887 ms |  22.408 ms | 1.2282 ms | 0.005 |  776.45 KB |        3.10 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.386 ms** |  **27.089 ms** | **1.4848 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.395 ms | 110.402 ms | 6.0515 ms | 0.006 |  994.48 KB |        1.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.998 ms** |  **23.512 ms** | **1.2888 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.893 ms |  16.282 ms | 0.8925 ms | 0.005 | 2763.02 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.143 ms** |  **14.997 ms** | **0.8220 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.874 ms |  14.303 ms | 0.7840 ms | 0.002 |  184.44 KB |       76.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.159 ms** |  **52.560 ms** | **2.8810 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.038 ms |  17.600 ms | 0.9647 ms | 0.002 |  186.55 KB |       44.80 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.377 ms** |  **11.612 ms** | **0.6365 ms** | **1.000** |    **3.34 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.060 ms |  13.042 ms | 0.7149 ms | 0.002 |  184.77 KB |       55.26 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.958 ms** |  **27.041 ms** | **1.4822 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.345 ms |  15.645 ms | 0.8576 ms | 0.002 |  188.34 KB |       45.06 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.845 μs |   2.589 μs | 0.1419 μs | 14.818 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.287 μs |   2.710 μs | 0.1486 μs |  9.217 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.136 μs |   1.637 μs | 0.0897 μs | 10.129 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.762 μs |   4.123 μs | 0.2260 μs | 26.744 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.915 μs |   1.005 μs | 0.0551 μs |  8.911 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.366 μs |   3.243 μs | 0.1778 μs | 19.306 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.668 μs | 179.138 μs | 9.8192 μs | 18.054 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.802 μs |   9.717 μs | 0.5326 μs | 21.706 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.718 μs |   7.674 μs | 0.4207 μs |  4.705 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.319 μs |   3.753 μs | 0.2057 μs | 10.309 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,392.2 ns |   547.4 ns |  30.01 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,565.8 ns | 4,620.1 ns | 253.25 ns |  0.37 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,556.3 ns |   557.4 ns |  30.55 ns |  0.37 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,505.0 ns |   182.4 ns |  10.00 ns |  0.60 |    0.00 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    885.0 ns | 1,801.1 ns |  98.73 ns |  0.21 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,124.5 ns | 5,435.8 ns | 297.95 ns |  9.37 |    0.09 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,175.8 ns |   640.7 ns |  35.12 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,918.8 ns | 1,196.4 ns |  65.58 ns |  0.94 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.80 μs |  23.996 μs |  1.315 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   529.25 μs | 106.990 μs |  5.864 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.29 μs |   1.105 μs |  0.061 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,678.64 μs | 373.262 μs | 20.460 μs |    1280 B |


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