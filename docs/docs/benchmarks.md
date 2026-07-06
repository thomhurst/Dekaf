---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 16:28 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,075.82 μs** |    **351.766 μs** |    **19.281 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,410.06 μs |  2,832.177 μs |   155.241 μs |  0.23 |    0.02 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,262.86 μs** |  **1,138.673 μs** |    **62.415 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,323.70 μs |    672.359 μs |    36.854 μs |  0.32 |    0.00 |  15.6250 |       - |  340.02 KB |        0.32 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,624.60 μs** |    **450.913 μs** |    **24.716 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,560.00 μs |  2,124.816 μs |   116.468 μs |  0.24 |    0.02 |        - |       - |   36.53 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,376.48 μs** |  **2,919.078 μs** |   **160.005 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,643.91 μs |  8,718.563 μs |   477.894 μs |  0.58 |    0.04 |  15.6250 |       - |  359.55 KB |        0.19 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.92 μs** |     **42.889 μs** |     **2.351 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     75.31 μs |     90.858 μs |     4.980 μs |  0.60 |    0.04 |        - |       - |    4.51 KB |        0.13 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,220.96 μs** |  **2,605.640 μs** |   **142.824 μs** |  **1.01** |    **0.15** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    597.76 μs |    282.807 μs |    15.502 μs |  0.49 |    0.05 |        - |       - |   58.33 KB |        0.17 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **999.33 μs** |     **80.237 μs** |     **4.398 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.44 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    295.07 μs |    202.472 μs |    11.098 μs |  0.30 |    0.01 |        - |       - |   92.74 KB |        0.76 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,861.52 μs** | **33,247.793 μs** | **1,822.424 μs** |  **1.03** |    **0.28** |  **74.2188** |       **-** | **1226.06 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,477.69 μs | 26,190.678 μs | 1,435.599 μs |  0.41 |    0.17 |        - |       - |  900.49 KB |        0.73 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,401.83 μs** |     **56.188 μs** |     **3.080 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,351.83 μs |    166.966 μs |     9.152 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,404.89 μs** |     **67.444 μs** |     **3.697 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,362.62 μs |    200.810 μs |    11.007 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,420.88 μs** |    **171.856 μs** |     **9.420 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,376.21 μs |    463.356 μs |    25.398 μs |  0.25 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,410.63 μs** |     **42.816 μs** |     **2.347 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,103.49 μs |      5.818 μs |     0.319 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168,403.21 μs** | **17,530.50 μs** |   **960.906 μs** | **3,167,999.76 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14,855.51 μs | 14,846.71 μs |   813.798 μs |    15,218.05 μs | 0.005 |    0.00 |  617976 B |        8.09 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,448.07 μs** | **10,854.84 μs** |   **594.990 μs** | **3,165,363.80 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,210.09 μs | 10,655.56 μs |   584.067 μs |    14,290.02 μs | 0.004 |    0.00 |  810568 B |        3.16 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,133.87 μs** | **16,342.77 μs** |   **895.802 μs** | **3,166,532.32 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17,576.34 μs | 53,617.73 μs | 2,938.969 μs |    18,817.40 μs | 0.006 |    0.00 | 1068712 B |        1.73 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166,272.75 μs** |  **8,055.95 μs** |   **441.574 μs** | **3,166,077.59 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16,758.99 μs | 21,849.82 μs | 1,197.662 μs |    17,389.50 μs | 0.005 |    0.00 | 2842744 B |        1.17 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **32.20 μs** |     **57.82 μs** |     **3.169 μs** |        **32.87 μs** |  **1.01** |    **0.12** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        39.13 μs |    447.58 μs |    24.533 μs |        26.78 μs |  1.22 |    0.68 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **32.00 μs** |     **78.27 μs** |     **4.290 μs** |        **30.31 μs** |  **1.01** |    **0.16** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        26.17 μs |    103.32 μs |     5.663 μs |        23.48 μs |  0.83 |    0.18 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **29.51 μs** |     **90.25 μs** |     **4.947 μs** |        **27.48 μs** |  **1.02** |    **0.20** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        28.60 μs |     94.40 μs |     5.174 μs |        26.79 μs |  0.99 |    0.20 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **36.19 μs** |     **81.61 μs** |     **4.473 μs** |        **37.69 μs** |  **1.01** |    **0.16** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        28.39 μs |     48.10 μs |     2.637 μs |        28.62 μs |  0.79 |    0.11 |    2312 B |        0.87 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.547 μs | 10.103 μs | 0.5538 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.558 μs |  2.454 μs | 0.1345 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.586 μs | 45.707 μs | 2.5053 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.249 μs |  1.380 μs | 0.0756 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.396 μs |  8.171 μs | 0.4479 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.344 μs |  1.026 μs | 0.0562 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.736 μs |  5.139 μs | 0.2817 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.956 μs |  8.624 μs | 0.4727 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.191 μs |  6.618 μs | 0.3628 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 27.297 μs |  3.596 μs | 0.1971 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 29.768 μs | 31.963 μs | 1.7520 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.269 μs | 19.670 μs | 1.0782 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.551 μs |  3.353 μs | 0.1838 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.549 μs | 26.170 μs | 1.4345 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,412.5 ns |  4,759.5 ns | 260.89 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,559.0 ns |  1,818.6 ns |  99.68 ns |  0.37 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,689.7 ns |  3,869.4 ns | 212.10 ns |  0.40 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,484.3 ns |  3,973.3 ns | 217.79 ns |  0.82 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    808.7 ns |  3,135.8 ns | 171.88 ns |  0.19 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,610.7 ns | 14,755.2 ns | 808.78 ns | 10.04 |    0.32 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,247.8 ns |  2,538.5 ns | 139.14 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,132.8 ns | 15,193.9 ns | 832.83 ns |  0.97 |    0.17 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.353 μs |   3.596 μs |  0.1971 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 547.041 μs | 753.903 μs | 41.3240 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.431 μs |   8.949 μs |  0.4905 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 241.880 μs |  52.502 μs |  2.8778 μs |      80 B |


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