---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 18:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,077.97 μs** |   **251.22 μs** |  **13.770 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,370.72 μs | 1,954.04 μs | 107.107 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,240.53 μs** |   **827.42 μs** |  **45.353 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,308.30 μs |   266.22 μs |  14.592 μs |  0.32 |    0.00 |  15.6250 |       - |  339.44 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,630.24 μs** |   **525.86 μs** |  **28.824 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,185.90 μs |   997.09 μs |  54.654 μs |  0.18 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,845.79 μs** | **1,381.01 μs** |  **75.698 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,387.27 μs | 4,536.15 μs | 248.642 μs |  0.54 |    0.02 |  15.6250 |       - |  361.72 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.25 μs** |    **41.68 μs** |   **2.285 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.33 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     57.94 μs |   148.67 μs |   8.149 μs |  0.44 |    0.05 |        - |       - |    8.71 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,344.14 μs** |   **761.09 μs** |  **41.718 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **427.43 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    615.04 μs |   279.59 μs |  15.325 μs |  0.46 |    0.02 |        - |       - |   65.21 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    215.87 μs |   471.87 μs |  25.865 μs |     ? |       ? |   0.9766 |       - |  104.86 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,868.37 μs | 2,568.00 μs | 140.761 μs |     ? |       ? |   7.8125 |       - |  980.08 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,392.71 μs** |    **86.48 μs** |   **4.741 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,114.00 μs |    73.89 μs |   4.050 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,394.89 μs** |    **68.83 μs** |   **3.773 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,377.51 μs |   538.93 μs |  29.541 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,395.75 μs** |    **56.29 μs** |   **3.086 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,330.75 μs |   541.06 μs |  29.657 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,396.81 μs** |    **72.49 μs** |   **3.974 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,243.36 μs |   478.63 μs |  26.235 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.146 ms** | **15.2192 ms** | **0.8342 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.348 ms | 15.2154 ms | 0.8340 ms | 0.005 | 410.09 KB |        5.50 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.361 ms** | **21.8803 ms** | **1.1993 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.623 ms | 21.3222 ms | 1.1687 ms | 0.004 | 593.99 KB |        2.37 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.935 ms** | **17.2682 ms** | **0.9465 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.988 ms | 42.3216 ms | 2.3198 ms | 0.005 | 812.98 KB |        1.35 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.034 ms** | **40.8177 ms** | **2.2374 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.078 ms | 36.8425 ms | 2.0195 ms | 0.005 | 2580.3 KB |        1.09 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.301 ms** |  **0.9659 ms** | **0.0529 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.250 ms |  2.4668 ms | 0.1352 ms | 0.002 | 182.33 KB |       75.77 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,153.872 ms** | **99.6283 ms** | **5.4610 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.149 ms |  8.9741 ms | 0.4919 ms | 0.002 | 193.28 KB |       46.42 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.794 ms** | **25.2706 ms** | **1.3852 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.558 ms | 12.5234 ms | 0.6865 ms | 0.002 | 254.53 KB |      105.78 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.609 ms** | **21.6348 ms** | **1.1859 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.196 ms | 34.5537 ms | 1.8940 ms | 0.002 | 187.19 KB |       44.79 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.746 μs |  5.962 μs | 0.3268 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.515 μs |  3.586 μs | 0.1966 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.084 μs |  4.001 μs | 0.2193 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.717 μs |  2.318 μs | 0.1271 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.882 μs |  1.277 μs | 0.0700 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.472 μs |  1.004 μs | 0.0550 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.359 μs | 72.246 μs | 3.9600 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.143 μs | 23.976 μs | 1.3142 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.120 μs |  7.225 μs | 0.3960 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.640 μs |  4.983 μs | 0.2732 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,249.5 ns |   1,978.2 ns |   108.43 ns |  0.28 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,393.3 ns |   1,682.0 ns |    92.20 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,386.3 ns |   2,310.1 ns |   126.62 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,576.0 ns |   1,493.3 ns |    81.85 ns |  0.58 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    757.3 ns |     115.9 ns |     6.35 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46,504.0 ns | 109,271.4 ns | 5,989.53 ns | 10.38 |    1.59 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,524.7 ns |  10,586.1 ns |   580.26 ns |  1.01 |    0.15 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,957.5 ns |   3,791.9 ns |   207.85 ns |  0.88 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.96 μs |   2.965 μs |  0.163 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   533.46 μs | 540.125 μs | 29.606 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.09 μs |   1.316 μs |  0.072 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,679.52 μs | 329.213 μs | 18.045 μs |    1280 B |


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