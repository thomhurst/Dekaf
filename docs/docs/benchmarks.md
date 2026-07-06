---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 11:28 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,133.79 μs** |  **1,665.726 μs** |    **91.304 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,592.53 μs |  1,495.892 μs |    81.995 μs |  0.26 |    0.01 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,231.03 μs** |  **1,525.665 μs** |    **83.627 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,330.88 μs |    538.869 μs |    29.537 μs |  0.32 |    0.00 |  15.6250 |       - |  339.66 KB |        0.32 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,574.04 μs** |  **1,215.859 μs** |    **66.645 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,661.42 μs |  1,294.861 μs |    70.976 μs |  0.25 |    0.01 |        - |       - |   36.52 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,960.14 μs** |  **2,101.581 μs** |   **115.195 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,513.09 μs |  3,666.810 μs |   200.990 μs |  0.54 |    0.02 |  15.6250 |       - |  359.55 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **120.93 μs** |     **53.567 μs** |     **2.936 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     92.52 μs |      0.395 μs |     0.022 μs |  0.77 |    0.02 |        - |       - |    5.89 KB |        0.18 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,236.22 μs** |    **553.004 μs** |    **30.312 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    812.59 μs |  1,983.994 μs |   108.749 μs |  0.66 |    0.08 |        - |       - |    82.9 KB |        0.25 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **564.49 μs** |  **7,102.481 μs** |   **389.311 μs** |  **1.45** |    **1.36** |   **7.3242** |       **-** |   **122.4 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    237.41 μs |    338.892 μs |    18.576 μs |  0.61 |    0.39 |        - |       - |   81.88 KB |        0.67 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,592.83 μs** | **20,242.188 μs** | **1,109.543 μs** |  **1.01** |    **0.15** |  **74.2188** |       **-** | **1225.07 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,315.97 μs |  6,458.839 μs |   354.031 μs |  0.24 |    0.04 |   7.8125 |       - |  978.14 KB |        0.80 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,438.11 μs** |    **142.661 μs** |     **7.820 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,256.44 μs |    485.649 μs |    26.620 μs |  0.23 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,443.24 μs** |    **170.362 μs** |     **9.338 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,324.79 μs |    344.532 μs |    18.885 μs |  0.24 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,437.47 μs** |     **51.270 μs** |     **2.810 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,328.18 μs |    357.132 μs |    19.576 μs |  0.24 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,413.57 μs** |    **131.855 μs** |     **7.227 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,362.67 μs |    699.683 μs |    38.352 μs |  0.25 |    0.01 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,172,242.62 μs** | **31,406.68 μs** | **1,721.506 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    20,124.06 μs | 58,123.77 μs | 3,185.960 μs | 0.006 |    0.00 |  620672 B |        8.12 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167,851.49 μs** | **16,840.09 μs** |   **923.062 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,439.77 μs | 16,893.28 μs |   925.978 μs | 0.005 |    0.00 |  815824 B |        3.18 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167,365.03 μs** | **15,367.32 μs** |   **842.335 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19,258.48 μs |  9,469.35 μs |   519.047 μs | 0.006 |    0.00 | 1036632 B |        1.68 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167,621.46 μs** | **10,409.29 μs** |   **570.568 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17,812.45 μs | 22,099.98 μs | 1,211.374 μs | 0.006 |    0.00 | 2844288 B |        1.17 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **59.11 μs** |    **131.86 μs** |     **7.228 μs** |  **1.01** |    **0.15** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        31.77 μs |    100.34 μs |     5.500 μs |  0.54 |    0.10 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **55.51 μs** |    **242.03 μs** |    **13.267 μs** |  **1.04** |    **0.29** |    **3000 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        34.62 μs |    149.53 μs |     8.196 μs |  0.65 |    0.18 |    2312 B |        0.77 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **45.33 μs** |     **71.36 μs** |     **3.911 μs** |  **1.00** |    **0.10** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        33.09 μs |    103.81 μs |     5.690 μs |  0.73 |    0.12 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **53.48 μs** |    **274.37 μs** |    **15.039 μs** |  **1.05** |    **0.34** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        34.12 μs |     39.96 μs |     2.191 μs |  0.67 |    0.15 |    2408 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.273 μs |  4.5997 μs | 0.2521 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.742 μs |  1.9062 μs | 0.1045 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.396 μs |  5.9752 μs | 0.3275 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.331 μs |  3.3297 μs | 0.1825 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.192 μs | 38.9063 μs | 2.1326 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.710 μs |  6.3182 μs | 0.3463 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.544 μs |  5.2591 μs | 0.2883 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.086 μs |  0.7359 μs | 0.0403 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.910 μs |  1.4171 μs | 0.0777 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.421 μs |  1.5939 μs | 0.0874 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.594 μs | 19.8668 μs | 1.0890 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.130 μs | 24.3666 μs | 1.3356 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.586 μs |  2.2069 μs | 0.1210 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.744 μs |  4.7057 μs | 0.2579 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,311.7 ns |   1,323.2 ns |     72.53 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,299.7 ns |     918.2 ns |     50.33 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,389.7 ns |   2,847.1 ns |    156.06 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,510.7 ns |   1,214.7 ns |     66.58 ns |  0.61 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    828.7 ns |     822.7 ns |     45.09 ns |  0.20 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 47,346.5 ns | 207,272.3 ns | 11,361.30 ns | 11.55 |    2.43 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,104.7 ns |   2,847.1 ns |    156.06 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,960.0 ns |   2,305.3 ns |    126.36 ns |  0.97 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.113 μs |   3.151 μs |  0.1727 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 521.005 μs | 222.616 μs | 12.2023 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.873 μs |   3.319 μs |  0.1819 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 233.834 μs | 257.064 μs | 14.0906 μs |      80 B |


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