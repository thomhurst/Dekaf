---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 08:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,197.24 μs** | **1,169.128 μs** |  **64.084 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,425.43 μs | 2,979.007 μs | 163.289 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,445.11 μs** |   **493.838 μs** |  **27.069 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,337.93 μs |   145.318 μs |   7.965 μs |  0.31 |    0.00 |  15.6250 |       - |  339.57 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,222.02 μs** |   **975.576 μs** |  **53.475 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,390.77 μs | 3,316.340 μs | 181.780 μs |  0.22 |    0.03 |        - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,526.31 μs** | **4,833.018 μs** | **264.914 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,648.58 μs | 5,519.879 μs | 302.563 μs |  0.53 |    0.02 |  15.6250 |       - |  361.82 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **139.52 μs** |   **131.292 μs** |   **7.197 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |   **41.97 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.63 μs |   228.955 μs |  12.550 μs |  0.46 |    0.08 |        - |       - |    9.64 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,428.46 μs** | **1,270.728 μs** |  **69.653 μs** |  **1.00** |    **0.06** |  **25.3906** |       **-** |  **422.53 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    667.32 μs | 1,204.634 μs |  66.030 μs |  0.47 |    0.04 |        - |       - |   70.35 KB |        0.17 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    202.03 μs |   551.409 μs |  30.225 μs |     ? |       ? |   0.9766 |       - |  100.62 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,063.16 μs | 2,060.786 μs | 112.959 μs |     ? |       ? |   7.8125 |       - | 1016.15 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,446.38 μs** |    **35.726 μs** |   **1.958 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,399.98 μs |   335.434 μs |  18.386 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,450.75 μs** |    **93.931 μs** |   **5.149 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,340.11 μs |   410.614 μs |  22.507 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,482.61 μs** |   **185.661 μs** |  **10.177 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,247.76 μs |   207.368 μs |  11.367 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,465.86 μs** |   **188.765 μs** |  **10.347 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.99 μs |     7.291 μs |   0.400 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.335 ms** | **29.392 ms** | **1.6111 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.459 ms | 30.399 ms | 1.6663 ms | 0.006 |  593.34 KB |        7.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.460 ms** | **25.518 ms** | **1.3987 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.016 ms | 44.791 ms | 2.4551 ms | 0.005 |  780.76 KB |        3.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.863 ms** | **25.022 ms** | **1.3715 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.669 ms | 58.288 ms | 3.1950 ms | 0.006 | 1138.97 KB |        1.89 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.478 ms** | **28.093 ms** | **1.5399 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.335 ms | 16.612 ms | 0.9106 ms | 0.005 | 2806.67 KB |        1.19 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.283 ms** | **36.917 ms** | **2.0236 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.250 ms | 10.111 ms | 0.5542 ms | 0.002 |  185.49 KB |       77.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.016 ms** |  **2.650 ms** | **0.1453 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.027 ms | 24.323 ms | 1.3332 ms | 0.002 |   195.4 KB |       46.92 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.362 ms** | **27.242 ms** | **1.4932 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.850 ms | 16.371 ms | 0.8974 ms | 0.002 |  185.94 KB |       77.27 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.261 ms** | **29.086 ms** | **1.5943 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.884 ms | 16.820 ms | 0.9220 ms | 0.002 |  232.08 KB |       55.53 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 21.119 μs | 172.410 μs | 9.4504 μs | 15.794 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.749 μs |  11.947 μs | 0.6548 μs |  8.540 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.871 μs |  12.474 μs | 0.6837 μs |  9.770 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.416 μs |  85.227 μs | 4.6716 μs | 26.058 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.320 μs |  12.314 μs | 0.6750 μs | 11.991 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.417 μs |  93.341 μs | 5.1164 μs | 16.673 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 22.678 μs |  37.202 μs | 2.0392 μs | 22.582 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.913 μs |  40.105 μs | 2.1983 μs | 20.254 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.888 μs |   8.792 μs | 0.4819 μs |  4.690 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.693 μs |   7.016 μs | 0.3846 μs | 10.590 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.667 μs |  0.7957 μs | 0.0436 μs |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.498 μs |  5.6296 μs | 0.3086 μs |  0.28 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  2.191 μs |  8.8031 μs | 0.4825 μs |  0.41 |    0.09 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.956 μs |  5.7893 μs | 0.3173 μs |  0.56 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  2.152 μs |  4.5457 μs | 0.2492 μs |  0.41 |    0.06 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 34.734 μs | 27.0241 μs | 1.4813 μs |  6.55 |    0.76 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5.357 μs | 12.2839 μs | 0.6733 μs |  1.01 |    0.16 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.438 μs | 11.5849 μs | 0.6350 μs |  0.84 |    0.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.301 μs |  18.318 μs |  1.004 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   462.178 μs | 155.286 μs |  8.512 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.853 μs |  22.055 μs |  1.209 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,380.438 μs | 376.171 μs | 20.619 μs |    1280 B |


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