---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 16:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,150.20 μs** |   **333.14 μs** |  **18.260 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,552.19 μs | 4,315.73 μs | 236.559 μs |  0.25 |    0.03 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,576.22 μs** | **2,451.00 μs** | **134.348 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,388.11 μs |   373.42 μs |  20.469 μs |  0.32 |    0.01 |  15.6250 |       - |  339.96 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,224.70 μs** |   **361.65 μs** |  **19.823 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,858.95 μs |   866.38 μs |  47.489 μs |  0.30 |    0.01 |        - |       - |    36.6 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,485.01 μs** | **1,428.55 μs** |  **78.304 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,681.94 μs | 1,457.95 μs |  79.915 μs |  0.54 |    0.01 |  15.6250 |       - |  359.34 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.18 μs** |    **94.58 μs** |   **5.184 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     77.31 μs |   199.89 μs |  10.957 μs |  0.58 |    0.07 |        - |       - |    7.76 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,383.65 μs** |   **398.90 μs** |  **21.865 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    831.04 μs |   811.15 μs |  44.462 μs |  0.60 |    0.03 |        - |       - |  139.51 KB |        0.42 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,115.05 μs** |    **49.05 μs** |   **2.689 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    254.31 μs |    56.63 μs |   3.104 μs |  0.23 |    0.00 |        - |       - |   90.93 KB |        0.74 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,305.65 μs** | **1,671.02 μs** |  **91.594 μs** |  **1.00** |    **0.05** |  **70.3125** |       **-** | **1210.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,984.98 μs | 5,675.81 μs | 311.110 μs |  1.73 |    0.13 |        - |       - |  799.96 KB |        0.66 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,503.32 μs** | **1,120.81 μs** |  **61.436 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,119.10 μs |    34.78 μs |   1.906 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,435.83 μs** |    **55.91 μs** |   **3.064 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,390.10 μs |   392.16 μs |  21.495 μs |  0.26 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,487.39 μs** |    **95.42 μs** |   **5.230 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,413.61 μs |   873.07 μs |  47.856 μs |  0.26 |    0.01 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,479.40 μs** |   **677.16 μs** |  **37.118 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,374.03 μs |   469.11 μs |  25.714 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171,184.55 μs** |  **21,584.82 μs** | **1,183.137 μs** | **3,171,756.34 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16,792.48 μs |  23,897.99 μs | 1,309.930 μs |    16,174.06 μs | 0.005 |    0.00 |  629312 B |        8.24 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,665.65 μs** |   **9,037.22 μs** |   **495.360 μs** | **3,165,739.84 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,298.37 μs |  37,260.41 μs | 2,042.369 μs |    14,813.06 μs | 0.005 |    0.00 |  805768 B |        3.14 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,803.03 μs** |  **15,905.06 μs** |   **871.810 μs** | **3,165,777.55 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19,542.95 μs | 111,489.65 μs | 6,111.124 μs |    21,201.90 μs | 0.006 |    0.00 | 1030008 B |        1.67 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166,949.32 μs** |  **13,195.14 μs** |   **723.270 μs** | **3,166,713.14 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17,284.59 μs |  38,542.90 μs | 2,112.667 μs |    18,221.47 μs | 0.005 |    0.00 | 2841064 B |        1.17 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **31.10 μs** |      **83.40 μs** |     **4.571 μs** |        **29.59 μs** |  **1.01** |    **0.18** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        26.62 μs |      80.58 μs |     4.417 μs |        24.72 μs |  0.87 |    0.16 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **29.33 μs** |      **75.64 μs** |     **4.146 μs** |        **28.77 μs** |  **1.01** |    **0.17** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        40.32 μs |     260.52 μs |    14.280 μs |        33.92 μs |  1.39 |    0.46 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **38.65 μs** |      **86.26 μs** |     **4.728 μs** |        **38.15 μs** |  **1.01** |    **0.15** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        32.45 μs |     302.64 μs |    16.589 μs |        24.05 μs |  0.85 |    0.39 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **51.77 μs** |     **374.75 μs** |    **20.541 μs** |        **47.95 μs** |  **1.11** |    **0.54** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        31.97 μs |     239.41 μs |    13.123 μs |        25.30 μs |  0.69 |    0.34 |    2312 B |        0.87 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.179 μs |  6.0392 μs | 0.3310 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.650 μs |  1.4833 μs | 0.0813 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.694 μs | 39.0709 μs | 2.1416 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.440 μs |  0.4688 μs | 0.0257 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.385 μs |  4.4197 μs | 0.2423 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.634 μs |  1.9342 μs | 0.1060 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.580 μs | 11.2017 μs | 0.6140 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.305 μs | 95.2193 μs | 5.2193 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.020 μs |  1.0997 μs | 0.0603 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.420 μs |  1.1870 μs | 0.0651 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.576 μs | 16.2783 μs | 0.8923 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.992 μs | 28.4240 μs | 1.5580 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.548 μs |  2.2859 μs | 0.1253 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.598 μs | 18.1519 μs | 0.9950 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.669 μs |  3.454 μs | 0.1893 μs |  0.37 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.306 μs |  1.037 μs | 0.0569 μs |  0.29 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.375 μs |  2.130 μs | 0.1168 μs |  0.31 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.575 μs |  2.465 μs | 0.1351 μs |  0.58 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.022 μs |  5.354 μs | 0.2935 μs |  0.23 |    0.06 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40.078 μs |  5.560 μs | 0.3047 μs |  8.97 |    0.67 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.492 μs |  7.266 μs | 0.3983 μs |  1.01 |    0.11 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.479 μs | 16.000 μs | 0.8770 μs |  1.00 |    0.19 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error     | StdDev    | Allocated |
|------------------------ |-----------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.184 μs |  7.036 μs | 0.3856 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 521.050 μs | 64.153 μs | 3.5165 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.742 μs |  1.559 μs | 0.0854 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.113 μs |  6.205 μs | 0.3401 μs |      80 B |


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