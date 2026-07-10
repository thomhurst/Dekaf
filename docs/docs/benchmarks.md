---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-10 19:50 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,081.4 μs** |    **272.81 μs** |    **14.95 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,379.0 μs |  1,004.04 μs |    55.03 μs |  0.23 |    0.01 |        - |       - |   34.74 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,368.1 μs** |  **1,750.53 μs** |    **95.95 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,455.1 μs |  1,358.88 μs |    74.48 μs |  0.33 |    0.01 |  15.6250 |       - |  341.51 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,256.7 μs** |  **1,257.80 μs** |    **68.94 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,794.8 μs |  1,131.93 μs |    62.05 μs |  0.29 |    0.01 |        - |       - |   38.32 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,688.4 μs** |  **6,775.80 μs** |   **371.40 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,983.2 μs |  8,079.76 μs |   442.88 μs |  0.63 |    0.03 |  15.6250 |       - |  396.27 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.0 μs** |     **30.62 μs** |     **1.68 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    105.6 μs |    384.46 μs |    21.07 μs |  0.79 |    0.14 |        - |       - |    7.36 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,352.2 μs** |    **284.27 μs** |    **15.58 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    838.9 μs |    120.95 μs |     6.63 μs |  0.62 |    0.01 |        - |       - |  115.28 KB |        0.34 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,076.9 μs** |     **57.70 μs** |     **3.16 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    435.2 μs |  1,103.01 μs |    60.46 μs |  0.40 |    0.05 |        - |       - |   88.78 KB |        0.72 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,709.3 μs** | **36,878.39 μs** | **2,021.43 μs** |  **1.03** |    **0.28** |  **74.2188** |       **-** | **1226.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,740.9 μs |  5,764.27 μs |   315.96 μs |  0.40 |    0.09 |        - |       - | 1019.34 KB |        0.83 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,414.7 μs** |     **70.74 μs** |     **3.88 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,113.9 μs |      9.40 μs |     0.52 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,417.9 μs** |     **95.06 μs** |     **5.21 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,115.7 μs |     14.39 μs |     0.79 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.4 μs** |     **57.99 μs** |     **3.18 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,115.6 μs |     38.79 μs |     2.13 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,413.3 μs** |     **10.86 μs** |     **0.60 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,371.6 μs |    481.16 μs |    26.37 μs |  0.25 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167,088.05 μs** |  **18,462.71 μs** | **1,012.003 μs** | **3,166,597.03 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,930.52 μs |  47,257.51 μs | 2,590.344 μs |    16,877.17 μs | 0.005 |    0.00 |  623672 B |        8.16 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164,050.49 μs** |  **39,560.99 μs** | **2,168.471 μs** | **3,164,875.90 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13,927.11 μs |  19,142.36 μs | 1,049.257 μs |    14,075.10 μs | 0.004 |    0.00 |  822280 B |        3.21 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,565.81 μs** |  **10,656.63 μs** |   **584.126 μs** | **3,165,519.79 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    21,019.67 μs | 129,237.93 μs | 7,083.967 μs |    18,737.80 μs | 0.007 |    0.00 | 1034296 B |        1.68 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,868.35 μs** |  **17,060.62 μs** |   **935.150 μs** | **3,165,307.07 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15,980.10 μs |  59,564.79 μs | 3,264.947 μs |    14,804.80 μs | 0.005 |    0.00 | 2847176 B |        1.17 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **33.91 μs** |     **193.16 μs** |    **10.588 μs** |        **29.53 μs** |  **1.06** |    **0.39** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        23.88 μs |      45.92 μs |     2.517 μs |        23.76 μs |  0.75 |    0.19 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **27.91 μs** |      **46.68 μs** |     **2.558 μs** |        **27.75 μs** |  **1.01** |    **0.11** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        35.12 μs |     284.74 μs |    15.607 μs |        26.73 μs |  1.27 |    0.50 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **36.37 μs** |     **123.23 μs** |     **6.754 μs** |        **36.56 μs** |  **1.02** |    **0.24** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        24.91 μs |      96.69 μs |     5.300 μs |        23.33 μs |  0.70 |    0.17 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **29.48 μs** |      **40.42 μs** |     **2.215 μs** |        **29.22 μs** |  **1.00** |    **0.09** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        29.39 μs |      44.18 μs |     2.422 μs |        28.68 μs |  1.00 |    0.10 |    2408 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.175 μs |  1.836 μs | 0.1007 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.917 μs | 42.819 μs | 2.3471 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.494 μs |  2.123 μs | 0.1164 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.630 μs |  2.793 μs | 0.1531 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.414 μs |  5.494 μs | 0.3011 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.027 μs |  7.586 μs | 0.4158 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.915 μs |  4.135 μs | 0.2266 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.489 μs |  7.325 μs | 0.4015 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.116 μs |  8.244 μs | 0.4519 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.678 μs | 72.050 μs | 3.9493 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.634 μs | 73.228 μs | 4.0139 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.869 μs | 29.153 μs | 1.5980 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.550 μs |  7.823 μs | 0.4288 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.320 μs |  4.011 μs | 0.2199 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,813.0 ns |     316.0 ns |     17.32 ns |  0.42 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,396.7 ns |   1,654.1 ns |     90.67 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,594.0 ns |   7,822.0 ns |    428.75 ns |  0.37 |    0.09 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,435.0 ns |   2,071.4 ns |    113.54 ns |  0.56 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    814.0 ns |   1,462.6 ns |     80.17 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 50,766.7 ns | 284,560.0 ns | 15,597.69 ns | 11.69 |    3.24 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,365.3 ns |   7,266.4 ns |    398.30 ns |  1.01 |    0.11 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,904.5 ns |   5,165.3 ns |    283.13 ns |  1.13 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.639 μs |  16.723 μs |  0.9166 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 552.969 μs | 182.092 μs |  9.9811 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.827 μs |  39.847 μs |  2.1841 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 260.329 μs | 230.550 μs | 12.6372 μs |      80 B |


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