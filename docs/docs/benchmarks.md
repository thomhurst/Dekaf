---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 14:54 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,236.65 μs** |    **906.027 μs** |    **49.662 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,705.06 μs |  3,007.594 μs |   164.856 μs |  0.27 |    0.02 |        - |       - |   34.77 KB |        0.33 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,504.98 μs** |    **774.987 μs** |    **42.480 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,362.20 μs |    497.760 μs |    27.284 μs |  0.31 |    0.00 |  15.6250 |       - |  339.62 KB |        0.32 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,161.10 μs** |    **920.349 μs** |    **50.447 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,734.74 μs |  1,045.156 μs |    57.289 μs |  0.28 |    0.01 |        - |       - |   36.63 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **14,094.04 μs** | **15,601.201 μs** |   **855.154 μs** |  **1.00** |    **0.07** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,140.73 μs |  5,147.263 μs |   282.139 μs |  0.51 |    0.03 |  15.6250 |       - |  358.95 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **139.85 μs** |     **86.408 μs** |     **4.736 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     76.55 μs |    133.040 μs |     7.292 μs |  0.55 |    0.05 |        - |       - |    7.06 KB |        0.21 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,403.40 μs** |    **385.879 μs** |    **21.151 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    689.41 μs |  1,075.141 μs |    58.932 μs |  0.49 |    0.04 |        - |       - |  119.67 KB |        0.36 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **645.87 μs** |  **7,904.423 μs** |   **433.268 μs** |  **1.48** |    **1.43** |   **7.3242** |       **-** |  **122.55 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    244.34 μs |    522.331 μs |    28.631 μs |  0.56 |    0.39 |   0.9766 |       - |   94.79 KB |        0.77 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,338.81 μs** | **23,176.194 μs** | **1,270.365 μs** |  **1.01** |    **0.16** |  **74.2188** |       **-** |  **1226.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,524.30 μs |  7,658.460 μs |   419.786 μs |  0.25 |    0.05 |   7.8125 |       - |  972.42 KB |        0.79 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,430.17 μs** |    **151.961 μs** |     **8.329 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,389.10 μs |    402.020 μs |    22.036 μs |  0.26 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,419.94 μs** |      **9.990 μs** |     **0.548 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,370.74 μs |    772.489 μs |    42.343 μs |  0.25 |    0.01 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,434.82 μs** |     **87.322 μs** |     **4.786 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,344.95 μs |    127.641 μs |     6.996 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,436.60 μs** |     **89.612 μs** |     **4.912 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,302.30 μs |    159.667 μs |     8.752 μs |  0.24 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168,608.43 μs** |  **30,895.97 μs** | **1,693.512 μs** | **3,169,005.58 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17,678.79 μs |  35,748.40 μs | 1,959.490 μs |    18,665.89 μs | 0.006 |    0.00 |  623080 B |        8.15 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166,816.47 μs** |  **18,544.92 μs** | **1,016.510 μs** | **3,166,545.85 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17,091.31 μs |  15,538.63 μs |   851.725 μs |    17,142.37 μs | 0.005 |    0.00 |  805728 B |        3.14 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,294.06 μs** |  **21,095.00 μs** | **1,156.288 μs** | **3,165,799.50 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    21,288.43 μs | 110,043.30 μs | 6,031.845 μs |    19,765.86 μs | 0.007 |    0.00 | 1075384 B |        1.74 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166,454.38 μs** |  **14,465.97 μs** |   **792.929 μs** | **3,166,525.99 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16,766.40 μs |  26,307.22 μs | 1,441.987 μs |    17,406.33 μs | 0.005 |    0.00 | 2842352 B |        1.17 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **49.59 μs** |     **491.12 μs** |    **26.920 μs** |        **34.34 μs** |  **1.18** |    **0.72** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        32.72 μs |     110.07 μs |     6.033 μs |        29.88 μs |  0.78 |    0.31 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **39.10 μs** |      **57.80 μs** |     **3.168 μs** |        **38.07 μs** |  **1.00** |    **0.10** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        29.17 μs |      66.72 μs |     3.657 μs |        28.50 μs |  0.75 |    0.10 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **43.56 μs** |      **69.10 μs** |     **3.788 μs** |        **43.24 μs** |  **1.01** |    **0.11** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        35.16 μs |     252.22 μs |    13.825 μs |        27.31 μs |  0.81 |    0.28 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **51.92 μs** |     **529.56 μs** |    **29.027 μs** |        **36.62 μs** |  **1.19** |    **0.75** |    **2736 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        30.87 μs |      70.62 μs |     3.871 μs |        29.69 μs |  0.71 |    0.27 |    2312 B |        0.85 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.097 μs |  16.576 μs | 0.9086 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.667 μs |  15.137 μs | 0.8297 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.142 μs |  22.797 μs | 1.2496 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 11.137 μs |  15.876 μs | 0.8702 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.765 μs |  14.970 μs | 0.8206 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.826 μs |  39.846 μs | 2.1841 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 18.748 μs |  13.467 μs | 0.7382 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 39.299 μs |  69.937 μs | 3.8335 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.906 μs |   8.764 μs | 0.4804 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 14.608 μs |   9.249 μs | 0.5069 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 41.228 μs |  77.557 μs | 4.2512 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 50.963 μs | 110.542 μs | 6.0592 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.830 μs |  27.791 μs | 1.5233 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.011 μs | 119.171 μs | 6.5322 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.830 μs |  12.528 μs | 0.6867 μs |  1.5230 μs |  0.42 |    0.17 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.272 μs |  14.891 μs | 0.8162 μs |  0.9630 μs |  0.29 |    0.18 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  3.100 μs |  16.913 μs | 0.9271 μs |  2.8235 μs |  0.72 |    0.26 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.802 μs |  18.517 μs | 1.0150 μs |  3.0820 μs |  0.65 |    0.26 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  2.004 μs |  20.820 μs | 1.1412 μs |  2.6500 μs |  0.46 |    0.26 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 38.425 μs | 141.830 μs | 7.7742 μs | 35.8495 μs |  8.89 |    2.64 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.568 μs |  24.937 μs | 1.3669 μs |  4.1605 μs |  1.06 |    0.38 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3.975 μs |  34.511 μs | 1.8917 μs |  3.0520 μs |  0.92 |    0.45 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.663 μs |  42.133 μs |  2.309 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 422.976 μs | 403.764 μs | 22.132 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.206 μs |  27.310 μs |  1.497 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 219.596 μs | 883.404 μs | 48.422 μs |      80 B |


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