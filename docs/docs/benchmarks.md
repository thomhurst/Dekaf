---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 09:34 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,887.82 μs** |   **417.07 μs** |  **22.861 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,247.52 μs | 1,414.04 μs |  77.508 μs |  0.21 |    0.01 |        - |       - |   34.82 KB |        0.33 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,084.17 μs** | **1,160.96 μs** |  **63.636 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,237.60 μs |   284.62 μs |  15.601 μs |  0.32 |    0.00 |  15.6250 |       - |  339.82 KB |        0.32 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,394.96 μs** |   **240.63 μs** |  **13.190 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,153.23 μs |   539.28 μs |  29.560 μs |  0.18 |    0.00 |        - |       - |   36.71 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,858.30 μs** | **1,435.22 μs** |  **78.669 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,600.21 μs | 1,875.27 μs | 102.790 μs |  0.52 |    0.01 |  15.6250 |       - |  364.24 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **92.19 μs** |    **29.18 μs** |   **1.599 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    42.63 μs |    18.40 μs |   1.008 μs |  0.46 |    0.01 |   0.2441 |       - |   18.13 KB |        0.54 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,023.28 μs** |   **156.47 μs** |   **8.577 μs** |  **1.00** |    **0.01** |  **20.5078** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   655.30 μs | 1,279.61 μs |  70.140 μs |  0.64 |    0.06 |        - |       - |   67.63 KB |        0.20 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **954.83 μs** | **2,658.00 μs** | **145.694 μs** |  **1.02** |    **0.20** |   **7.3242** |       **-** |  **122.06 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   159.73 μs |   105.50 μs |   5.783 μs |  0.17 |    0.02 |   0.9766 |       - |   99.87 KB |        0.82 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,868.25 μs** | **3,152.56 μs** | **172.802 μs** |  **1.00** |    **0.03** |  **74.2188** |       **-** | **1224.96 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,869.77 μs | 1,864.15 μs | 102.180 μs |  0.24 |    0.01 |   7.8125 |       - |  912.65 KB |        0.75 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,339.40 μs** |    **67.53 μs** |   **3.702 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,252.49 μs |   351.75 μs |  19.280 μs |  0.23 |    0.00 |        - |       - |    1.19 KB |        1.01 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,337.06 μs** |    **52.81 μs** |   **2.895 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,093.55 μs |    31.66 μs |   1.735 μs |  0.20 |    0.00 |        - |       - |    1.19 KB |        1.01 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,352.11 μs** |   **119.90 μs** |   **6.572 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,318.25 μs |   219.11 μs |  12.010 μs |  0.25 |    0.00 |        - |       - |    1.19 KB |        0.58 |
|                         |               |             |           |             |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,347.22 μs** |   **116.35 μs** |   **6.377 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,254.75 μs |   807.23 μs |  44.247 μs |  0.23 |    0.01 |        - |       - |    1.19 KB |        0.58 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167,294.38 μs** | **35,043.90 μs** | **1,920.874 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14,821.91 μs | 22,078.80 μs | 1,210.214 μs | 0.005 |    0.00 |  617904 B |        8.09 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164,300.97 μs** | **13,430.07 μs** |   **736.148 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12,587.37 μs | 34,121.17 μs | 1,870.296 μs | 0.004 |    0.00 |  809720 B |        3.16 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164,331.56 μs** |  **2,706.42 μs** |   **148.348 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15,273.91 μs |  5,615.29 μs |   307.793 μs | 0.005 |    0.00 | 1030016 B |        1.67 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,168,723.72 μs** | **85,566.50 μs** | **4,690.189 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13,790.29 μs | 11,696.08 μs |   641.102 μs | 0.004 |    0.00 | 2914880 B |        1.20 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **34.44 μs** |    **102.41 μs** |     **5.613 μs** |  **1.02** |    **0.21** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        45.45 μs |    276.35 μs |    15.148 μs |  1.34 |    0.44 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **36.13 μs** |    **212.28 μs** |    **11.636 μs** |  **1.08** |    **0.44** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        25.09 μs |     51.56 μs |     2.826 μs |  0.75 |    0.23 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **39.68 μs** |     **31.82 μs** |     **1.744 μs** |  **1.00** |    **0.05** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        24.50 μs |     54.49 μs |     2.987 μs |  0.62 |    0.07 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **35.84 μs** |    **119.97 μs** |     **6.576 μs** |  **1.02** |    **0.23** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        25.66 μs |     23.79 μs |     1.304 μs |  0.73 |    0.12 |    2408 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.040 μs |   2.001 μs |  0.1097 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.411 μs |   5.108 μs |  0.2800 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.576 μs |   7.526 μs |  0.4125 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.147 μs | 184.379 μs | 10.1065 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.924 μs |   2.346 μs |  0.1286 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.825 μs |  14.672 μs |  0.8042 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 33.225 μs |  45.753 μs |  2.5079 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 38.556 μs |  69.980 μs |  3.8358 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.842 μs |   6.330 μs |  0.3470 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.739 μs |   3.752 μs |  0.2057 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,397.5 ns |    482.7 ns |    26.46 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,333.0 ns |  1,930.7 ns |   105.83 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,499.7 ns |  1,214.7 ns |    66.58 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,045.5 ns |  4,654.8 ns |   255.15 ns |  0.73 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    762.0 ns |    482.7 ns |    26.46 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,215.5 ns | 26,417.7 ns | 1,448.04 ns |  9.69 |    0.55 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,158.3 ns |  4,277.4 ns |   234.46 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,029.5 ns |  3,430.0 ns |   188.01 ns |  0.97 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean      | Error     | StdDev    | Median     | Allocated |
|------------------------ |----------:|----------:|----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.36 μs |  22.03 μs |  1.208 μs |  13.224 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 535.39 μs | 411.70 μs | 22.567 μs | 546.222 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  16.29 μs | 241.29 μs | 13.226 μs |   8.721 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 267.79 μs | 316.82 μs | 17.366 μs | 272.748 μs |      80 B |


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