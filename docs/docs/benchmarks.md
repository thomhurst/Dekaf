---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 08:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error         | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|--------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,598.29 μs** |    **348.300 μs** |    **19.092 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,226.28 μs |  1,023.776 μs |    56.117 μs |  0.22 |    0.01 |   1.9531 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,530.40 μs** |    **175.459 μs** |     **9.617 μs** |  **1.00** |    **0.00** |  **62.5000** | **23.4375** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 1,147.02 μs |    413.563 μs |    22.669 μs |  0.18 |    0.00 |  19.5313 |  3.9063 |  339.23 KB |        0.32 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **5,896.77 μs** |    **694.555 μs** |    **38.071 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,289.43 μs |  3,110.735 μs |   170.510 μs |  0.22 |    0.03 |   1.9531 |       - |   36.29 KB |        0.19 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **6,830.64 μs** |    **212.599 μs** |    **11.653 μs** |  **1.00** |    **0.00** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,062.81 μs | 12,957.621 μs |   710.251 μs |  0.59 |    0.09 |  19.5313 |  3.9063 |  361.59 KB |        0.19 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |          **NA** |            **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    24.13 μs |      7.264 μs |     0.398 μs |     ? |       ? |   0.2441 |       - |    9.16 KB |           ? |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **697.94 μs** |    **561.330 μs** |    **30.768 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |  **415.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   326.39 μs |     94.239 μs |     5.166 μs |  0.47 |    0.02 |   1.9531 |       - |   92.18 KB |        0.22 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |            **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   540.27 μs |  1,933.208 μs |   105.966 μs |     ? |       ? |   0.7324 |       - |   14.71 KB |           ? |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |            **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 4,934.48 μs | 61,759.026 μs | 3,385.221 μs |     ? |       ? |   3.9063 |       - |  121.33 KB |           ? |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,211.65 μs** |     **38.519 μs** |     **2.111 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.36 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,185.00 μs |    258.242 μs |    14.155 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.84 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,213.87 μs** |     **69.056 μs** |     **3.785 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,095.90 μs |     67.570 μs |     3.704 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,221.38 μs** |     **64.501 μs** |     **3.536 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,229.25 μs |    158.888 μs |     8.709 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |               |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,237.73 μs** |    **766.442 μs** |    **42.011 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,218.03 μs |    213.008 μs |    11.676 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error        | StdDev      | Median       | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-------------:|------------:|-------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,162.052 ms** |    **25.740 ms** |   **1.4109 ms** | **3,162.186 ms** | **1.000** |    **0.00** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    11.935 ms |    38.300 ms |   2.0994 ms |    11.791 ms | 0.004 |    0.00 |  593.97 KB |        7.96 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,159.848 ms** |     **9.254 ms** |   **0.5072 ms** | **3,159.641 ms** | **1.000** |    **0.00** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    10.100 ms |    20.391 ms |   1.1177 ms |    10.495 ms | 0.003 |    0.00 |  780.93 KB |        3.12 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,437.250 ms** | **8,752.281 ms** | **479.7421 ms** | **3,160.834 ms** | **1.012** |    **0.17** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |     9.667 ms |    27.963 ms |   1.5327 ms |    10.096 ms | 0.003 |    0.00 |   993.2 KB |        1.65 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,159.505 ms** |    **14.940 ms** |   **0.8189 ms** | **3,159.356 ms** | **1.000** |    **0.00** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    11.592 ms |    43.010 ms |   2.3575 ms |    11.251 ms | 0.004 |    0.00 | 2840.09 KB |        1.20 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,153.236 ms** |    **36.041 ms** |   **1.9755 ms** | **3,152.984 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.293 ms |    41.695 ms |   2.2855 ms |     4.013 ms | 0.002 |    0.00 |   188.9 KB |       78.50 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,153.701 ms** |    **13.274 ms** |   **0.7276 ms** | **3,153.660 ms** | **1.000** |    **0.00** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.158 ms |    48.428 ms |   2.6545 ms |     3.756 ms | 0.002 |    0.00 |  186.32 KB |       44.74 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,154.080 ms** |    **15.159 ms** |   **0.8309 ms** | **3,154.098 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     4.297 ms |     5.286 ms |   0.2897 ms |     4.427 ms | 0.001 |    0.00 |   256.9 KB |      106.76 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,153.206 ms** |    **19.321 ms** |   **1.0591 ms** | **3,153.418 ms** | **1.000** |    **0.00** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.206 ms |    28.773 ms |   1.5771 ms |     5.021 ms | 0.002 |    0.00 |  188.34 KB |       45.06 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.964 μs | 3.1125 μs | 0.1706 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.194 μs | 4.1800 μs | 0.2291 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.199 μs | 3.2173 μs | 0.1764 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.741 μs | 2.4819 μs | 0.1360 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.000 μs | 0.5320 μs | 0.0292 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.363 μs | 2.1145 μs | 0.1159 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.181 μs | 8.1410 μs | 0.4462 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.416 μs | 8.8521 μs | 0.4852 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.558 μs | 1.5673 μs | 0.0859 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.462 μs | 6.2811 μs | 0.3443 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,662.7 ns |  4,494.0 ns | 246.33 ns |  0.40 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,352.5 ns |  2,104.0 ns | 115.33 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,415.8 ns |    557.4 ns |  30.55 ns |  0.34 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,565.3 ns |    667.9 ns |  36.61 ns |  0.61 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    784.0 ns |    938.6 ns |  51.45 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 38,682.5 ns | 13,814.9 ns | 757.24 ns |  9.25 |    0.20 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,181.7 ns |  1,163.9 ns |  63.80 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,001.0 ns |  1,906.2 ns | 104.48 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.13 μs |   1.326 μs |  0.073 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   545.30 μs | 518.602 μs | 28.426 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.45 μs |   8.452 μs |  0.463 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,858.77 μs | 680.339 μs | 37.292 μs |    1280 B |


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