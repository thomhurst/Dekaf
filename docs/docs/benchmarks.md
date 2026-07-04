---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 15:58 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,182.60 μs** |   **645.97 μs** |  **35.408 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,347.38 μs | 3,005.65 μs | 164.750 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,332.01 μs** | **1,625.70 μs** |  **89.110 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,376.50 μs | 1,811.05 μs |  99.270 μs |  0.32 |    0.01 |  15.6250 |       - |  339.55 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,362.69 μs** | **1,191.70 μs** |  **65.321 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,311.50 μs | 2,408.52 μs | 132.019 μs |  0.21 |    0.02 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,594.22 μs** | **8,137.30 μs** | **446.033 μs** |  **1.00** |    **0.04** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,422.12 μs | 2,154.47 μs | 118.094 μs |  0.51 |    0.02 |  15.6250 |       - |  361.63 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **139.65 μs** |    **56.29 μs** |   **3.085 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.18 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     70.29 μs |   147.77 μs |   8.100 μs |  0.50 |    0.05 |        - |       - |    7.01 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,381.19 μs** |   **941.19 μs** |  **51.590 μs** |  **1.00** |    **0.05** |  **23.4375** |       **-** |  **421.79 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    656.32 μs | 1,340.59 μs |  73.482 μs |  0.48 |    0.05 |        - |       - |   64.67 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    215.33 μs |   317.22 μs |  17.388 μs |     ? |       ? |   0.9766 |       - |  104.57 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,984.40 μs | 3,688.85 μs | 202.198 μs |     ? |       ? |   7.8125 |       - |  981.77 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,441.59 μs** |   **549.09 μs** |  **30.097 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,110.39 μs |    28.80 μs |   1.579 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,445.22 μs** |    **56.81 μs** |   **3.114 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,385.62 μs |   197.63 μs |  10.833 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,428.40 μs** |   **240.45 μs** |  **13.180 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,363.56 μs |   585.69 μs |  32.103 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,435.47 μs** |   **234.95 μs** |  **12.878 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,342.64 μs |   669.24 μs |  36.683 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.518 ms** |  **21.474 ms** | **1.1770 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.726 ms |  38.228 ms | 2.0954 ms | 0.005 |  595.35 KB |        7.98 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.050 ms** |  **10.265 ms** | **0.5626 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.493 ms |  21.716 ms | 1.1903 ms | 0.005 |   775.8 KB |        3.10 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.610 ms** |  **13.651 ms** | **0.7483 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.577 ms | 105.541 ms | 5.7851 ms | 0.006 | 1090.62 KB |        1.81 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.859 ms** |  **49.481 ms** | **2.7122 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.138 ms |  15.305 ms | 0.8389 ms | 0.005 | 2759.77 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.715 ms** |   **6.836 ms** | **0.3747 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.485 ms |  15.357 ms | 0.8418 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.912 ms** |  **36.189 ms** | **1.9836 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.731 ms |   7.047 ms | 0.3862 ms | 0.002 |  186.35 KB |       44.75 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.019 ms** |  **13.100 ms** | **0.7180 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.296 ms |  24.416 ms | 1.3383 ms | 0.002 |  240.23 KB |       99.84 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.350 ms** |  **28.936 ms** | **1.5861 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.710 ms |   4.030 ms | 0.2209 ms | 0.002 |  190.13 KB |       45.49 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 31.301 μs | 178.1873 μs | 9.7670 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.269 μs |   0.6320 μs | 0.0346 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.440 μs |   2.5541 μs | 0.1400 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.830 μs |   4.4903 μs | 0.2461 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.003 μs |   0.8622 μs | 0.0473 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.128 μs |   2.6039 μs | 0.1427 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.320 μs |   4.5704 μs | 0.2505 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.364 μs |   4.5168 μs | 0.2476 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.841 μs |   9.1753 μs | 0.5029 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.566 μs |   4.3126 μs | 0.2364 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,782.3 ns |   2,641.3 ns |    144.78 ns |  1,742.0 ns |  0.37 |    0.09 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,218.3 ns |     270.8 ns |     14.84 ns |  1,222.0 ns |  0.25 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,360.5 ns |     909.8 ns |     49.87 ns |  1,367.5 ns |  0.28 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,507.3 ns |     918.2 ns |     50.33 ns |  2,514.0 ns |  0.52 |    0.13 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    738.7 ns |     379.8 ns |     20.82 ns |    732.0 ns |  0.15 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 52,094.3 ns | 391,319.9 ns | 21,449.57 ns | 40,265.0 ns | 10.80 |    4.74 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,139.3 ns |  30,866.1 ns |  1,691.88 ns |  4,338.0 ns |  1.07 |    0.40 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,164.0 ns |  10,407.4 ns |    570.46 ns |  3,897.0 ns |  0.86 |    0.23 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.385 μs |  11.924 μs |  0.6536 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   533.344 μs | 629.501 μs | 34.5051 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.838 μs |   7.399 μs |  0.4056 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,726.117 μs | 825.810 μs | 45.2654 μs |    1280 B |


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