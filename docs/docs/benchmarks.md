---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 00:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,277.81 μs** |    **845.46 μs** |    **46.343 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,845.16 μs |  2,098.38 μs |   115.020 μs |  0.29 |    0.02 |       - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,272.95 μs** |    **399.64 μs** |    **21.906 μs** |  **1.00** |    **0.00** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,377.30 μs |  1,916.91 μs |   105.073 μs |  0.33 |    0.01 |  7.8125 |       - |  339.65 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,778.61 μs** |    **233.98 μs** |    **12.825 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,694.01 μs |  1,724.47 μs |    94.524 μs |  0.25 |    0.01 |       - |       - |   36.58 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,215.20 μs** |  **3,076.71 μs** |   **168.645 μs** |  **1.00** |    **0.02** | **78.1250** | **46.8750** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 6,463.56 μs | 18,659.00 μs | 1,022.763 μs |  0.70 |    0.10 |       - |       - |   356.2 KB |        0.18 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **120.32 μs** |     **17.42 μs** |     **0.955 μs** |  **1.00** |    **0.01** |  **1.3428** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    83.17 μs |     30.25 μs |     1.658 μs |  0.69 |    0.01 |       - |       - |    8.02 KB |        0.24 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,203.16 μs** |    **445.89 μs** |    **24.441 μs** |  **1.00** |    **0.03** | **13.6719** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   951.50 μs |  1,513.58 μs |    82.965 μs |  0.79 |    0.06 |       - |       - |   41.93 KB |        0.12 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **782.58 μs** |     **28.20 μs** |     **1.546 μs** |  **1.00** |    **0.00** |  **4.8828** |       **-** |  **122.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   213.39 μs |    321.47 μs |    17.621 μs |  0.27 |    0.02 |       - |       - |   91.99 KB |        0.75 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,834.18 μs** |  **2,290.07 μs** |   **125.526 μs** |  **1.00** |    **0.02** | **48.8281** |       **-** | **1221.12 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,063.09 μs |  2,396.95 μs |   131.385 μs |  0.26 |    0.01 |       - |       - |  853.39 KB |        0.70 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,485.44 μs** |     **33.40 μs** |     **1.831 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,106.93 μs |     49.49 μs |     2.713 μs |  0.20 |    0.00 |       - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,483.34 μs** |     **49.44 μs** |     **2.710 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,108.18 μs |     35.93 μs |     1.970 μs |  0.20 |    0.00 |       - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,498.35 μs** |    **272.57 μs** |    **14.940 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,107.91 μs |     20.41 μs |     1.119 μs |  0.20 |    0.00 |       - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,505.89 μs** |    **189.99 μs** |    **10.414 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,106.69 μs |     13.04 μs |     0.715 μs |  0.20 |    0.00 |       - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169,866.78 μs** |  **24,378.76 μs** | **1,336.282 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    20,234.98 μs |  54,076.74 μs | 2,964.129 μs | 0.006 |    0.00 |  618504 B |        8.09 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167,159.68 μs** |  **17,386.33 μs** |   **953.003 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,627.13 μs |  29,317.79 μs | 1,607.007 μs | 0.005 |    0.00 |  815504 B |        3.18 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167,081.52 μs** |  **19,064.70 μs** | **1,045.001 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20,652.91 μs | 101,508.70 μs | 5,564.034 μs | 0.007 |    0.00 | 1032808 B |        1.68 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167,297.67 μs** |   **7,060.16 μs** |   **386.991 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18,229.77 μs |  70,712.14 μs | 3,875.971 μs | 0.006 |    0.00 | 2842576 B |        1.17 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **55.02 μs** |     **296.06 μs** |    **16.228 μs** |  **1.05** |    **0.36** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        45.09 μs |     114.08 μs |     6.253 μs |  0.86 |    0.22 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **43.09 μs** |      **71.22 μs** |     **3.904 μs** |  **1.01** |    **0.11** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        49.13 μs |     105.62 μs |     5.789 μs |  1.15 |    0.15 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **48.01 μs** |     **169.86 μs** |     **9.311 μs** |  **1.02** |    **0.24** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        43.19 μs |     156.29 μs |     8.567 μs |  0.92 |    0.22 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **45.93 μs** |      **73.10 μs** |     **4.007 μs** |  **1.01** |    **0.11** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        51.20 μs |     138.71 μs |     7.603 μs |  1.12 |    0.17 |    2472 B |        0.93 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.267 μs |  0.9339 μs | 0.0512 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.168 μs |  2.4295 μs | 0.1332 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.608 μs |  3.4821 μs | 0.1909 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 12.990 μs |  2.0748 μs | 0.1137 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.481 μs |  4.7525 μs | 0.2605 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.987 μs |  2.6998 μs | 0.1480 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 16.607 μs | 60.9137 μs | 3.3389 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.994 μs |  3.2751 μs | 0.1795 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.987 μs |  0.6320 μs | 0.0346 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.233 μs |  1.5842 μs | 0.0868 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.653 μs | 22.0166 μs | 1.2068 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.541 μs | 27.3499 μs | 1.4991 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.715 μs |  0.7297 μs | 0.0400 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.756 μs |  4.7936 μs | 0.2628 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,333.3 ns |  1,804.2 ns |  98.90 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,432.7 ns |  3,110.6 ns | 170.50 ns |  0.33 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,417.8 ns |  1,025.6 ns |  56.22 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,625.0 ns |  2,554.1 ns | 140.00 ns |  0.61 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    738.7 ns |  2,176.5 ns | 119.30 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,779.7 ns | 12,500.9 ns | 685.22 ns |  9.51 |    0.23 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,289.5 ns |  1,820.4 ns |  99.78 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,114.7 ns |  2,449.9 ns | 134.29 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.352 μs |   3.003 μs |  0.1646 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 506.954 μs | 320.910 μs | 17.5902 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.736 μs |   5.148 μs |  0.2822 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 241.884 μs | 272.638 μs | 14.9442 μs |      80 B |


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