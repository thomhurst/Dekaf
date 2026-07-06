---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 04:34 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,149.65 μs** |    **421.12 μs** |    **23.083 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,455.50 μs |    349.92 μs |    19.180 μs |  0.24 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,375.52 μs** |    **521.94 μs** |    **28.609 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,359.46 μs |    260.82 μs |    14.296 μs |  0.32 |    0.00 |  15.6250 |       - |  339.63 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,148.50 μs** |    **382.44 μs** |    **20.963 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,420.31 μs |  3,047.59 μs |   167.049 μs |  0.23 |    0.02 |        - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,778.11 μs** |    **652.35 μs** |    **35.758 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,017.95 μs | 17,111.17 μs |   937.921 μs |  0.63 |    0.06 |  15.6250 |       - |  361.56 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.01 μs** |     **97.99 μs** |     **5.371 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.62 μs |     15.93 μs |     0.873 μs |  0.47 |    0.02 |        - |       - |    9.65 KB |        0.29 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,368.86 μs** |    **286.93 μs** |    **15.728 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    653.30 μs |    364.99 μs |    20.006 μs |  0.48 |    0.01 |        - |       - |   113.7 KB |        0.34 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **658.44 μs** |  **8,026.94 μs** |   **439.983 μs** |  **1.53** |    **1.55** |   **7.3242** |       **-** |  **122.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    220.23 μs |     57.90 μs |     3.174 μs |  0.51 |    0.37 |   0.9766 |       - |  106.22 KB |        0.87 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,488.29 μs** | **29,597.63 μs** | **1,622.346 μs** |  **1.02** |    **0.20** |  **74.2188** |       **-** | **1226.36 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,924.75 μs |  3,205.79 μs |   175.720 μs |  0.19 |    0.03 |   7.8125 |       - |  984.26 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,439.31 μs** |     **64.15 μs** |     **3.516 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,355.85 μs |    664.91 μs |    36.446 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,429.92 μs** |     **24.35 μs** |     **1.335 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,394.74 μs |    352.56 μs |    19.325 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,430.85 μs** |     **84.19 μs** |     **4.615 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,403.15 μs |    122.67 μs |     6.724 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,445.82 μs** |     **99.52 μs** |     **5.455 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,381.58 μs |    409.24 μs |    22.432 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.008 ms** |  **3.437 ms** | **0.1884 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.743 ms | 36.241 ms | 1.9865 ms | 0.005 |  601.55 KB |        8.06 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.534 ms** | **23.940 ms** | **1.3122 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.875 ms |  6.303 ms | 0.3455 ms | 0.005 |  794.72 KB |        3.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.234 ms** | **25.160 ms** | **1.3791 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.554 ms | 39.850 ms | 2.1843 ms | 0.006 | 1003.16 KB |        1.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.426 ms** | **10.305 ms** | **0.5648 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.330 ms | 49.610 ms | 2.7193 ms | 0.005 | 2882.52 KB |        1.22 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.742 ms** | **40.229 ms** | **2.2051 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.612 ms | 22.858 ms | 1.2529 ms | 0.002 |  184.47 KB |       76.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.211 ms** | **24.821 ms** | **1.3605 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.078 ms | 11.244 ms | 0.6163 ms | 0.002 |  195.47 KB |       46.94 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.839 ms** | **25.716 ms** | **1.4096 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.898 ms | 34.532 ms | 1.8928 ms | 0.003 |  184.82 KB |       76.81 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.123 ms** |  **4.950 ms** | **0.2713 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.253 ms | 14.979 ms | 0.8211 ms | 0.002 |  227.71 KB |       54.48 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.813 μs |   1.136 μs |  0.0623 μs | 25.794 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.063 μs |   1.324 μs |  0.0726 μs | 10.100 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 16.588 μs | 195.387 μs | 10.7098 μs | 10.541 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.921 μs |   1.931 μs |  0.1058 μs | 26.911 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.986 μs |   1.797 μs |  0.0985 μs |  8.957 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 27.797 μs | 231.100 μs | 12.6674 μs | 20.483 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.010 μs |   3.424 μs |  0.1877 μs | 18.113 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.115 μs |   6.107 μs |  0.3347 μs | 19.958 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.524 μs |   3.646 μs |  0.1998 μs |  4.634 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.640 μs |   5.326 μs |  0.2919 μs | 10.710 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,433.0 ns |  1,493.3 ns |  81.85 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,426.3 ns |  1,474.6 ns |  80.83 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,364.2 ns |    379.8 ns |  20.82 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,447.7 ns |  1,413.3 ns |  77.47 ns |  0.61 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    762.0 ns |  1,580.0 ns |  86.60 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,356.3 ns | 11,517.2 ns | 631.29 ns | 10.35 |    0.51 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,003.7 ns |  4,177.5 ns | 228.99 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,779.8 ns |  2,708.0 ns | 148.44 ns |  0.95 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.306 μs |   5.588 μs |  0.3063 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   548.077 μs | 338.762 μs | 18.5687 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.781 μs |  20.691 μs |  1.1341 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,629.743 μs |  62.186 μs |  3.4086 μs |    1280 B |


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