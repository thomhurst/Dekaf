---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-07 15:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,170.46 μs** |    **922.28 μs** |    **50.553 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,605.08 μs |  2,343.00 μs |   128.428 μs |  0.26 |    0.02 |        - |       - |   34.77 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,386.31 μs** |  **1,068.23 μs** |    **58.554 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,358.34 μs |  1,015.45 μs |    55.660 μs |  0.32 |    0.01 |  15.6250 |       - |  339.63 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,212.02 μs** |  **1,178.69 μs** |    **64.608 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,700.46 μs |  2,000.26 μs |   109.641 μs |  0.27 |    0.02 |        - |       - |   36.62 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,093.48 μs** |    **485.08 μs** |    **26.589 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1938.06 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,069.25 μs |  5,114.56 μs |   280.346 μs |  0.54 |    0.02 |  15.6250 |       - |  359.59 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.14 μs** |     **34.88 μs** |     **1.912 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     61.89 μs |    118.85 μs |     6.514 μs |  0.45 |    0.04 |   0.2441 |       - |    8.16 KB |        0.24 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,397.49 μs** |    **210.64 μs** |    **11.546 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    713.42 μs |    528.79 μs |    28.985 μs |  0.51 |    0.02 |        - |       - |  185.61 KB |        0.55 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **613.70 μs** |  **7,943.50 μs** |   **435.410 μs** |  **1.46** |    **1.39** |   **7.3242** |       **-** |  **122.54 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    254.28 μs |    370.47 μs |    20.307 μs |  0.60 |    0.38 |   0.9766 |       - |   98.13 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,198.97 μs** | **25,325.00 μs** | **1,388.149 μs** |  **1.01** |    **0.18** |  **74.2188** |       **-** |  **1226.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,920.09 μs | 13,990.66 μs |   766.875 μs |  0.29 |    0.08 |        - |       - |  983.99 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,474.09 μs** |    **832.63 μs** |    **45.639 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,320.12 μs |    102.85 μs |     5.638 μs |  0.24 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,463.60 μs** |    **389.63 μs** |    **21.357 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,183.35 μs |    371.64 μs |    20.371 μs |  0.22 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,489.93 μs** |    **185.90 μs** |    **10.190 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,118.33 μs |     68.90 μs |     3.777 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,478.29 μs** |     **74.21 μs** |     **4.067 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.87 μs |     98.41 μs |     5.394 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169,425.96 μs** |  **30,173.35 μs** | **1,653.903 μs** | **3,169,989.92 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17,340.81 μs |  30,456.95 μs | 1,669.448 μs |    17,447.63 μs | 0.005 |    0.00 |  630536 B |        8.25 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167,037.33 μs** |  **25,073.85 μs** | **1,374.382 μs** | **3,167,565.83 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,593.93 μs |   3,953.54 μs |   216.707 μs |    15,626.92 μs | 0.005 |    0.00 |  807424 B |        3.15 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167,172.33 μs** |  **16,738.70 μs** |   **917.504 μs** | **3,166,695.34 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19,264.45 μs | 123,380.46 μs | 6,762.900 μs |    15,677.28 μs | 0.006 |    0.00 | 1030000 B |        1.67 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167,163.23 μs** |  **16,536.45 μs** |   **906.418 μs** | **3,167,446.68 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15,394.18 μs |  26,823.45 μs | 1,470.284 μs |    15,191.03 μs | 0.005 |    0.00 | 2841312 B |        1.17 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **38.16 μs** |      **22.48 μs** |     **1.232 μs** |        **38.56 μs** |  **1.00** |    **0.04** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        37.06 μs |     390.81 μs |    21.422 μs |        25.48 μs |  0.97 |    0.49 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **39.40 μs** |      **60.18 μs** |     **3.299 μs** |        **39.66 μs** |  **1.00** |    **0.10** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        29.61 μs |     110.47 μs |     6.055 μs |        31.68 μs |  0.76 |    0.15 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **38.67 μs** |      **42.91 μs** |     **2.352 μs** |        **39.50 μs** |  **1.00** |    **0.08** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        24.72 μs |      46.77 μs |     2.563 μs |        25.79 μs |  0.64 |    0.07 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **46.97 μs** |      **91.16 μs** |     **4.997 μs** |        **48.41 μs** |  **1.01** |    **0.13** |    **2736 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        28.96 μs |      20.52 μs |     1.125 μs |        28.53 μs |  0.62 |    0.06 |    2440 B |        0.89 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 23.874 μs | 10.142 μs | 0.5559 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 14.344 μs | 10.417 μs | 0.5710 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 10.191 μs | 19.374 μs | 1.0620 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.089 μs |  8.039 μs | 0.4407 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.982 μs |  8.890 μs | 0.4873 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 14.415 μs | 37.271 μs | 2.0429 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.583 μs | 36.683 μs | 2.0107 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 29.767 μs | 40.244 μs | 2.2059 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.267 μs | 24.357 μs | 1.3351 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.111 μs | 99.030 μs | 5.4282 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 43.236 μs | 40.100 μs | 2.1980 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 41.648 μs | 45.287 μs | 2.4823 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.400 μs | 12.800 μs | 0.7016 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  9.938 μs | 18.067 μs | 0.9903 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,530.0 ns |  10,569.9 ns |    579.4 ns |  0.32 |    0.12 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,492.8 ns |   4,674.2 ns |    256.2 ns |  0.32 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,690.0 ns |   4,726.1 ns |    259.1 ns |  0.36 |    0.08 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,470.8 ns |   4,323.8 ns |    237.0 ns |  0.74 |    0.14 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    903.2 ns |   9,004.0 ns |    493.5 ns |  0.19 |    0.10 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,436.0 ns | 185,238.4 ns | 10,153.5 ns |  8.79 |    2.46 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,847.5 ns |  17,889.1 ns |    980.6 ns |  1.03 |    0.26 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,804.8 ns |  18,756.5 ns |  1,028.1 ns |  0.81 |    0.24 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.671 μs |  19.370 μs |  1.062 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 454.091 μs | 112.881 μs |  6.187 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.619 μs |  19.231 μs |  1.054 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 201.503 μs | 232.936 μs | 12.768 μs |      80 B |


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