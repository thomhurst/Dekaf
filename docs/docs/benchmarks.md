---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 14:07 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,108.48 μs** |    **300.818 μs** |    **16.489 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,429.86 μs |  2,382.402 μs |   130.587 μs |  0.23 |    0.02 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,413.11 μs** |    **750.494 μs** |    **41.137 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,450.51 μs |    149.013 μs |     8.168 μs |  0.33 |    0.00 |  15.6250 |       - |  339.82 KB |        0.32 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,255.73 μs** |    **688.728 μs** |    **37.752 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,805.17 μs |    729.868 μs |    40.007 μs |  0.29 |    0.01 |        - |       - |   36.61 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,536.47 μs** |  **1,331.060 μs** |    **72.960 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,903.52 μs |  4,266.987 μs |   233.888 μs |  0.55 |    0.02 |  15.6250 |       - |  359.73 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.94 μs** |     **43.495 μs** |     **2.384 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     70.62 μs |    101.745 μs |     5.577 μs |  0.52 |    0.04 |        - |       - |    7.06 KB |        0.21 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,378.77 μs** |    **406.660 μs** |    **22.290 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    757.67 μs |  2,004.213 μs |   109.858 μs |  0.55 |    0.07 |        - |       - |   84.28 KB |        0.25 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **635.71 μs** |  **7,932.113 μs** |   **434.786 μs** |  **1.47** |    **1.42** |   **7.3242** |       **-** |  **122.63 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    226.70 μs |     15.338 μs |     0.841 μs |  0.53 |    0.35 |   0.9766 |       - |   96.02 KB |        0.78 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,468.31 μs** | **22,874.417 μs** | **1,253.824 μs** |  **1.01** |    **0.15** |  **74.2188** |       **-** | **1226.45 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,071.31 μs |  4,443.175 μs |   243.545 μs |  0.30 |    0.04 |        - |       - |  929.16 KB |        0.76 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,423.70 μs** |     **65.380 μs** |     **3.584 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,136.80 μs |    552.204 μs |    30.268 μs |  0.21 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.17 μs** |     **51.855 μs** |     **2.842 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,117.49 μs |     33.613 μs |     1.842 μs |  0.21 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,429.42 μs** |     **44.145 μs** |     **2.420 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,116.18 μs |      8.677 μs |     0.476 μs |  0.21 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,436.68 μs** |    **128.110 μs** |     **7.022 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,390.48 μs |    335.059 μs |    18.366 μs |  0.26 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166,711.41 μs** | **23,116.67 μs** | **1,267.103 μs** | **3,166,802.02 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,269.67 μs | 15,955.45 μs |   874.572 μs |    15,703.46 μs | 0.005 |    0.00 |  618904 B |        8.10 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,293.81 μs** | **12,068.83 μs** |   **661.533 μs** | **3,165,403.30 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13,536.36 μs | 12,768.00 μs |   699.857 μs |    13,726.82 μs | 0.004 |    0.00 |  807336 B |        3.15 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,913.29 μs** | **37,017.07 μs** | **2,029.030 μs** | **3,166,335.40 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19,985.10 μs | 92,651.09 μs | 5,078.519 μs |    21,429.57 μs | 0.006 |    0.00 | 1031352 B |        1.67 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,686.28 μs** | **16,749.25 μs** |   **918.083 μs** | **3,164,799.53 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17,659.36 μs | 19,937.28 μs | 1,092.830 μs |    17,442.10 μs | 0.006 |    0.00 | 2838576 B |        1.17 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **31.16 μs** |     **43.83 μs** |     **2.402 μs** |        **31.84 μs** |  **1.00** |    **0.10** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        27.46 μs |    126.17 μs |     6.916 μs |        26.51 μs |  0.88 |    0.20 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **26.86 μs** |     **55.61 μs** |     **3.048 μs** |        **26.05 μs** |  **1.01** |    **0.14** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        24.35 μs |     72.38 μs |     3.968 μs |        24.92 μs |  0.91 |    0.16 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **28.67 μs** |     **95.09 μs** |     **5.212 μs** |        **28.85 μs** |  **1.02** |    **0.23** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        35.21 μs |    316.97 μs |    17.374 μs |        25.68 μs |  1.26 |    0.58 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **29.27 μs** |     **70.39 μs** |     **3.858 μs** |        **28.93 μs** |  **1.01** |    **0.16** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        30.10 μs |    238.89 μs |    13.094 μs |        23.46 μs |  1.04 |    0.41 |    2440 B |        0.91 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.002 μs |  4.782 μs | 0.2621 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.626 μs |  3.650 μs | 0.2001 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.155 μs |  2.869 μs | 0.1573 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.344 μs |  1.215 μs | 0.0666 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.905 μs | 35.274 μs | 1.9335 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.028 μs |  7.051 μs | 0.3865 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.074 μs |  4.023 μs | 0.2205 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.971 μs |  4.390 μs | 0.2406 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.987 μs | 67.652 μs | 3.7082 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.180 μs |  2.320 μs | 0.1272 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 29.575 μs | 24.354 μs | 1.3350 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.919 μs | 34.542 μs | 1.8934 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.607 μs |  2.012 μs | 0.1103 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.686 μs |  3.749 μs | 0.2055 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,222.2 ns |   475.9 ns |  26.08 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,481.7 ns | 8,204.8 ns | 449.73 ns |  0.36 |    0.10 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,350.0 ns | 1,742.9 ns |  95.54 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,725.2 ns | 6,199.7 ns | 339.83 ns |  0.67 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    774.8 ns | 3,172.2 ns | 173.88 ns |  0.19 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,791.7 ns | 5,308.9 ns | 291.00 ns | 10.27 |    0.10 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,070.3 ns |   690.7 ns |  37.86 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,942.2 ns | 4,612.1 ns | 252.80 ns |  0.97 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.381 μs |   5.840 μs |  0.3201 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 536.138 μs | 187.025 μs | 10.2515 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.863 μs |   2.072 μs |  0.1136 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 242.132 μs |  97.945 μs |  5.3687 μs |      80 B |


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