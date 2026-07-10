---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-10 17:30 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,142.20 μs** |    **363.73 μs** |    **19.938 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,530.09 μs |  2,046.85 μs |   112.195 μs |  0.25 |    0.02 |        - |       - |   34.74 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,462.00 μs** |    **612.11 μs** |    **33.552 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,470.66 μs |  1,072.87 μs |    58.808 μs |  0.33 |    0.01 |  15.6250 |       - |  341.62 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,115.95 μs** |    **514.93 μs** |    **28.225 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,766.05 μs |    729.30 μs |    39.975 μs |  0.29 |    0.01 |        - |       - |   38.45 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,741.31 μs** |  **2,165.59 μs** |   **118.703 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,568.05 μs | 10,827.81 μs |   593.509 μs |  0.59 |    0.04 |  15.6250 |       - |  392.63 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.83 μs** |    **273.24 μs** |    **14.977 μs** |  **1.01** |    **0.12** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     92.41 μs |    326.79 μs |    17.913 μs |  0.64 |    0.12 |        - |       - |    6.87 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,324.22 μs** |  **1,210.65 μs** |    **66.360 μs** |  **1.00** |    **0.06** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,218.77 μs |  5,664.26 μs |   310.477 μs |  0.92 |    0.21 |        - |       - |   83.07 KB |        0.25 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,112.39 μs** |     **19.81 μs** |     **1.086 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.59 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    345.90 μs |    128.40 μs |     7.038 μs |  0.31 |    0.01 |        - |       - |   81.63 KB |        0.67 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,071.62 μs** | **35,463.66 μs** | **1,943.883 μs** |  **1.03** |    **0.26** |  **74.2188** |       **-** | **1226.42 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  4,079.38 μs |  7,597.21 μs |   416.429 μs |  0.42 |    0.09 |        - |       - |  705.97 KB |        0.58 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,431.75 μs** |     **45.60 μs** |     **2.500 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,131.06 μs |    401.67 μs |    22.017 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,428.93 μs** |     **67.44 μs** |     **3.697 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,126.61 μs |    249.72 μs |    13.688 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,437.37 μs** |    **137.94 μs** |     **7.561 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,117.82 μs |     47.45 μs |     2.601 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,430.83 μs** |     **57.74 μs** |     **3.165 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,119.81 μs |    107.41 μs |     5.888 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168,259.36 μs** | **18,444.36 μs** | **1,010.998 μs** | **3,168,284.40 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17,951.70 μs | 32,225.19 μs | 1,766.371 μs |    17,820.00 μs | 0.006 |    0.00 |  623416 B |        8.16 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166,110.52 μs** | **15,561.73 μs** |   **852.991 μs** | **3,165,773.97 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,787.50 μs | 22,571.28 μs | 1,237.208 μs |    15,080.93 μs | 0.005 |    0.00 |  809448 B |        3.16 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,915.04 μs** | **15,583.12 μs** |   **854.163 μs** | **3,165,935.74 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16,288.68 μs | 37,843.56 μs | 2,074.333 μs |    16,887.88 μs | 0.005 |    0.00 | 1034304 B |        1.68 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165,761.37 μs** | **22,378.69 μs** | **1,226.651 μs** | **3,165,495.50 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17,829.75 μs | 55,542.03 μs | 3,044.446 μs |    17,157.41 μs | 0.006 |    0.00 | 2843600 B |        1.17 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **31.16 μs** |    **113.24 μs** |     **6.207 μs** |        **32.20 μs** |  **1.03** |    **0.26** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        26.25 μs |    115.46 μs |     6.329 μs |        24.13 μs |  0.87 |    0.24 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **34.61 μs** |     **62.87 μs** |     **3.446 μs** |        **36.26 μs** |  **1.01** |    **0.13** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        27.16 μs |     91.22 μs |     5.000 μs |        25.15 μs |  0.79 |    0.15 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **39.84 μs** |    **119.98 μs** |     **6.576 μs** |        **41.89 μs** |  **1.02** |    **0.22** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        25.69 μs |     89.55 μs |     4.908 μs |        22.92 μs |  0.66 |    0.15 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |       **197.10 μs** |  **5,259.34 μs** |   **288.282 μs** |        **33.93 μs** |  **4.46** |    **7.45** |    **2736 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        30.13 μs |     92.63 μs |     5.077 μs |        30.30 μs |  0.68 |    0.49 |    2440 B |        0.89 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.108 μs |   3.207 μs | 0.1758 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.290 μs |   2.337 μs | 0.1281 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.710 μs |   1.099 μs | 0.0602 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.494 μs |   5.263 μs | 0.2885 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.496 μs |   4.253 μs | 0.2331 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.914 μs |   5.066 μs | 0.2777 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 18.424 μs |   5.000 μs | 0.2740 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 30.790 μs | 117.089 μs | 6.4181 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.967 μs |   1.469 μs | 0.0805 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.302 μs |   1.541 μs | 0.0844 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.917 μs |  26.385 μs | 1.4462 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.408 μs |  31.552 μs | 1.7295 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.475 μs |  14.283 μs | 0.7829 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.465 μs |   6.078 μs | 0.3332 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,261.7 ns |   844.0 ns |  46.26 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,368.3 ns |   431.9 ns |  23.67 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,643.2 ns | 3,579.7 ns | 196.22 ns |  0.39 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,011.7 ns | 6,805.8 ns | 373.05 ns |  0.72 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    889.0 ns | 3,019.7 ns | 165.52 ns |  0.21 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,306.0 ns | 6,840.3 ns | 374.94 ns |  9.68 |    0.29 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,165.0 ns | 2,540.2 ns | 139.24 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,993.7 ns |   278.7 ns |  15.28 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean      | Error     | StdDev    | Allocated |
|------------------------ |----------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.76 μs |  25.56 μs |  1.401 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 499.13 μs |  33.68 μs |  1.846 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.13 μs |  19.71 μs |  1.080 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 235.55 μs | 201.20 μs | 11.028 μs |      80 B |


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