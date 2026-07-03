---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 12:55 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,220.03 μs** |    **142.74 μs** |   **7.824 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,273.17 μs |  2,159.98 μs | 118.396 μs |  0.20 |    0.02 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,424.11 μs** |  **1,064.18 μs** |  **58.331 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,377.00 μs |    515.52 μs |  28.258 μs |  0.32 |    0.00 |  15.6250 |       - |  339.85 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,155.02 μs** |    **432.00 μs** |  **23.679 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,428.96 μs |  3,271.85 μs | 179.341 μs |  0.23 |    0.03 |        - |       - |   36.89 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,954.01 μs** |  **4,065.19 μs** | **222.827 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,897.08 μs | 10,877.16 μs | 596.214 μs |  0.53 |    0.04 |  15.6250 |       - |  369.42 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **145.66 μs** |     **63.09 μs** |   **3.458 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.68 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.53 μs |     77.21 μs |   4.232 μs |  0.47 |    0.03 |   0.4883 |       - |   16.74 KB |        0.40 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,412.77 μs** |  **1,008.78 μs** |  **55.295 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |  **430.28 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    786.91 μs |    791.61 μs |  43.391 μs |  0.56 |    0.03 |   3.9063 |       - |  130.46 KB |        0.30 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    332.62 μs |    680.00 μs |  37.273 μs |     ? |       ? |   6.8359 |  5.8594 |  191.54 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,311.81 μs |  1,795.11 μs |  98.396 μs |     ? |       ? |  70.3125 | 62.5000 | 2021.16 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,455.60 μs** |    **145.98 μs** |   **8.002 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,124.19 μs |     48.66 μs |   2.667 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,470.30 μs** |     **92.00 μs** |   **5.043 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,121.31 μs |     35.44 μs |   1.942 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,517.74 μs** |    **600.01 μs** |  **32.889 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,422.82 μs |    557.19 μs |  30.541 μs |  0.26 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,450.77 μs** |     **37.63 μs** |   **2.062 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,402.17 μs |    481.83 μs |  26.411 μs |  0.26 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.992 ms** | **17.029 ms** | **0.9334 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.201 ms | 20.568 ms | 1.1274 ms | 0.005 |  409.06 KB |        5.48 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.697 ms** |  **6.454 ms** | **0.3537 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.228 ms | 38.778 ms | 2.1256 ms | 0.005 |  592.29 KB |        2.37 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.424 ms** | **15.663 ms** | **0.8585 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.030 ms | 16.850 ms | 0.9236 ms | 0.005 |  819.55 KB |        1.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.610 ms** | **16.705 ms** | **0.9157 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.522 ms |  6.283 ms | 0.3444 ms | 0.005 | 2644.15 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.512 ms** |  **9.150 ms** | **0.5016 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.719 ms | 14.821 ms | 0.8124 ms | 0.002 |  181.28 KB |       75.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.128 ms** | **38.680 ms** | **2.1202 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.025 ms | 14.043 ms | 0.7697 ms | 0.002 |  184.66 KB |       44.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.846 ms** | **22.152 ms** | **1.2142 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.465 ms | 12.247 ms | 0.6713 ms | 0.002 |  181.43 KB |       75.40 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.025 ms** | **12.976 ms** | **0.7112 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.147 ms |  3.737 ms | 0.2048 ms | 0.002 |  255.91 KB |       61.23 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 20.414 μs | 83.436 μs | 4.5734 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.955 μs |  5.118 μs | 0.2805 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 11.915 μs | 19.558 μs | 1.0720 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.664 μs |  4.598 μs | 0.2520 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.916 μs |  2.600 μs | 0.1425 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.463 μs |  9.310 μs | 0.5103 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.947 μs | 24.630 μs | 1.3501 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.524 μs |  6.961 μs | 0.3816 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.857 μs | 36.007 μs | 1.9737 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,231.5 ns |   1,740.3 ns |     95.39 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,275.2 ns |   2,796.7 ns |    153.30 ns |  0.33 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,350.2 ns |   2,261.8 ns |    123.98 ns |  0.35 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,539.7 ns |   4,119.0 ns |    225.77 ns |  0.65 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    707.7 ns |   3,686.6 ns |    202.07 ns |  0.18 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,347.5 ns | 234,531.0 ns | 12,855.44 ns | 10.84 |    2.93 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,918.0 ns |   4,937.2 ns |    270.62 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,855.5 ns |   9,349.3 ns |    512.47 ns |  0.99 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.954 μs |  17.748 μs | 0.9729 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   516.125 μs | 119.388 μs | 6.5441 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.305 μs |   8.481 μs | 0.4649 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,571.546 μs | 161.226 μs | 8.8374 μs |    1280 B |


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