---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 07:51 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,085.2 μs** |    **500.51 μs** |    **27.43 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,565.7 μs |  4,879.27 μs |   267.45 μs |  0.26 |    0.04 |        - |       - |   34.74 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,383.3 μs** |    **814.50 μs** |    **44.65 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,495.9 μs |    498.14 μs |    27.30 μs |  0.34 |    0.00 |  15.6250 |       - |  341.56 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,165.7 μs** |    **164.05 μs** |     **8.99 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,780.7 μs |  1,665.26 μs |    91.28 μs |  0.29 |    0.01 |        - |       - |   38.34 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,574.1 μs** |  **4,372.02 μs** |   **239.65 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,761.5 μs |  2,615.31 μs |   143.35 μs |  0.62 |    0.01 |  15.6250 |       - |  390.58 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.7 μs** |     **18.49 μs** |     **1.01 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    104.3 μs |    285.46 μs |    15.65 μs |  0.77 |    0.10 |        - |       - |    6.22 KB |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,315.0 μs** |    **321.94 μs** |    **17.65 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    974.6 μs |  3,255.96 μs |   178.47 μs |  0.74 |    0.12 |        - |       - |   84.34 KB |        0.25 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **606.7 μs** |  **8,012.97 μs** |   **439.22 μs** |  **1.45** |    **1.38** |   **7.3242** |       **-** |  **122.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    348.7 μs |    514.21 μs |    28.19 μs |  0.84 |    0.52 |        - |       - |   78.95 KB |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,668.7 μs** | **45,926.84 μs** | **2,517.41 μs** |  **1.04** |    **0.32** |  **74.2188** |       **-** | **1226.61 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,820.7 μs |  6,083.92 μs |   333.48 μs |  0.37 |    0.09 |        - |       - |  651.01 KB |        0.53 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,422.2 μs** |     **48.57 μs** |     **2.66 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,117.4 μs |     43.71 μs |     2.40 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,426.7 μs** |     **29.75 μs** |     **1.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,118.3 μs |     23.84 μs |     1.31 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,427.2 μs** |     **45.80 μs** |     **2.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,118.2 μs |     32.16 μs |     1.76 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,427.9 μs** |     **69.11 μs** |     **3.79 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,423.3 μs |    144.09 μs |     7.90 μs |  0.26 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,165,156.28 μs** | **17,454.50 μs** |   **956.740 μs** | **3,165,406.85 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16,590.50 μs | 29,514.19 μs | 1,617.772 μs |    16,796.25 μs | 0.005 |    0.00 |  637288 B |        8.34 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164,365.04 μs** |  **8,713.04 μs** |   **477.591 μs** | **3,164,181.48 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13,848.63 μs | 26,973.90 μs | 1,478.530 μs |    13,377.17 μs | 0.004 |    0.00 |  821832 B |        3.21 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,162,871.51 μs** | **18,147.96 μs** |   **994.751 μs** | **3,163,211.69 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17,615.18 μs | 52,875.15 μs | 2,898.265 μs |    16,311.47 μs | 0.006 |    0.00 | 1056344 B |        1.71 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163,870.54 μs** |  **8,873.97 μs** |   **486.412 μs** | **3,163,622.96 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16,619.51 μs | 32,493.71 μs | 1,781.090 μs |    17,091.84 μs | 0.005 |    0.00 | 2944168 B |        1.21 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **24.78 μs** |     **25.08 μs** |     **1.375 μs** |        **25.53 μs** |  **1.00** |    **0.07** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        39.09 μs |    328.30 μs |    17.995 μs |        30.22 μs |  1.58 |    0.64 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **24.67 μs** |     **14.66 μs** |     **0.804 μs** |        **24.74 μs** |  **1.00** |    **0.04** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        37.62 μs |    300.33 μs |    16.462 μs |        30.66 μs |  1.53 |    0.58 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **23.31 μs** |     **56.06 μs** |     **3.073 μs** |        **24.43 μs** |  **1.01** |    **0.17** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        29.39 μs |    119.74 μs |     6.563 μs |        26.01 μs |  1.28 |    0.29 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **27.90 μs** |     **70.92 μs** |     **3.887 μs** |        **28.68 μs** |  **1.01** |    **0.18** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        29.54 μs |     13.07 μs |     0.717 μs |        29.55 μs |  1.07 |    0.14 |    2344 B |        0.88 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 40.294 μs | 127.455 μs | 6.9862 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.369 μs |  14.850 μs | 0.8140 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.010 μs |  16.249 μs | 0.8907 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.786 μs |  18.385 μs | 1.0077 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.020 μs |  19.842 μs | 1.0876 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.249 μs |  17.601 μs | 0.9648 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 11.414 μs |  19.952 μs | 1.0936 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 36.276 μs | 106.788 μs | 5.8534 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.642 μs |  10.629 μs | 0.5826 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 14.344 μs |   6.568 μs | 0.3600 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 42.492 μs |  77.776 μs | 4.2631 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 50.785 μs | 161.771 μs | 8.8672 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  7.954 μs |  31.836 μs | 1.7450 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.252 μs |  38.318 μs | 2.1003 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  2,512.0 ns | 28,072.0 ns | 1,538.7 ns |  2,596.0 ns |  0.77 |    0.49 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,490.0 ns | 19,545.2 ns | 1,071.3 ns |  1,084.0 ns |  0.46 |    0.33 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  3,132.3 ns | 12,378.1 ns |   678.5 ns |  2,884.0 ns |  0.96 |    0.35 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,124.0 ns | 17,788.6 ns |   975.1 ns |  1,644.0 ns |  0.65 |    0.34 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    983.0 ns |  6,862.5 ns |   376.2 ns |  1,017.0 ns |  0.30 |    0.14 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 47,164.5 ns | 20,086.8 ns | 1,101.0 ns | 47,454.5 ns | 14.43 |    4.50 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,633.3 ns | 28,102.5 ns | 1,540.4 ns |  3,025.0 ns |  1.11 |    0.55 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  5,962.2 ns | 28,498.5 ns | 1,562.1 ns |  5,513.5 ns |  1.82 |    0.71 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean      | Error     | StdDev    | Allocated |
|------------------------ |----------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.62 μs |  74.97 μs |  4.110 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 451.62 μs | 358.95 μs | 19.675 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.93 μs |  47.65 μs |  2.612 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 193.65 μs | 155.15 μs |  8.504 μs |      80 B |


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