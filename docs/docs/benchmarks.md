---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 18:06 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,178.15 μs** |    **248.81 μs** |    **13.638 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,323.92 μs |    711.79 μs |    39.016 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,385.40 μs** |  **1,663.68 μs** |    **91.192 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,335.85 μs |    189.91 μs |    10.410 μs |  0.32 |    0.00 |  15.6250 |       - |  339.31 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,410.68 μs** |  **1,428.05 μs** |    **78.276 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,202.71 μs |    828.25 μs |    45.399 μs |  0.19 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,143.23 μs** |  **3,837.58 μs** |   **210.351 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,459.13 μs |  5,685.55 μs |   311.644 μs |  0.53 |    0.02 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.25 μs** |     **78.96 μs** |     **4.328 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **42.09 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.56 μs |    158.85 μs |     8.707 μs |  0.50 |    0.06 |        - |       - |    9.11 KB |        0.22 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,361.06 μs** |    **751.72 μs** |    **41.204 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **420.67 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    616.86 μs |    745.36 μs |    40.856 μs |  0.45 |    0.03 |        - |       - |   95.37 KB |        0.23 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    200.54 μs |    226.58 μs |    12.420 μs |     ? |       ? |   0.9766 |       - |  100.83 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,022.31 μs |  2,157.56 μs |   118.263 μs |     ? |       ? |   7.8125 |       - | 1000.63 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,393.88 μs** |     **48.60 μs** |     **2.664 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,111.54 μs |     37.40 μs |     2.050 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,393.67 μs** |     **84.99 μs** |     **4.659 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,111.95 μs |     35.94 μs |     1.970 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **6,895.76 μs** | **25,083.09 μs** | **1,374.889 μs** |  **1.03** |    **0.26** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,112.10 μs |     38.51 μs |     2.111 μs |  0.17 |    0.03 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,396.86 μs** |     **27.10 μs** |     **1.485 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,354.65 μs |    416.12 μs |    22.809 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.888 ms** |  **0.8796 ms** | **0.0482 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.297 ms | 73.0154 ms | 4.0022 ms | 0.006 |  593.65 KB |        7.96 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.466 ms** | **65.5358 ms** | **3.5922 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.170 ms | 39.6851 ms | 2.1753 ms | 0.005 |  783.66 KB |        3.13 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.470 ms** | **33.4212 ms** | **1.8319 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.885 ms | 39.9109 ms | 2.1876 ms | 0.006 |  995.23 KB |        1.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.611 ms** | **21.3710 ms** | **1.1714 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.375 ms | 40.5210 ms | 2.2211 ms | 0.005 | 2834.59 KB |        1.20 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.494 ms** |  **7.6785 ms** | **0.4209 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.799 ms | 36.6109 ms | 2.0068 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.884 ms** | **12.7897 ms** | **0.7010 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.746 ms | 10.5108 ms | 0.5761 ms | 0.002 |  191.05 KB |       45.88 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.682 ms** | **33.0414 ms** | **1.8111 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.045 ms |  6.3504 ms | 0.3481 ms | 0.002 |  202.75 KB |       84.26 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.442 ms** | **21.1304 ms** | **1.1582 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.497 ms | 19.0729 ms | 1.0454 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.089 μs |  1.905 μs | 0.1044 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.433 μs |  2.034 μs | 0.1115 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.815 μs |  5.601 μs | 0.3070 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.778 μs |  1.703 μs | 0.0933 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.075 μs |  1.369 μs | 0.0751 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.307 μs |  2.407 μs | 0.1319 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.083 μs | 12.357 μs | 0.6773 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.528 μs |  8.337 μs | 0.4570 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.569 μs |  2.885 μs | 0.1581 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.699 μs |  5.338 μs | 0.2926 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,995.8 ns |   6,813.5 ns |   373.47 ns |  1,848.5 ns |  0.27 |    0.18 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,309.7 ns |     379.8 ns |    20.82 ns |  1,303.0 ns |  0.18 |    0.12 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  2,157.0 ns |  19,835.7 ns | 1,087.26 ns |  1,713.0 ns |  0.29 |    0.24 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,544.0 ns |   2,219.4 ns |   121.66 ns |  2,484.0 ns |  0.35 |    0.22 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    747.7 ns |     557.4 ns |    30.55 ns |    741.0 ns |  0.10 |    0.07 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,805.3 ns |   2,464.6 ns |   135.09 ns | 39,765.0 ns |  5.40 |    3.50 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           | 11,551.0 ns | 169,385.5 ns | 9,284.59 ns |  8,645.0 ns |  1.57 |    1.63 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,037.0 ns |   5,457.3 ns |   299.13 ns |  3,957.0 ns |  0.55 |    0.36 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.69 μs |  16.311 μs |  0.894 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   523.42 μs | 508.304 μs | 27.862 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.38 μs |   6.260 μs |  0.343 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,791.29 μs | 496.080 μs | 27.192 μs |    1280 B |


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