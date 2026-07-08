---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-08 21:26 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,112.46 μs** |    **588.69 μs** |    **32.268 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,627.05 μs |  3,656.14 μs |   200.406 μs |  0.27 |    0.03 |        - |       - |   34.75 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,383.14 μs** |    **694.38 μs** |    **38.061 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,529.58 μs |    446.10 μs |    24.452 μs |  0.34 |    0.00 |  15.6250 |       - |   341.6 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,157.95 μs** |    **900.63 μs** |    **49.367 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,145.85 μs |    552.73 μs |    30.297 μs |  0.35 |    0.00 |        - |       - |   38.64 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,669.13 μs** |    **379.80 μs** |    **20.818 μs** |  **1.00** |    **0.00** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 17,074.91 μs | 39,318.31 μs | 2,155.169 μs |  1.35 |    0.15 |  15.6250 |       - |  418.59 KB |        0.22 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.35 μs** |    **106.62 μs** |     **5.844 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     86.05 μs |    177.92 μs |     9.753 μs |  0.61 |    0.06 |        - |       - |   11.43 KB |        0.34 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,327.30 μs** |    **401.25 μs** |    **21.994 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    816.68 μs |    763.01 μs |    41.823 μs |  0.62 |    0.03 |        - |       - |    93.9 KB |        0.28 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,090.86 μs** |     **58.81 μs** |     **3.224 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.55 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    423.17 μs |    411.02 μs |    22.529 μs |  0.39 |    0.02 |        - |       - |  108.61 KB |        0.89 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,891.05 μs** | **33,246.70 μs** | **1,822.364 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1226.42 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,543.72 μs | 12,736.21 μs |   698.115 μs |  0.37 |    0.09 |        - |       - | 1074.21 KB |        0.88 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,424.41 μs** |     **96.27 μs** |     **5.277 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,408.20 μs |    172.78 μs |     9.471 μs |  0.26 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,425.20 μs** |     **46.03 μs** |     **2.523 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,389.76 μs |    211.63 μs |    11.600 μs |  0.26 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.74 μs** |     **41.71 μs** |     **2.286 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,384.40 μs |    202.81 μs |    11.117 μs |  0.26 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,424.14 μs** |     **65.61 μs** |     **3.596 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,354.19 μs |    170.57 μs |     9.349 μs |  0.25 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167,357.64 μs** | **13,824.34 μs** |   **757.759 μs** | **3,167,187.65 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16,493.15 μs | 10,582.76 μs |   580.077 μs |    16,550.58 μs | 0.005 |    0.00 |  620808 B |        8.12 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,240.31 μs** | **15,366.85 μs** |   **842.309 μs** | **3,165,407.19 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13,737.24 μs | 24,796.75 μs | 1,359.194 μs |    13,930.22 μs | 0.004 |    0.00 |  806680 B |        3.15 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,328.52 μs** | **15,637.14 μs** |   **857.124 μs** | **3,165,316.15 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18,426.96 μs | 49,531.03 μs | 2,714.963 μs |    17,267.16 μs | 0.006 |    0.00 | 1035072 B |        1.68 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,769.58 μs** | **22,870.83 μs** | **1,253.627 μs** | **3,165,288.38 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15,449.36 μs | 25,662.31 μs | 1,406.638 μs |    15,681.91 μs | 0.005 |    0.00 | 2923064 B |        1.21 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **30.19 μs** |     **17.48 μs** |     **0.958 μs** |        **30.56 μs** |  **1.00** |    **0.04** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        25.42 μs |     98.68 μs |     5.409 μs |        24.07 μs |  0.84 |    0.16 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **30.60 μs** |     **34.05 μs** |     **1.866 μs** |        **29.72 μs** |  **1.00** |    **0.07** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        32.13 μs |    319.45 μs |    17.510 μs |        24.16 μs |  1.05 |    0.50 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **30.44 μs** |     **62.78 μs** |     **3.441 μs** |        **32.19 μs** |  **1.01** |    **0.14** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        26.36 μs |     79.91 μs |     4.380 μs |        26.23 μs |  0.87 |    0.16 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **30.17 μs** |     **16.86 μs** |     **0.924 μs** |        **29.80 μs** |  **1.00** |    **0.04** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        26.74 μs |     69.86 μs |     3.829 μs |        24.53 μs |  0.89 |    0.11 |    2408 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.456 μs |  49.761 μs | 2.7276 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.083 μs |   3.146 μs | 0.1724 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.907 μs |   6.585 μs | 0.3609 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.976 μs |   9.989 μs | 0.5475 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.188 μs |   1.425 μs | 0.0781 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.893 μs |   5.834 μs | 0.3198 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.944 μs |   4.585 μs | 0.2513 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.140 μs |   1.590 μs | 0.0872 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.127 μs |   4.199 μs | 0.2302 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.589 μs |  10.344 μs | 0.5670 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.149 μs |  35.887 μs | 1.9671 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 41.209 μs | 142.320 μs | 7.8010 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.612 μs |   2.207 μs | 0.1210 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.829 μs |  10.481 μs | 0.5745 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,318.3 ns | 2,796.7 ns | 153.30 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,265.7 ns | 2,187.0 ns | 119.88 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,564.5 ns | 5,109.1 ns | 280.05 ns |  0.36 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,047.5 ns | 8,539.3 ns | 468.07 ns |  0.71 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    711.7 ns |   374.0 ns |  20.50 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,874.7 ns | 6,584.7 ns | 360.93 ns |  9.26 |    0.45 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,313.2 ns | 4,463.9 ns | 244.68 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,686.0 ns | 5,885.3 ns | 322.60 ns |  1.09 |    0.08 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.634 μs |  32.228 μs |  1.7665 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 531.512 μs | 592.000 μs | 32.4495 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.013 μs |   1.104 μs |  0.0605 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 228.848 μs |  53.194 μs |  2.9157 μs |      80 B |


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