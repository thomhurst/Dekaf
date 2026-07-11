---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 00:21 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,947.22 μs** |   **478.273 μs** |  **26.216 μs** |  **1.00** |    **0.01** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,466.76 μs |   640.037 μs |  35.083 μs |  0.25 |    0.01 |       - |   34.74 KB |        0.33 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,338.72 μs** | **1,161.211 μs** |  **63.650 μs** |  **1.00** |    **0.01** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,365.95 μs | 1,143.555 μs |  62.682 μs |  0.32 |    0.01 |       - |  341.36 KB |        0.32 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,367.12 μs** |   **170.836 μs** |   **9.364 μs** |  **1.00** |    **0.00** |       **-** |  **194.03 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,573.58 μs | 6,335.660 μs | 347.279 μs |  0.25 |    0.05 |       - |   38.29 KB |        0.20 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,994.80 μs** | **3,868.200 μs** | **212.029 μs** |  **1.00** |    **0.03** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,662.25 μs | 2,695.905 μs | 147.772 μs |  0.71 |    0.02 |       - |  368.27 KB |        0.19 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **118.52 μs** |     **8.699 μs** |   **0.477 μs** |  **1.00** |    **0.00** |  **0.3662** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    66.18 μs |   160.829 μs |   8.816 μs |  0.56 |    0.06 |       - |    7.12 KB |        0.21 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,053.63 μs** | **2,707.347 μs** | **148.399 μs** |  **1.01** |    **0.17** |  **3.9063** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   677.89 μs |   355.226 μs |  19.471 μs |  0.65 |    0.08 |       - |   45.39 KB |        0.14 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **611.24 μs** |   **205.141 μs** |  **11.244 μs** |  **1.00** |    **0.02** |  **1.4648** |  **121.83 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   217.50 μs |   363.892 μs |  19.946 μs |  0.36 |    0.03 |       - |   100.8 KB |        0.83 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **6,210.04 μs** | **5,523.585 μs** | **302.766 μs** |  **1.00** |    **0.06** | **13.6719** | **1218.02 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,750.74 μs | 6,110.889 μs | 334.958 μs |  0.28 |    0.05 |       - |  966.78 KB |        0.79 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,339.58 μs** |   **535.917 μs** |  **29.375 μs** |  **1.00** |    **0.01** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,097.58 μs |    43.287 μs |   2.373 μs |  0.21 |    0.00 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,324.23 μs** |   **164.346 μs** |   **9.008 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,296.94 μs |   196.920 μs |  10.794 μs |  0.24 |    0.00 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,312.84 μs** |    **49.937 μs** |   **2.737 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,258.50 μs | 1,909.327 μs | 104.657 μs |  0.24 |    0.02 |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |            |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,326.60 μs** |   **252.982 μs** |  **13.867 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,311.65 μs |   320.636 μs |  17.575 μs |  0.25 |    0.00 |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev        | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|--------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168,568.11 μs** |  **72,615.69 μs** |  **3,980.311 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,665.12 μs |  30,895.87 μs |  1,693.507 μs | 0.005 |    0.00 |  632848 B |        8.28 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,175,854.05 μs** | **304,178.82 μs** | **16,673.068 μs** | **1.000** |    **0.01** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,369.95 μs |  20,945.94 μs |  1,148.118 μs | 0.005 |    0.00 |  826096 B |        3.22 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164,920.75 μs** |  **15,893.57 μs** |    **871.180 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15,211.75 μs |  24,648.67 μs |  1,351.077 μs | 0.005 |    0.00 | 1039456 B |        1.69 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165,060.66 μs** |  **20,538.38 μs** |  **1,125.778 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14,756.90 μs |   4,365.01 μs |    239.261 μs | 0.005 |    0.00 | 2851456 B |        1.18 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **34.71 μs** |      **51.64 μs** |      **2.830 μs** |  **1.00** |    **0.10** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        39.68 μs |      80.61 μs |      4.418 μs |  1.15 |    0.14 |     512 B |        0.59 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **35.19 μs** |      **67.95 μs** |      **3.725 μs** |  **1.01** |    **0.14** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        42.05 μs |     145.75 μs |      7.989 μs |  1.20 |    0.23 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **41.27 μs** |     **160.94 μs** |      **8.821 μs** |  **1.03** |    **0.26** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        42.61 μs |      66.73 μs |      3.657 μs |  1.06 |    0.20 |     512 B |        0.59 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **39.79 μs** |      **98.42 μs** |      **5.395 μs** |  **1.01** |    **0.17** |    **2736 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        43.49 μs |      78.63 μs |      4.310 μs |  1.11 |    0.16 |    2376 B |        0.87 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.898 μs |   2.867 μs |  0.1572 μs | 25.928 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.353 μs |  41.475 μs |  2.2734 μs | 11.081 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 13.441 μs |   8.693 μs |  0.4765 μs | 13.676 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.536 μs |   2.198 μs |  0.1205 μs |  8.536 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.728 μs |   4.539 μs |  0.2488 μs | 11.802 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.208 μs |   3.623 μs |  0.1986 μs | 13.125 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.355 μs |   5.225 μs |  0.2864 μs | 13.194 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 40.600 μs | 305.614 μs | 16.7517 μs | 31.089 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.859 μs |   1.552 μs |  0.0850 μs |  8.826 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.290 μs |   2.463 μs |  0.1350 μs | 20.287 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 38.732 μs |  81.494 μs |  4.4669 μs | 37.119 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 42.462 μs | 107.400 μs |  5.8869 μs | 45.745 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.537 μs |  11.951 μs |  0.6551 μs |  5.279 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.098 μs |  22.755 μs |  1.2473 μs | 13.636 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,418.3 ns |  1,581.0 ns |    86.66 ns |  0.30 |    0.08 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,412.0 ns |  1,424.9 ns |    78.10 ns |  0.30 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,503.0 ns |  2,104.0 ns |   115.33 ns |  0.32 |    0.08 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,592.0 ns |    221.2 ns |    12.12 ns |  0.55 |    0.14 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    661.2 ns |    173.4 ns |     9.50 ns |  0.14 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,962.3 ns |  8,838.2 ns |   484.45 ns |  8.97 |    2.20 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,998.3 ns | 30,753.4 ns | 1,685.70 ns |  1.07 |    0.41 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,083.7 ns |  1,916.3 ns |   105.04 ns |  0.87 |    0.22 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.256 μs |   8.045 μs |  0.4410 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 563.003 μs | 943.622 μs | 51.7231 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.194 μs |   1.387 μs |  0.0760 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 263.455 μs | 345.631 μs | 18.9452 μs |      80 B |


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