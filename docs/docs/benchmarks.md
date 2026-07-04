---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 06:30 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,213.16 μs** | **1,102.52 μs** |  **60.433 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,386.48 μs | 1,872.22 μs | 102.623 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,382.13 μs** | **1,590.35 μs** |  **87.173 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,319.60 μs |   405.76 μs |  22.241 μs |  0.31 |    0.00 |  15.6250 |       - |  339.59 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,255.19 μs** |   **862.36 μs** |  **47.269 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,360.39 μs | 3,942.18 μs | 216.084 μs |  0.22 |    0.03 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,447.65 μs** |   **839.79 μs** |  **46.032 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,788.12 μs | 7,835.27 μs | 429.478 μs |  0.55 |    0.03 |  15.6250 |       - |  361.61 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.40 μs** |   **124.09 μs** |   **6.802 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |   **41.89 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     72.38 μs |   165.80 μs |   9.088 μs |  0.52 |    0.06 |        - |       - |    7.78 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,411.77 μs** |   **395.12 μs** |  **21.658 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **422.16 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    646.66 μs | 1,092.65 μs |  59.892 μs |  0.46 |    0.04 |        - |       - |    88.8 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    210.50 μs |   248.12 μs |  13.600 μs |     ? |       ? |   0.9766 |       - |   96.05 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,141.25 μs | 3,990.79 μs | 218.749 μs |     ? |       ? |   7.8125 |       - | 1026.75 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,467.79 μs** |   **564.70 μs** |  **30.953 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,216.68 μs |   275.53 μs |  15.103 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,493.93 μs** |   **127.13 μs** |   **6.968 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,116.04 μs |    38.94 μs |   2.134 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,571.41 μs** |   **296.45 μs** |  **16.249 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,375.30 μs |   364.44 μs |  19.976 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,531.29 μs** |   **771.75 μs** |  **42.302 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,357.31 μs |   830.75 μs |  45.536 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.986 ms** |  **5.404 ms** | **0.2962 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.552 ms | 12.550 ms | 0.6879 ms | 0.005 | 595.63 KB |        7.98 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.716 ms** | **27.024 ms** | **1.4813 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.338 ms | 11.985 ms | 0.6569 ms | 0.004 | 775.56 KB |        3.10 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.528 ms** |  **6.546 ms** | **0.3588 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.620 ms | 47.508 ms | 2.6041 ms | 0.006 | 995.44 KB |        1.65 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.721 ms** | **36.299 ms** | **1.9897 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.691 ms | 20.745 ms | 1.1371 ms | 0.006 | 2763.4 KB |        1.17 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.000 ms** | **59.388 ms** | **3.2553 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.203 ms | 14.876 ms | 0.8154 ms | 0.002 | 190.99 KB |       79.37 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.870 ms** | **26.249 ms** | **1.4388 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.089 ms |  5.024 ms | 0.2754 ms | 0.002 | 186.32 KB |       44.74 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.770 ms** | **42.425 ms** | **2.3255 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.814 ms |  5.074 ms | 0.2781 ms | 0.002 | 184.72 KB |       76.77 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.061 ms** |  **5.585 ms** | **0.3061 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.388 ms | 15.361 ms | 0.8420 ms | 0.002 | 188.38 KB |       45.07 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 31.980 μs | 243.759 μs | 13.3613 μs | 24.346 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.308 μs |   3.707 μs |  0.2032 μs |  9.228 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.894 μs |   2.877 μs |  0.1577 μs | 10.839 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.673 μs |   1.369 μs |  0.0751 μs | 26.630 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.805 μs |   3.531 μs |  0.1935 μs |  9.868 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.901 μs |  28.510 μs |  1.5627 μs | 26.250 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.894 μs |   4.034 μs |  0.2211 μs | 17.974 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.824 μs |  33.360 μs |  1.8286 μs | 20.828 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.367 μs |   2.402 μs |  0.1317 μs |  4.347 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.320 μs |  23.973 μs |  1.3141 μs | 10.810 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,654.8 ns |  5,664.1 ns |   310.47 ns |  0.41 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,337.5 ns |  1,277.1 ns |    70.00 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,523.3 ns |  1,794.0 ns |    98.34 ns |  0.38 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,551.7 ns |    421.3 ns |    23.09 ns |  0.63 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    875.3 ns |  2,750.2 ns |   150.75 ns |  0.22 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,436.7 ns | 26,333.5 ns | 1,443.43 ns | 10.21 |    0.41 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,060.8 ns |  2,251.7 ns |   123.42 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,966.7 ns |  1,981.0 ns |   108.58 ns |  0.98 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.012 μs |   4.112 μs |  0.2254 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   520.992 μs | 287.059 μs | 15.7347 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.990 μs |   2.324 μs |  0.1274 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,670.272 μs | 466.836 μs | 25.5888 μs |    1280 B |


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