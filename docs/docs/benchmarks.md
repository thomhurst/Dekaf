---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 16:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,032.79 μs** |    **183.44 μs** |    **10.055 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,376.60 μs |  2,425.22 μs |   132.934 μs |  0.23 |    0.02 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,375.76 μs** |    **714.34 μs** |    **39.155 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,347.07 μs |  1,094.50 μs |    59.993 μs |  0.32 |    0.01 |  15.6250 |       - |   339.7 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,600.46 μs** |    **486.94 μs** |    **26.691 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,674.64 μs |  4,613.46 μs |   252.879 μs |  0.25 |    0.03 |        - |       - |   36.56 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,813.15 μs** |  **1,096.14 μs** |    **60.083 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,098.02 μs |  6,927.78 μs |   379.735 μs |  0.60 |    0.03 |  15.6250 |       - |  359.82 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.76 μs** |     **43.35 μs** |     **2.376 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     80.70 μs |     59.76 μs |     3.276 μs |  0.60 |    0.02 |        - |       - |    7.04 KB |        0.21 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,313.12 μs** |    **525.45 μs** |    **28.802 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    686.03 μs |  1,166.78 μs |    63.955 μs |  0.52 |    0.04 |        - |       - |   69.35 KB |        0.21 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,026.51 μs** |    **254.22 μs** |    **13.935 μs** |  **1.00** |    **0.02** |   **7.3242** |       **-** |  **122.48 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    518.19 μs |    432.24 μs |    23.692 μs |  0.50 |    0.02 |        - |       - |   58.95 KB |        0.48 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,137.18 μs** | **34,196.87 μs** | **1,874.446 μs** |  **1.03** |    **0.28** |  **74.2188** |       **-** | **1225.19 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,104.31 μs |  5,671.86 μs |   310.894 μs |  0.35 |    0.08 |        - |       - |  953.59 KB |        0.78 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,394.54 μs** |     **53.64 μs** |     **2.940 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,387.73 μs |    343.28 μs |    18.816 μs |  0.26 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,737.68 μs** |  **6,069.70 μs** |   **332.701 μs** |  **1.00** |    **0.07** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,373.73 μs |    192.27 μs |    10.539 μs |  0.24 |    0.01 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,400.66 μs** |     **84.44 μs** |     **4.628 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,366.31 μs |    303.48 μs |    16.635 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,399.35 μs** |     **14.53 μs** |     **0.796 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,311.24 μs |  1,843.30 μs |   101.037 μs |  0.24 |    0.02 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168,060.71 μs** |  **9,510.03 μs** |   **521.277 μs** | **3,167,943.23 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,768.66 μs | 36,900.96 μs | 2,022.666 μs |    16,159.58 μs | 0.005 |    0.00 |  626760 B |        8.20 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164,584.46 μs** | **15,418.32 μs** |   **845.130 μs** | **3,164,767.44 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16,040.64 μs | 54,761.43 μs | 3,001.659 μs |    17,174.34 μs | 0.005 |    0.00 |  807664 B |        3.15 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,084.67 μs** |  **5,632.32 μs** |   **308.727 μs** | **3,166,130.11 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15,853.41 μs | 13,389.05 μs |   733.899 μs |    15,793.49 μs | 0.005 |    0.00 | 1030648 B |        1.67 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,743.60 μs** |  **6,385.07 μs** |   **349.987 μs** | **3,164,642.39 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15,897.30 μs | 37,273.53 μs | 2,043.088 μs |    15,342.38 μs | 0.005 |    0.00 | 2841008 B |        1.17 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **27.37 μs** |     **45.41 μs** |     **2.489 μs** |        **26.08 μs** |  **1.01** |    **0.11** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        26.53 μs |     98.11 μs |     5.378 μs |        25.04 μs |  0.97 |    0.19 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **28.82 μs** |     **47.41 μs** |     **2.599 μs** |        **27.75 μs** |  **1.01** |    **0.11** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        36.17 μs |    392.08 μs |    21.491 μs |        25.11 μs |  1.26 |    0.66 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **27.07 μs** |     **68.23 μs** |     **3.740 μs** |        **26.49 μs** |  **1.01** |    **0.17** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        26.56 μs |     67.93 μs |     3.724 μs |        26.05 μs |  0.99 |    0.17 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **31.42 μs** |     **49.96 μs** |     **2.739 μs** |        **32.63 μs** |  **1.01** |    **0.11** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        25.65 μs |     44.78 μs |     2.455 μs |        25.69 μs |  0.82 |    0.09 |    2312 B |        0.87 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.873 μs |   0.5320 μs |  0.0292 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.501 μs |   6.0038 μs |  0.3291 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.832 μs |   5.0288 μs |  0.2756 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.625 μs |   1.6554 μs |  0.0907 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.884 μs |   1.3774 μs |  0.0755 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.792 μs |  50.2176 μs |  2.7526 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 11.584 μs |   4.2960 μs |  0.2355 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.707 μs |   5.7768 μs |  0.3166 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.376 μs |   8.8646 μs |  0.4859 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.123 μs |   8.0177 μs |  0.4395 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 29.781 μs |  55.7706 μs |  3.0570 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 43.197 μs | 231.3091 μs | 12.6788 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.813 μs |  10.3579 μs |  0.5678 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.966 μs |   3.9615 μs |  0.2171 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,017.8 ns |  6,132.2 ns | 336.1 ns |  0.24 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,239.0 ns |  2,942.7 ns | 161.3 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,282.0 ns |  3,883.0 ns | 212.8 ns |  0.30 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,570.3 ns |  4,091.2 ns | 224.3 ns |  0.61 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    868.3 ns |  2,472.7 ns | 135.5 ns |  0.20 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 34,957.7 ns | 13,346.6 ns | 731.6 ns |  8.24 |    0.36 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,247.7 ns |  3,585.9 ns | 196.6 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,817.3 ns |  9,754.2 ns | 534.7 ns |  0.90 |    0.11 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.128 μs |  33.330 μs |  1.8269 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 524.120 μs | 335.189 μs | 18.3728 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.011 μs |   9.219 μs |  0.5053 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.339 μs | 211.835 μs | 11.6114 μs |      80 B |


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