---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-10 22:12 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,887.01 μs** |    **870.47 μs** |    **47.713 μs** | **5,865.61 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,258.12 μs |  2,652.80 μs |   145.409 μs | 1,190.69 μs |  0.21 |    0.02 |        - |       - |   34.73 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,520.94 μs** | **16,018.62 μs** |   **878.035 μs** | **7,044.85 μs** |  **1.01** |    **0.14** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,975.61 μs | 23,099.63 μs | 1,266.169 μs | 2,259.93 μs |  0.40 |    0.15 |  15.6250 |       - |  341.36 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,408.90 μs** |    **526.48 μs** |    **28.858 μs** | **6,410.01 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,321.33 μs |  2,377.49 μs |   130.318 μs | 1,338.79 μs |  0.21 |    0.02 |        - |       - |   38.37 KB |        0.20 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,162.41 μs** |  **4,590.88 μs** |   **251.642 μs** | **9,051.04 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 8,317.66 μs | 76,265.35 μs | 4,180.361 μs | 6,359.54 μs |  0.91 |    0.40 |  23.4375 |  7.8125 |  398.31 KB |        0.21 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **76.77 μs** |    **116.05 μs** |     **6.361 μs** |    **75.03 μs** |  **1.00** |    **0.10** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    41.80 μs |     68.94 μs |     3.779 μs |    40.63 μs |  0.55 |    0.06 |   0.2441 |       - |   18.52 KB |        0.55 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **889.54 μs** |    **145.58 μs** |     **7.980 μs** |   **886.89 μs** |  **1.00** |    **0.01** |  **20.5078** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   668.70 μs |    461.11 μs |    25.275 μs |   669.36 μs |  0.75 |    0.03 |        - |       - |   48.21 KB |        0.14 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **970.70 μs** |  **6,234.91 μs** |   **341.756 μs** |   **785.47 μs** |  **1.07** |    **0.43** |   **7.3242** |       **-** |  **122.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   243.69 μs |    752.58 μs |    41.252 μs |   259.29 μs |  0.27 |    0.08 |   0.9766 |       - |   94.64 KB |        0.78 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **8,096.02 μs** |  **8,369.00 μs** |   **458.733 μs** | **7,874.82 μs** |  **1.00** |    **0.07** |  **74.2188** |       **-** | **1225.48 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,184.58 μs |  3,294.92 μs |   180.606 μs | 2,148.79 μs |  0.27 |    0.02 |   7.8125 |       - | 1099.55 KB |        0.90 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,335.93 μs** |     **75.41 μs** |     **4.133 μs** | **5,337.88 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,111.11 μs |    639.53 μs |    35.055 μs | 1,090.95 μs |  0.21 |    0.01 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,326.72 μs** |     **40.25 μs** |     **2.206 μs** | **5,326.48 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,094.59 μs |     42.92 μs |     2.353 μs | 1,095.58 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,330.11 μs** |     **52.27 μs** |     **2.865 μs** | **5,329.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,504.01 μs | 11,505.52 μs |   630.656 μs | 1,157.14 μs |  0.28 |    0.10 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,366.07 μs** |    **360.31 μs** |    **19.750 μs** | **5,363.93 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,094.30 μs |     30.90 μs |     1.694 μs | 1,094.46 μs |  0.20 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,165,410.44 μs** |  **29,052.14 μs** | **1,592.446 μs** | **3,166,155.61 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14,262.28 μs |  16,048.54 μs |   879.675 μs |    14,251.04 μs | 0.005 |    0.00 |  624896 B |        8.18 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167,407.00 μs** | **115,944.26 μs** | **6,355.296 μs** | **3,164,409.98 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    11,551.30 μs |  15,606.37 μs |   855.438 μs |    11,221.35 μs | 0.004 |    0.00 |  813528 B |        3.17 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164,153.36 μs** |  **33,486.36 μs** | **1,835.500 μs** | **3,164,946.38 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    13,103.62 μs |  33,650.40 μs | 1,844.492 μs |    12,219.17 μs | 0.004 |    0.00 | 1035752 B |        1.68 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,758.56 μs** |  **29,091.66 μs** | **1,594.612 μs** | **3,163,868.83 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13,810.26 μs |  23,281.68 μs | 1,276.147 μs |    14,003.59 μs | 0.004 |    0.00 | 2849584 B |        1.18 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **27.13 μs** |      **36.84 μs** |     **2.020 μs** |        **27.31 μs** |  **1.00** |    **0.09** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        24.82 μs |      68.31 μs |     3.744 μs |        23.00 μs |  0.92 |    0.13 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **25.67 μs** |      **43.96 μs** |     **2.410 μs** |        **26.27 μs** |  **1.01** |    **0.12** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        25.74 μs |     111.90 μs |     6.134 μs |        23.70 μs |  1.01 |    0.23 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **28.98 μs** |      **83.75 μs** |     **4.591 μs** |        **31.27 μs** |  **1.02** |    **0.21** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        27.53 μs |     243.05 μs |    13.322 μs |        20.60 μs |  0.97 |    0.43 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **30.08 μs** |      **96.25 μs** |     **5.276 μs** |        **29.22 μs** |  **1.02** |    **0.22** |    **2768 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        25.38 μs |      79.05 μs |     4.333 μs |        26.71 μs |  0.86 |    0.18 |    2440 B |        0.88 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.984 μs |   1.734 μs |  0.0950 μs | 25.987 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.220 μs |   7.542 μs |  0.4134 μs | 10.986 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.659 μs |   1.108 μs |  0.0607 μs |  8.665 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 11.834 μs |  46.774 μs |  2.5638 μs | 13.244 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.359 μs |   2.353 μs |  0.1290 μs | 11.322 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 14.551 μs |   9.953 μs |  0.5456 μs | 14.788 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.815 μs |   2.230 μs |  0.1222 μs | 12.875 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.193 μs |   5.106 μs |  0.2799 μs | 27.250 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.096 μs |   3.654 μs |  0.2003 μs |  9.046 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.820 μs |   3.481 μs |  0.1908 μs | 20.834 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.910 μs |  39.198 μs |  2.1486 μs | 32.535 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.253 μs |  32.398 μs |  1.7758 μs | 33.963 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.002 μs |   3.558 μs |  0.1950 μs |  4.915 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 21.991 μs | 261.464 μs | 14.3317 μs | 14.566 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.309 μs |  2.0823 μs | 0.1141 μs |  0.24 |    0.08 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.300 μs |  0.4213 μs | 0.0231 μs |  0.24 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.489 μs |  2.5739 μs | 0.1411 μs |  0.27 |    0.09 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.582 μs |  1.4033 μs | 0.0769 μs |  0.48 |    0.15 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.187 μs |  1.5684 μs | 0.0860 μs |  0.22 |    0.07 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39.738 μs |  3.1471 μs | 0.1725 μs |  7.34 |    2.33 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  6.008 μs | 44.8940 μs | 2.4608 μs |  1.11 |    0.54 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.027 μs |  3.6506 μs | 0.2001 μs |  0.74 |    0.24 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.048 μs |   4.804 μs |  0.2633 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 527.648 μs |  87.634 μs |  4.8035 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.168 μs |  25.959 μs |  1.4229 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 255.625 μs | 439.362 μs | 24.0829 μs |      80 B |


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