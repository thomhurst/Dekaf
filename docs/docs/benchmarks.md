---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 14:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,176.43 μs** |    **520.17 μs** |    **28.512 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,265.46 μs |    954.36 μs |    52.312 μs |  0.20 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,351.27 μs** |    **490.15 μs** |    **26.867 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,324.91 μs |    248.01 μs |    13.594 μs |  0.32 |    0.00 |  15.6250 |       - |  339.31 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,191.92 μs** |    **513.65 μs** |    **28.155 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,421.81 μs |  4,512.79 μs |   247.361 μs |  0.23 |    0.03 |        - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **29,100.40 μs** | **93,762.98 μs** | **5,139.465 μs** |  **1.02** |    **0.23** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,396.68 μs |  1,850.02 μs |   101.406 μs |  0.22 |    0.04 |  15.6250 |       - |  361.78 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.36 μs** |    **244.80 μs** |    **13.419 μs** |  **1.01** |    **0.13** |   **2.4414** |       **-** |   **41.71 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     54.30 μs |     65.77 μs |     3.605 μs |  0.44 |    0.05 |        - |       - |    7.45 KB |        0.18 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,425.52 μs** |    **424.67 μs** |    **23.278 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |   **421.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    648.61 μs |    401.49 μs |    22.007 μs |  0.46 |    0.01 |        - |       - |   66.09 KB |        0.16 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    213.48 μs |    485.65 μs |    26.620 μs |     ? |       ? |   0.9766 |       - |   97.39 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,333.05 μs** |    **539.49 μs** |    **29.571 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** | **1227.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,158.71 μs |  5,147.71 μs |   282.163 μs |  0.93 |    0.11 |   7.8125 |       - |  983.32 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,425.51 μs** |     **81.31 μs** |     **4.457 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,117.71 μs |     54.97 μs |     3.013 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,674.53 μs** |  **7,799.83 μs** |   **427.535 μs** |  **1.00** |    **0.09** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,129.26 μs |    231.19 μs |    12.672 μs |  0.20 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,430.50 μs** |     **47.15 μs** |     **2.585 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,117.64 μs |     38.93 μs |     2.134 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,434.69 μs** |     **54.78 μs** |     **3.002 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,115.74 μs |     26.98 μs |     1.479 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,151.971 ms** | **555.967 ms** | **30.4745 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.370 ms |  41.771 ms |  2.2896 ms | 0.006 |  594.03 KB |        7.96 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.807 ms** |  **24.542 ms** |  **1.3452 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.366 ms |  30.879 ms |  1.6926 ms | 0.005 |  776.23 KB |        3.10 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.567 ms** |   **8.737 ms** |  **0.4789 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.770 ms |  57.789 ms |  3.1676 ms | 0.006 |  995.08 KB |        1.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.898 ms** |  **22.352 ms** |  **1.2252 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.208 ms |  21.463 ms |  1.1765 ms | 0.005 | 2761.13 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.170 ms** |  **17.718 ms** |  **0.9712 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.363 ms |  19.932 ms |  1.0925 ms | 0.002 |  184.41 KB |       76.64 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.508 ms** |   **9.870 ms** |  **0.5410 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.178 ms |  21.956 ms |  1.2035 ms | 0.002 |  186.29 KB |       44.74 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.598 ms** |  **31.869 ms** |  **1.7468 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.611 ms |   1.093 ms |  0.0599 ms | 0.002 |  184.76 KB |       76.78 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.984 ms** |   **5.073 ms** |  **0.2781 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.927 ms |   9.418 ms |  0.5162 ms | 0.002 |  187.04 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.782 μs |  1.5191 μs | 0.0833 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.501 μs |  2.0013 μs | 0.1097 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.422 μs |  5.1855 μs | 0.2842 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.890 μs |  6.8665 μs | 0.3764 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.022 μs |  0.7995 μs | 0.0438 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.447 μs |  2.8320 μs | 0.1552 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.944 μs | 15.8911 μs | 0.8710 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.660 μs | 32.7210 μs | 1.7935 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.576 μs |  0.7669 μs | 0.0420 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.539 μs |  6.2863 μs | 0.3446 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,275.3 ns |   2,001.3 ns |   109.70 ns |  1,212.0 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,328.7 ns |   3,140.5 ns |   172.14 ns |  1,272.0 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,291.2 ns |   1,540.5 ns |    84.44 ns |  1,247.5 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,585.3 ns |   1,287.5 ns |    70.57 ns |  2,555.0 ns |  0.62 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    748.7 ns |     936.2 ns |    51.32 ns |    762.0 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 38,994.8 ns |  16,725.8 ns |   916.80 ns | 39,108.5 ns |  9.40 |    0.24 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,149.7 ns |   1,369.3 ns |    75.06 ns |  4,153.0 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  7,580.3 ns | 113,432.9 ns | 6,217.64 ns |  4,167.0 ns |  1.83 |    1.30 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.07 μs |   4.575 μs |  0.251 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   504.31 μs |  38.507 μs |  2.111 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.28 μs |   3.655 μs |  0.200 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,686.09 μs | 516.776 μs | 28.326 μs |    1280 B |


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