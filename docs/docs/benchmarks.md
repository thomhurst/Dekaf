---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 23:03 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,219.27 μs** |   **867.78 μs** |  **47.566 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,319.04 μs |   905.84 μs |  49.652 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,451.02 μs** |   **196.28 μs** |  **10.759 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,337.33 μs |   581.23 μs |  31.859 μs |  0.31 |    0.00 |  15.6250 |       - |  339.43 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,206.58 μs** |   **741.65 μs** |  **40.652 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,232.90 μs | 2,087.38 μs | 114.416 μs |  0.20 |    0.02 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,756.05 μs** | **1,088.78 μs** |  **59.680 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,229.91 μs | 1,972.79 μs | 108.135 μs |  0.49 |    0.01 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.07 μs** |   **131.85 μs** |   **7.227 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |   **41.64 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     57.71 μs |    41.02 μs |   2.249 μs |  0.40 |    0.02 |   0.2441 |       - |    8.75 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,458.92 μs** | **1,670.10 μs** |  **91.544 μs** |  **1.00** |    **0.08** |  **25.3906** |       **-** |   **421.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    693.90 μs |   468.40 μs |  25.675 μs |  0.48 |    0.03 |        - |       - |    62.4 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    205.76 μs |   421.71 μs |  23.115 μs |     ? |       ? |   0.9766 |       - |  100.47 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,118.23 μs | 3,422.35 μs | 187.591 μs |     ? |       ? |   7.8125 |       - | 1053.95 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,430.39 μs** |   **151.50 μs** |   **8.304 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,354.67 μs |   656.62 μs |  35.991 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,426.87 μs** |    **69.26 μs** |   **3.796 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,174.97 μs |    40.67 μs |   2.229 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,426.84 μs** |    **43.37 μs** |   **2.377 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,115.74 μs |    49.01 μs |   2.687 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,430.56 μs** |    **21.89 μs** |   **1.200 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,405.52 μs |   398.81 μs |  21.860 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.381 ms** |  **9.881 ms** | **0.5416 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.913 ms | 43.970 ms | 2.4101 ms | 0.005 |  599.21 KB |        8.03 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.943 ms** | **62.008 ms** | **3.3989 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.894 ms | 31.685 ms | 1.7368 ms | 0.005 |   779.8 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.426 ms** |  **8.318 ms** | **0.4559 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.277 ms | 15.973 ms | 0.8755 ms | 0.005 |  996.01 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.188 ms** |  **6.630 ms** | **0.3634 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.984 ms | 39.650 ms | 2.1734 ms | 0.005 | 2760.25 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.823 ms** | **19.036 ms** | **1.0434 ms** | **1.000** |   **26.45 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.464 ms | 11.714 ms | 0.6421 ms | 0.002 |  186.65 KB |        7.06 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.896 ms** | **16.786 ms** | **0.9201 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.646 ms |  9.435 ms | 0.5172 ms | 0.002 |  187.73 KB |       45.08 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.763 ms** | **37.338 ms** | **2.0466 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.772 ms | 39.413 ms | 2.1603 ms | 0.002 |  276.08 KB |      114.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.194 ms** | **21.016 ms** | **1.1520 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.261 ms |  5.267 ms | 0.2887 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.392 μs | 205.970 μs | 11.2899 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.072 μs |   5.592 μs |  0.3065 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.381 μs |   4.397 μs |  0.2410 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.753 μs |   1.005 μs |  0.0551 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.984 μs |   2.292 μs |  0.1256 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.311 μs |   2.409 μs |  0.1320 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.750 μs |  12.594 μs |  0.6903 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.278 μs |  10.254 μs |  0.5620 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.997 μs |   6.793 μs |  0.3723 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.387 μs |   1.968 μs |  0.1079 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,435.7 ns |   2,319.9 ns |    127.16 ns |  0.35 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,332.7 ns |   1,910.8 ns |    104.74 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,426.7 ns |   1,100.7 ns |     60.34 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,428.7 ns |   1,225.2 ns |     67.16 ns |  0.59 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    840.7 ns |     958.5 ns |     52.54 ns |  0.20 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 56,430.3 ns | 395,265.4 ns | 21,665.83 ns | 13.63 |    4.54 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,144.0 ns |   2,207.9 ns |    121.02 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,838.8 ns |   2,938.9 ns |    161.09 ns |  0.93 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.907 μs |   8.036 μs |  0.4405 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   550.365 μs | 537.061 μs | 29.4381 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.001 μs |  10.191 μs |  0.5586 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,645.619 μs | 104.880 μs |  5.7488 μs |    1280 B |


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