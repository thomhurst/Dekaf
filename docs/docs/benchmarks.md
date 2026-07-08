---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 19:52 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,015.74 μs** |    **503.41 μs** |    **27.594 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,500.56 μs |  3,029.44 μs |   166.054 μs |  0.25 |    0.02 |        - |       - |   34.73 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,178.89 μs** |    **746.87 μs** |    **40.939 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,313.31 μs |    268.51 μs |    14.718 μs |  0.32 |    0.00 |  15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,657.74 μs** |    **182.62 μs** |    **10.010 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,379.28 μs |  2,888.37 μs |   158.321 μs |  0.21 |    0.02 |        - |       - |   36.46 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,186.18 μs** |  **2,879.62 μs** |   **157.842 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,160.15 μs |  4,737.83 μs |   259.697 μs |  0.55 |    0.02 |  15.6250 |       - |  358.68 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **123.02 μs** |     **11.54 μs** |     **0.633 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     75.24 μs |     52.13 μs |     2.858 μs |  0.61 |    0.02 |        - |       - |    5.76 KB |        0.17 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,175.20 μs** |    **915.29 μs** |    **50.170 μs** |  **1.00** |    **0.05** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    675.24 μs |    929.72 μs |    50.961 μs |  0.58 |    0.04 |        - |       - |  104.27 KB |        0.31 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **984.29 μs** |    **224.67 μs** |    **12.315 μs** |  **1.00** |    **0.02** |   **7.3242** |       **-** |  **122.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    237.23 μs |    461.15 μs |    25.277 μs |  0.24 |    0.02 |   0.9766 |       - |   97.05 KB |        0.79 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,744.01 μs** | **31,238.95 μs** | **1,712.312 μs** |  **1.03** |    **0.27** |  **74.2188** |       **-** | **1224.59 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,105.30 μs |  1,496.68 μs |    82.038 μs |  0.37 |    0.07 |        - |       - |  962.05 KB |        0.79 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,443.23 μs** |    **395.20 μs** |    **21.662 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,337.22 μs |    193.66 μs |    10.615 μs |  0.25 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,417.12 μs** |    **103.20 μs** |     **5.657 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,327.35 μs |    513.92 μs |    28.169 μs |  0.25 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,436.15 μs** |    **734.20 μs** |    **40.244 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,303.10 μs |    299.30 μs |    16.406 μs |  0.24 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,403.44 μs** |     **76.93 μs** |     **4.217 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,269.22 μs |    344.43 μs |    18.880 μs |  0.23 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166,854.53 μs** | **16,368.73 μs** |   **897.226 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,371.51 μs | 36,934.14 μs | 2,024.485 μs | 0.005 |    0.00 |  619208 B |        8.10 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,163,679.27 μs** | **26,418.08 μs** | **1,448.064 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,342.45 μs | 49,578.83 μs | 2,717.583 μs | 0.005 |    0.00 |  825320 B |        3.22 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,613.58 μs** | **14,757.72 μs** |   **808.920 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16,707.72 μs | 37,349.21 μs | 2,047.236 μs | 0.005 |    0.00 | 1104600 B |        1.79 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166,636.37 μs** | **16,755.94 μs** |   **918.450 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15,642.02 μs | 23,347.29 μs | 1,279.744 μs | 0.005 |    0.00 | 2847128 B |        1.17 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **30.46 μs** |     **83.42 μs** |     **4.573 μs** |  **1.01** |    **0.18** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        28.62 μs |     75.92 μs |     4.162 μs |  0.95 |    0.17 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **35.65 μs** |    **117.35 μs** |     **6.432 μs** |  **1.02** |    **0.24** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        28.60 μs |    138.18 μs |     7.574 μs |  0.82 |    0.24 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **35.82 μs** |     **70.31 μs** |     **3.854 μs** |  **1.01** |    **0.13** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        27.55 μs |     91.84 μs |     5.034 μs |  0.78 |    0.14 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **39.24 μs** |    **118.10 μs** |     **6.473 μs** |  **1.02** |    **0.21** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        27.65 μs |     55.33 μs |     3.033 μs |  0.72 |    0.13 |    2312 B |        0.87 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.058 μs | 269.985 μs | 14.7988 μs | 24.625 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.809 μs |   8.276 μs |  0.4536 μs | 10.646 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 12.670 μs |  11.012 μs |  0.6036 μs | 12.628 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.805 μs |  31.174 μs |  1.7088 μs |  8.683 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.634 μs |  31.438 μs |  1.7232 μs | 12.473 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 17.604 μs |  63.770 μs |  3.4955 μs | 19.535 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 17.470 μs |  69.122 μs |  3.7888 μs | 19.578 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.163 μs |   6.945 μs |  0.3807 μs | 28.012 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 17.311 μs |  90.667 μs |  4.9697 μs | 20.136 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 34.084 μs | 174.800 μs |  9.5814 μs | 30.980 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 49.646 μs | 118.040 μs |  6.4702 μs | 50.575 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 42.773 μs |  42.316 μs |  2.3195 μs | 41.608 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.637 μs |  18.207 μs |  0.9980 μs |  5.156 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.195 μs |  12.621 μs |  0.6918 μs | 10.434 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,464.7 ns |   5,485.5 ns |    300.7 ns |  1,515.0 ns |  0.26 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,637.0 ns |   5,587.5 ns |    306.3 ns |  1,762.0 ns |  0.29 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,634.7 ns |   3,193.5 ns |    175.0 ns |  1,630.0 ns |  0.29 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,450.7 ns |   7,962.3 ns |    436.4 ns |  2,358.0 ns |  0.44 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    749.0 ns |   5,159.1 ns |    282.8 ns |    629.0 ns |  0.13 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 55,268.5 ns | 383,597.6 ns | 21,026.3 ns | 43,325.5 ns |  9.81 |    3.68 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,803.7 ns |  23,458.0 ns |  1,285.8 ns |  5,226.0 ns |  1.03 |    0.27 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,949.3 ns |  15,219.0 ns |    834.2 ns |  3,792.0 ns |  0.70 |    0.18 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.853 μs |  19.942 μs |  1.0931 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 496.275 μs | 202.177 μs | 11.0820 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.853 μs |  16.491 μs |  0.9039 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 203.078 μs | 211.731 μs | 11.6057 μs |      80 B |


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