---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 00:06 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,143.02 μs** |    **849.27 μs** |    **46.551 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,290.89 μs |  2,006.76 μs |   109.997 μs |  0.21 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,205.06 μs** |    **612.31 μs** |    **33.563 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,301.61 μs |  1,857.21 μs |   101.800 μs |  0.32 |    0.01 |  19.5313 |  3.9063 |  339.53 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,949.53 μs** |  **8,474.53 μs** |   **464.518 μs** |  **1.00** |    **0.08** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,123.29 μs |    456.92 μs |    25.045 μs |  0.16 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,630.94 μs** |  **4,259.72 μs** |   **233.490 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,891.23 μs |  6,331.17 μs |   347.033 μs |  0.59 |    0.03 |  15.6250 |       - |  361.61 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.19 μs** |     **38.13 μs** |     **2.090 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     69.61 μs |    201.26 μs |    11.032 μs |  0.57 |    0.08 |        - |       - |    7.93 KB |        0.24 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,325.80 μs** |  **1,417.06 μs** |    **77.674 μs** |  **1.00** |    **0.07** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    584.50 μs |    319.32 μs |    17.503 μs |  0.44 |    0.03 |        - |       - |   70.52 KB |        0.21 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,009.73 μs** |    **160.19 μs** |     **8.781 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.61 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    196.10 μs |    304.87 μs |    16.711 μs |  0.19 |    0.01 |   0.9766 |       - |   98.66 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,025.54 μs** | **29,382.51 μs** | **1,610.555 μs** |  **1.02** |    **0.24** |  **74.2188** |       **-** | **1225.63 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,964.65 μs |  2,967.85 μs |   162.678 μs |  0.22 |    0.04 |   7.8125 |       - |  974.67 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,404.29 μs** |     **57.66 μs** |     **3.161 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,168.44 μs |  1,940.71 μs |   106.377 μs |  0.22 |    0.02 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,405.47 μs** |    **114.83 μs** |     **6.294 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,105.82 μs |     50.17 μs |     2.750 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.41 μs** |    **106.04 μs** |     **5.812 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,157.54 μs |  1,601.07 μs |    87.760 μs |  0.21 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,413.18 μs** |     **46.51 μs** |     **2.549 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,137.08 μs |     74.14 μs |     4.064 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.413 ms** |  **12.733 ms** | **0.6979 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.423 ms |  28.486 ms | 1.5614 ms | 0.005 |  598.83 KB |        8.03 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.673 ms** |  **16.497 ms** | **0.9043 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.652 ms |   3.583 ms | 0.1964 ms | 0.005 |  784.82 KB |        3.13 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.308 ms** |   **6.815 ms** | **0.3736 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.028 ms | 111.472 ms | 6.1102 ms | 0.006 | 1001.39 KB |        1.66 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.030 ms** |   **3.233 ms** | **0.1772 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.189 ms |  38.886 ms | 2.1315 ms | 0.005 | 2767.29 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.339 ms** |  **14.371 ms** | **0.7877 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.179 ms |  12.747 ms | 0.6987 ms | 0.002 |  184.73 KB |       76.77 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.728 ms** |   **5.704 ms** | **0.3127 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.872 ms |   5.161 ms | 0.2829 ms | 0.002 |  186.65 KB |       44.82 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.779 ms** |  **22.406 ms** | **1.2282 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.032 ms |   4.946 ms | 0.2711 ms | 0.002 |  184.95 KB |       76.86 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.155 ms** |  **27.892 ms** | **1.5289 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.499 ms |   7.600 ms | 0.4166 ms | 0.002 |  187.36 KB |       44.83 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.066 μs |  3.6721 μs | 0.2013 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.273 μs |  5.6022 μs | 0.3071 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.275 μs |  4.5059 μs | 0.2470 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.809 μs |  0.8440 μs | 0.0463 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.024 μs |  0.6494 μs | 0.0356 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.438 μs | 67.4849 μs | 3.6991 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.945 μs |  8.4331 μs | 0.4622 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.049 μs | 44.0753 μs | 2.4159 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.106 μs | 17.7767 μs | 0.9744 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.696 μs |  3.5894 μs | 0.1967 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,471.2 ns |  5,646.4 ns | 309.50 ns |  0.36 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,291.7 ns |  1,103.8 ns |  60.50 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,489.7 ns |  1,004.8 ns |  55.08 ns |  0.36 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,525.3 ns |    556.5 ns |  30.50 ns |  0.61 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    768.7 ns |  1,099.7 ns |  60.28 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,099.7 ns | 10,947.7 ns | 600.08 ns |  9.48 |    0.14 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,125.8 ns |    526.7 ns |  28.87 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,891.3 ns |  1,187.0 ns |  65.06 ns |  0.94 |    0.01 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error       | StdDev    | Allocated |
|------------------------ |------------:|------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.39 μs |    11.39 μs |  0.624 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   525.34 μs |   389.34 μs | 21.341 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.61 μs |    34.02 μs |  1.865 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,805.81 μs | 1,109.48 μs | 60.814 μs |    1280 B |


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