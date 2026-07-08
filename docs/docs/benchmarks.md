---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 02:38 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,583.83 μs** |    **635.79 μs** |    **34.850 μs** | **5,573.15 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,284.13 μs |  1,012.29 μs |    55.487 μs | 1,306.48 μs |  0.23 |    0.01 |   1.9531 |       - |   34.76 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,573.49 μs** |    **561.91 μs** |    **30.800 μs** | **6,564.49 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 1,268.37 μs |  1,862.38 μs |   102.083 μs | 1,220.24 μs |  0.19 |    0.01 |  19.5313 |  3.9063 |  339.76 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **5,941.59 μs** |  **1,287.30 μs** |    **70.561 μs** | **5,912.48 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,100.71 μs |    317.05 μs |    17.378 μs | 1,094.30 μs |  0.19 |    0.00 |        - |       - |   36.55 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **6,895.23 μs** |  **1,156.53 μs** |    **63.393 μs** | **6,898.26 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 3,146.15 μs |  1,892.38 μs |   103.728 μs | 3,091.28 μs |  0.46 |    0.01 |  15.6250 |       - |  359.51 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **60.86 μs** |     **22.85 μs** |     **1.252 μs** |    **60.94 μs** |  **1.00** |    **0.03** |   **2.0142** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    34.59 μs |     46.13 μs |     2.529 μs |    33.19 μs |  0.57 |    0.04 |   0.2441 |       - |    6.84 KB |        0.20 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **656.24 μs** |    **226.11 μs** |    **12.394 μs** |   **651.63 μs** |  **1.00** |    **0.02** |  **20.5078** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   335.80 μs |    426.18 μs |    23.360 μs |   327.96 μs |  0.51 |    0.03 |   1.9531 |       - |   83.23 KB |        0.25 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **570.52 μs** |  **1,370.81 μs** |    **75.138 μs** |   **539.69 μs** |  **1.01** |    **0.16** |   **7.4463** |       **-** |  **122.29 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   122.96 μs |    139.13 μs |     7.626 μs |   125.37 μs |  0.22 |    0.03 |   0.4883 |       - |   68.31 KB |        0.56 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **6,103.57 μs** | **21,326.09 μs** | **1,168.955 μs** | **5,622.79 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1220.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,436.46 μs |  4,086.63 μs |   224.002 μs | 1,510.50 μs |  0.24 |    0.05 |   7.8125 |       - |  950.65 KB |        0.78 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,226.60 μs** |    **128.35 μs** |     **7.035 μs** | **5,222.84 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,589.71 μs | 11,677.57 μs |   640.087 μs | 1,222.66 μs |  0.30 |    0.11 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,220.31 μs** |     **26.07 μs** |     **1.429 μs** | **5,219.66 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,231.27 μs |    505.56 μs |    27.711 μs | 1,247.16 μs |  0.24 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,223.13 μs** |     **64.31 μs** |     **3.525 μs** | **5,224.56 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,207.02 μs |    274.81 μs |    15.063 μs | 1,210.49 μs |  0.23 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,224.63 μs** |     **40.90 μs** |     **2.242 μs** | **5,223.45 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,165.81 μs |     80.34 μs |     4.404 μs | 1,163.85 μs |  0.22 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166,593.11 μs** | **124,548.45 μs** | **6,826.921 μs** | **3,162,793.77 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13,633.51 μs |  89,999.21 μs | 4,933.160 μs |    10,792.52 μs | 0.004 |    0.00 |  618440 B |        8.09 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,161,553.51 μs** |  **20,583.31 μs** | **1,128.241 μs** | **3,161,144.88 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |     9,722.41 μs |   7,419.47 μs |   406.686 μs |     9,536.97 μs | 0.003 |    0.00 |  817088 B |        3.19 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,160,271.71 μs** |   **8,709.17 μs** |   **477.379 μs** | **3,160,288.21 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    10,711.61 μs |  26,027.65 μs | 1,426.664 μs |    11,053.79 μs | 0.003 |    0.00 | 1040120 B |        1.69 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,161,298.73 μs** |  **18,822.57 μs** | **1,031.729 μs** | **3,161,128.11 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    11,345.57 μs |  16,822.00 μs |   922.071 μs |    11,395.06 μs | 0.004 |    0.00 | 2841440 B |        1.17 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **28.34 μs** |     **283.23 μs** |    **15.525 μs** |        **20.09 μs** |  **1.18** |    **0.73** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        21.10 μs |      68.35 μs |     3.746 μs |        20.78 μs |  0.88 |    0.35 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **28.60 μs** |     **211.07 μs** |    **11.569 μs** |        **22.55 μs** |  **1.10** |    **0.51** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        28.65 μs |     316.88 μs |    17.369 μs |        20.68 μs |  1.10 |    0.68 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **37.41 μs** |     **164.99 μs** |     **9.044 μs** |        **41.78 μs** |  **1.05** |    **0.34** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        24.51 μs |      61.22 μs |     3.356 μs |        25.95 μs |  0.69 |    0.19 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **50.03 μs** |     **242.19 μs** |    **13.275 μs** |        **47.40 μs** |  **1.05** |    **0.34** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        45.42 μs |     359.16 μs |    19.687 μs |        41.05 μs |  0.95 |    0.42 |    2344 B |        0.88 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.297 μs |  3.652 μs | 0.2002 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.836 μs |  1.109 μs | 0.0608 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.004 μs |  4.978 μs | 0.2728 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  6.380 μs |  5.159 μs | 0.2828 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.762 μs |  3.025 μs | 0.1658 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          |  9.968 μs |  4.075 μs | 0.2234 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     |  9.715 μs |  3.662 μs | 0.2007 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.635 μs |  2.907 μs | 0.1593 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  7.976 μs |  4.188 μs | 0.2295 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.688 μs | 76.246 μs | 4.1793 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.102 μs | 93.688 μs | 5.1354 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 25.475 μs | 38.410 μs | 2.1054 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.626 μs |  4.071 μs | 0.2232 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.273 μs | 16.715 μs | 0.9162 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |    976.2 ns |     921.3 ns |    50.50 ns |  0.29 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,082.0 ns |   1,621.5 ns |    88.88 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,012.0 ns |   2,808.6 ns |   153.95 ns |  0.30 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,129.8 ns |   8,419.4 ns |   461.50 ns |  0.63 |    0.12 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    674.3 ns |   2,971.7 ns |   162.89 ns |  0.20 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,275.3 ns | 155,250.8 ns | 8,509.82 ns | 12.23 |    2.23 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,378.7 ns |   2,566.5 ns |   140.68 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  2,964.0 ns |   6,913.6 ns |   378.96 ns |  0.88 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   9.017 μs |   7.504 μs | 0.4113 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 411.894 μs | 160.383 μs | 8.7911 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   5.242 μs |  12.322 μs | 0.6754 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 174.106 μs | 148.844 μs | 8.1586 μs |      80 B |


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