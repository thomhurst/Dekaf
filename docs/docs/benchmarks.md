---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 23:45 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,261.76 μs** |   **107.29 μs** |   **5.881 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,368.27 μs | 2,301.12 μs | 126.132 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,412.86 μs** | **1,163.08 μs** |  **63.752 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,397.54 μs |   598.13 μs |  32.786 μs |  0.32 |    0.00 |  15.6250 |       - |   339.4 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,144.24 μs** |   **178.54 μs** |   **9.787 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,470.53 μs | 2,026.28 μs | 111.067 μs |  0.24 |    0.02 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,563.60 μs** | **1,075.08 μs** |  **58.929 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,118.57 μs | 7,171.10 μs | 393.072 μs |  0.57 |    0.03 |  15.6250 |       - |  361.73 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.78 μs** |    **53.27 μs** |   **2.920 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.52 μs |    48.11 μs |   2.637 μs |  0.45 |    0.02 |        - |       - |    8.15 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,358.56 μs** |   **394.34 μs** |  **21.615 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    635.87 μs |   819.22 μs |  44.904 μs |  0.47 |    0.03 |        - |       - |  106.17 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,117.05 μs** |   **235.32 μs** |  **12.899 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    210.95 μs |   156.99 μs |   8.605 μs |  0.19 |    0.01 |   0.9766 |       - |   98.39 KB |        0.80 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,291.64 μs** |   **763.26 μs** |  **41.837 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** | **1210.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,040.80 μs | 3,200.06 μs | 175.406 μs |  0.89 |    0.07 |   7.8125 |       - | 1043.73 KB |        0.86 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,456.42 μs** |   **251.98 μs** |  **13.812 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,315.97 μs |   237.69 μs |  13.029 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,476.54 μs** |   **567.26 μs** |  **31.094 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,275.84 μs |   645.48 μs |  35.381 μs |  0.23 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,521.97 μs** |    **34.39 μs** |   **1.885 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,125.41 μs |    75.62 μs |   4.145 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,457.71 μs** |    **64.38 μs** |   **3.529 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,123.63 μs |    78.26 μs |   4.290 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.711 ms** |  **41.231 ms** |  **2.2600 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.664 ms |  18.143 ms |  0.9945 ms | 0.005 |   598.7 KB |        8.02 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.249 ms** |  **12.383 ms** |  **0.6788 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.854 ms |  10.599 ms |  0.5809 ms | 0.004 |  794.48 KB |        3.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.296 ms** |  **17.134 ms** |  **0.9392 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.705 ms |  59.557 ms |  3.2645 ms | 0.006 | 1001.67 KB |        1.66 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.542 ms** |  **24.724 ms** |  **1.3552 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.269 ms |  25.949 ms |  1.4223 ms | 0.005 | 2769.05 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,140.709 ms** | **522.256 ms** | **28.6266 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.595 ms |   2.810 ms |  0.1540 ms | 0.002 |  193.73 KB |       80.51 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.543 ms** |  **35.830 ms** |  **1.9639 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.203 ms |  45.624 ms |  2.5008 ms | 0.002 |  186.65 KB |       44.82 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.339 ms** |  **15.008 ms** |  **0.8226 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.564 ms |  15.841 ms |  0.8683 ms | 0.002 |  193.98 KB |       80.61 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.144 ms** |  **14.346 ms** |  **0.7863 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.854 ms |  26.456 ms |  1.4501 ms | 0.002 |  187.48 KB |       44.85 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.138 μs | 201.880 μs | 11.0657 μs | 25.819 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.913 μs |   3.253 μs |  0.1783 μs | 12.853 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.343 μs |   4.807 μs |  0.2635 μs | 10.248 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 35.683 μs |  14.658 μs |  0.8035 μs | 35.566 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.849 μs |   2.290 μs |  0.1255 μs |  8.846 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.328 μs |   3.552 μs |  0.1947 μs | 20.258 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.500 μs |   6.365 μs |  0.3489 μs | 17.373 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 26.620 μs | 207.376 μs | 11.3670 μs | 20.107 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.287 μs |   4.494 μs |  0.2463 μs |  4.187 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.647 μs |   6.389 μs |  0.3502 μs | 10.460 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,730.3 ns | 2,200.6 ns | 120.62 ns |  0.43 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,248.3 ns |   851.9 ns |  46.69 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,352.0 ns | 2,072.2 ns | 113.58 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,428.3 ns |   842.6 ns |  46.19 ns |  0.60 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    834.3 ns |   640.7 ns |  35.12 ns |  0.21 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,804.8 ns | 5,474.1 ns | 300.06 ns | 10.04 |    0.08 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,064.3 ns |   416.2 ns |  22.81 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,850.5 ns | 2,077.5 ns | 113.87 ns |  0.95 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.24 μs |   4.791 μs |  0.263 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   517.12 μs | 397.497 μs | 21.788 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.03 μs |   1.464 μs |  0.080 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,699.42 μs | 221.874 μs | 12.162 μs |    1280 B |


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