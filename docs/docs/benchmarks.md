---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-10 10:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,944.95 μs** |    **717.78 μs** |    **39.344 μs** |  **1.00** |    **0.01** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,608.35 μs |  2,889.27 μs |   158.371 μs |  0.27 |    0.02 |       - |   34.74 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,365.99 μs** |    **583.00 μs** |    **31.956 μs** |  **1.00** |    **0.01** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,321.51 μs |  1,004.11 μs |    55.039 μs |  0.32 |    0.01 |       - |  341.23 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,364.20 μs** |    **100.83 μs** |     **5.527 μs** |  **1.00** |    **0.00** |       **-** |  **194.03 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,186.62 μs |  1,410.43 μs |    77.310 μs |  0.19 |    0.01 |       - |   38.27 KB |        0.20 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,939.77 μs** |  **1,204.26 μs** |    **66.010 μs** |  **1.00** |    **0.01** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,896.78 μs |  8,175.67 μs |   448.136 μs |  0.74 |    0.05 |       - |   366.8 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **119.29 μs** |     **15.45 μs** |     **0.847 μs** |  **1.00** |    **0.01** |  **0.3662** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    59.65 μs |     76.41 μs |     4.188 μs |  0.50 |    0.03 |       - |    8.75 KB |        0.26 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,104.46 μs** |  **2,385.40 μs** |   **130.752 μs** |  **1.01** |    **0.15** |  **3.9063** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   632.73 μs |    636.84 μs |    34.907 μs |  0.58 |    0.07 |       - |   53.24 KB |        0.16 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **602.21 μs** |    **319.78 μs** |    **17.528 μs** |  **1.00** |    **0.04** |  **1.4648** |  **121.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   223.55 μs |    840.96 μs |    46.096 μs |  0.37 |    0.07 |       - |   99.79 KB |        0.82 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,914.75 μs** | **27,551.88 μs** | **1,510.212 μs** |  **1.03** |    **0.26** | **13.6719** | **1219.08 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,874.98 μs |  6,311.06 μs |   345.931 μs |  0.24 |    0.06 |       - |  1003.3 KB |        0.82 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,321.65 μs** |     **26.61 μs** |     **1.459 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,241.76 μs |    412.20 μs |    22.594 μs |  0.23 |    0.00 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,327.98 μs** |    **127.84 μs** |     **7.007 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,189.17 μs |  1,680.39 μs |    92.108 μs |  0.22 |    0.01 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,310.36 μs** |    **100.36 μs** |     **5.501 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,342.92 μs |  1,692.92 μs |    92.795 μs |  0.25 |    0.02 |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,315.17 μs** |     **25.57 μs** |     **1.402 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,096.94 μs |     19.24 μs |     1.055 μs |  0.21 |    0.00 |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167,087.49 μs** | **13,036.423 μs** |   **714.570 μs** | **3,167,361.72 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    13,847.24 μs | 23,540.620 μs | 1,290.341 μs |    13,423.78 μs | 0.004 |    0.00 |  626240 B |        8.20 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164,344.16 μs** | **11,461.993 μs** |   **628.270 μs** | **3,164,550.42 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12,367.14 μs | 26,282.906 μs | 1,440.655 μs |    13,168.28 μs | 0.004 |    0.00 |  808824 B |        3.15 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164,938.35 μs** |  **6,432.430 μs** |   **352.583 μs** | **3,164,905.63 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17,153.04 μs | 57,308.861 μs | 3,141.292 μs |    15,497.49 μs | 0.005 |    0.00 | 1034184 B |        1.68 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165,615.55 μs** |  **6,337.800 μs** |   **347.396 μs** | **3,165,486.27 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16,875.91 μs | 68,275.652 μs | 3,742.419 μs |    14,890.25 μs | 0.005 |    0.00 | 2844632 B |        1.17 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **34.71 μs** |     **18.412 μs** |     **1.009 μs** |        **35.04 μs** |  **1.00** |    **0.04** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        40.74 μs |    117.012 μs |     6.414 μs |        39.94 μs |  1.17 |    0.16 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **31.83 μs** |     **29.009 μs** |     **1.590 μs** |        **31.36 μs** |  **1.00** |    **0.06** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        45.98 μs |    202.084 μs |    11.077 μs |        45.74 μs |  1.45 |    0.31 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **34.82 μs** |     **14.884 μs** |     **0.816 μs** |        **34.88 μs** |  **1.00** |    **0.03** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        47.85 μs |    382.686 μs |    20.976 μs |        37.88 μs |  1.37 |    0.52 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **37.68 μs** |     **86.770 μs** |     **4.756 μs** |        **39.91 μs** |  **1.01** |    **0.16** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        43.39 μs |      2.110 μs |     0.116 μs |        43.35 μs |  1.16 |    0.14 |    2408 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.488 μs |  3.770 μs | 0.2066 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.623 μs |  2.987 μs | 0.1637 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.209 μs |  3.972 μs | 0.2177 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  6.269 μs |  6.854 μs | 0.3757 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.798 μs |  2.107 μs | 0.1155 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 10.349 μs |  5.201 μs | 0.2851 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 11.477 μs |  3.049 μs | 0.1671 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.538 μs |  4.132 μs | 0.2265 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  7.952 μs |  3.014 μs | 0.1652 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 15.694 μs |  4.613 μs | 0.2528 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.444 μs | 35.154 μs | 1.9269 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 25.124 μs | 43.302 μs | 2.3735 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.913 μs |  7.414 μs | 0.4064 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  9.821 μs | 13.991 μs | 0.7669 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,035.3 ns |  1,393.4 ns |  76.38 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,118.7 ns |  4,006.7 ns | 219.62 ns |  0.32 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,232.0 ns |  6,961.4 ns | 381.58 ns |  0.35 |    0.10 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,006.3 ns |  5,102.8 ns | 279.70 ns |  0.57 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    575.5 ns |  2,534.5 ns | 138.92 ns |  0.16 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 27,681.3 ns | 12,926.8 ns | 708.56 ns |  7.86 |    0.49 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,532.3 ns |  4,235.2 ns | 232.14 ns |  1.00 |    0.08 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,011.3 ns |  7,096.8 ns | 389.00 ns |  0.86 |    0.11 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   8.456 μs |   5.962 μs |  0.3268 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 412.304 μs | 482.170 μs | 26.4294 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.405 μs |   2.646 μs |  0.1450 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 197.873 μs | 463.932 μs | 25.4297 μs |      80 B |


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