---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 21:40 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,846.19 μs** |    **261.97 μs** |    **14.360 μs** | **5,838.64 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,099.51 μs |    109.38 μs |     5.996 μs | 1,097.61 μs |  0.19 |    0.00 |   1.9531 |       - |   34.76 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,977.13 μs** |    **751.54 μs** |    **41.195 μs** | **6,959.33 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,227.66 μs |    311.67 μs |    17.084 μs | 2,233.26 μs |  0.32 |    0.00 |  15.6250 |       - |  339.65 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,354.20 μs** |    **388.86 μs** |    **21.315 μs** | **6,358.88 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,160.20 μs |  1,539.98 μs |    84.411 μs | 1,132.10 μs |  0.18 |    0.01 |        - |       - |    36.5 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,750.18 μs** |  **2,366.66 μs** |   **129.725 μs** | **8,682.34 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,441.44 μs | 19,968.38 μs | 1,094.534 μs | 5,250.02 μs |  0.62 |    0.11 |  15.6250 |       - |  359.45 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **87.85 μs** |     **82.70 μs** |     **4.533 μs** |    **89.07 μs** |  **1.00** |    **0.06** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    69.17 μs |    574.33 μs |    31.481 μs |    53.66 μs |  0.79 |    0.31 |        - |       - |   16.67 KB |        0.50 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **960.95 μs** |    **323.57 μs** |    **17.736 μs** |   **970.51 μs** |  **1.00** |    **0.02** |  **20.5078** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   541.81 μs |    396.24 μs |    21.719 μs |   530.33 μs |  0.56 |    0.02 |        - |       - |   61.19 KB |        0.18 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **893.03 μs** |  **2,005.68 μs** |   **109.938 μs** |   **954.53 μs** |  **1.01** |    **0.16** |   **7.3242** |       **-** |  **122.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   199.62 μs |    591.63 μs |    32.429 μs |   189.88 μs |  0.23 |    0.04 |   0.9766 |       - |   99.91 KB |        0.82 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,409.00 μs** |  **2,265.10 μs** |   **124.158 μs** | **7,382.15 μs** |  **1.00** |    **0.02** |  **74.2188** |       **-** | **1225.65 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,908.29 μs |  3,770.82 μs |   206.692 μs | 1,820.40 μs |  0.26 |    0.02 |   7.8125 |       - | 1015.94 KB |        0.83 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,593.13 μs** |  **7,013.77 μs** |   **384.448 μs** | **5,419.53 μs** |  **1.00** |    **0.08** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,253.20 μs |    348.02 μs |    19.076 μs | 1,261.05 μs |  0.22 |    0.01 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,328.47 μs** |    **103.02 μs** |     **5.647 μs** | **5,327.02 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,277.50 μs |    290.13 μs |    15.903 μs | 1,272.00 μs |  0.24 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,330.05 μs** |     **30.63 μs** |     **1.679 μs** | **5,329.82 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,419.97 μs |  3,833.75 μs |   210.141 μs | 1,323.20 μs |  0.27 |    0.03 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,329.47 μs** |     **33.56 μs** |     **1.839 μs** | **5,329.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,319.92 μs |    257.87 μs |    14.135 μs | 1,327.07 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166,226.16 μs** | **42,067.98 μs** | **2,305.888 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    12,161.78 μs | 10,924.77 μs |   598.824 μs | 0.004 |    0.00 |  617936 B |        8.09 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,163,224.84 μs** | **12,023.10 μs** |   **659.026 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    11,610.23 μs | 47,104.40 μs | 2,581.951 μs | 0.004 |    0.00 |  806208 B |        3.14 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,163,838.39 μs** | **13,685.31 μs** |   **750.138 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    12,903.24 μs | 14,347.36 μs |   786.427 μs | 0.004 |    0.00 | 1035888 B |        1.68 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163,973.81 μs** |  **9,682.98 μs** |   **530.757 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14,505.67 μs | 37,366.39 μs | 2,048.178 μs | 0.005 |    0.00 | 2840536 B |        1.17 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **25.06 μs** |     **93.63 μs** |     **5.132 μs** |  **1.03** |    **0.25** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        21.03 μs |     66.86 μs |     3.665 μs |  0.86 |    0.19 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **23.18 μs** |     **64.78 μs** |     **3.551 μs** |  **1.01** |    **0.18** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        21.47 μs |     56.92 μs |     3.120 μs |  0.94 |    0.17 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **22.95 μs** |     **38.46 μs** |     **2.108 μs** |  **1.01** |    **0.12** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        19.96 μs |     89.87 μs |     4.926 μs |  0.87 |    0.20 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **37.83 μs** |    **228.14 μs** |    **12.505 μs** |  **1.10** |    **0.50** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        22.30 μs |     47.70 μs |     2.615 μs |  0.65 |    0.24 |    2840 B |        1.06 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 28.619 μs |   5.7749 μs |  0.3165 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.762 μs |   4.9766 μs |  0.2728 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.992 μs |  10.2831 μs |  0.5637 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 10.052 μs |   7.0755 μs |  0.3878 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.749 μs |   8.1499 μs |  0.4467 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 15.119 μs |   5.6299 μs |  0.3086 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.549 μs |   4.6201 μs |  0.2532 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 29.425 μs |   3.5876 μs |  0.1966 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.570 μs |   2.6792 μs |  0.1469 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.080 μs |   0.6407 μs |  0.0351 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 43.556 μs | 244.3755 μs | 13.3950 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.057 μs |  40.3384 μs |  2.2111 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.357 μs |   2.9030 μs |  0.1591 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.786 μs |  10.6311 μs |  0.5827 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,433.0 ns |    795.2 ns |  43.59 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,426.3 ns |    557.4 ns |  30.55 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,581.5 ns |  1,596.4 ns |  87.50 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,821.7 ns |  2,708.0 ns | 148.44 ns |  0.63 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    814.3 ns |    105.3 ns |   5.77 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 48,104.3 ns | 15,999.8 ns | 877.00 ns | 10.80 |    0.35 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,455.5 ns |  2,749.1 ns | 150.69 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,160.7 ns |  2,982.2 ns | 163.46 ns |  0.93 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.553 μs |   5.491 μs |  0.3010 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 630.420 μs | 251.769 μs | 13.8003 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.564 μs |   4.815 μs |  0.2639 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 261.381 μs |  12.336 μs |  0.6762 μs |      80 B |


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