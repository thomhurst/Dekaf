---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 04:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,028.98 μs** |    **237.18 μs** |    **13.001 μs** | **6,025.02 μs** |  **1.00** |    **0.00** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,255.94 μs |    149.24 μs |     8.180 μs | 1,253.58 μs |  0.21 |    0.00 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,350.67 μs** |    **626.80 μs** |    **34.357 μs** | **7,332.63 μs** |  **1.00** |    **0.01** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,182.37 μs |    373.15 μs |    20.454 μs | 2,175.97 μs |  0.30 |    0.00 |  3.9063 |  339.42 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,373.36 μs** |    **510.45 μs** |    **27.980 μs** | **6,362.84 μs** |  **1.00** |    **0.01** |       **-** |  **194.03 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,156.35 μs |    678.13 μs |    37.171 μs | 1,164.63 μs |  0.18 |    0.01 |       - |   36.32 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,983.39 μs** |  **1,237.97 μs** |    **67.857 μs** | **8,013.28 μs** |  **1.00** |    **0.01** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 3,947.49 μs | 11,537.88 μs |   632.430 μs | 3,904.99 μs |  0.49 |    0.07 |       - |  361.56 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **129.79 μs** |     **32.75 μs** |     **1.795 μs** |   **130.74 μs** |  **1.00** |    **0.02** |  **0.4883** |   **42.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    44.59 μs |    122.08 μs |     6.691 μs |    44.80 μs |  0.34 |    0.04 |       - |   10.61 KB |        0.25 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,304.84 μs** |  **1,495.27 μs** |    **81.961 μs** | **1,321.56 μs** |  **1.00** |    **0.08** |  **3.9063** |  **421.69 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   476.32 μs |    394.84 μs |    21.643 μs |   486.37 μs |  0.37 |    0.02 |       - |    42.3 KB |        0.10 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   210.38 μs |  2,775.75 μs |   152.148 μs |   130.53 μs |     ? |       ? |       - |   11.57 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 3,584.67 μs | 61,647.56 μs | 3,379.111 μs | 1,871.47 μs |     ? |       ? |       - |  102.94 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,319.55 μs** |    **171.18 μs** |     **9.383 μs** | **5,321.29 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,248.69 μs |    386.25 μs |    21.172 μs | 1,257.43 μs |  0.23 |    0.00 |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,320.78 μs** |     **67.16 μs** |     **3.681 μs** | **5,321.20 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,236.86 μs |    243.93 μs |    13.371 μs | 1,231.04 μs |  0.23 |    0.00 |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,354.22 μs** |    **940.82 μs** |    **51.570 μs** | **5,328.86 μs** |  **1.00** |    **0.01** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,319.61 μs |  1,083.94 μs |    59.414 μs | 1,321.26 μs |  0.25 |    0.01 |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,322.94 μs** |     **71.96 μs** |     **3.944 μs** | **5,322.99 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,276.62 μs |    195.54 μs |    10.718 μs | 1,273.56 μs |  0.24 |    0.00 |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171.836 ms** | **10.712 ms** | **0.5871 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.735 ms | 24.396 ms | 1.3372 ms | 0.006 |  594.33 KB |        7.97 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.710 ms** | **26.595 ms** | **1.4578 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.242 ms | 38.170 ms | 2.0922 ms | 0.005 |  778.77 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.714 ms** | **12.668 ms** | **0.6944 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.977 ms | 12.877 ms | 0.7058 ms | 0.005 | 1034.41 KB |        1.72 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.269 ms** |  **8.924 ms** | **0.4891 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.351 ms | 86.381 ms | 4.7348 ms | 0.006 | 2764.34 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.294 ms** | **15.531 ms** | **0.8513 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.203 ms |  4.312 ms | 0.2363 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.417 ms** | **11.807 ms** | **0.6472 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.257 ms |  8.213 ms | 0.4502 ms | 0.002 |  186.32 KB |       44.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.088 ms** | **17.827 ms** | **0.9771 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.730 ms | 44.119 ms | 2.4183 ms | 0.003 |  240.96 KB |      100.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.244 ms** | **22.016 ms** | **1.2068 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.629 ms |  4.189 ms | 0.2296 ms | 0.002 |  189.34 KB |       45.30 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.632 μs |   1.1083 μs |  0.0607 μs | 14.638 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.430 μs |  33.4451 μs |  1.8332 μs |  9.468 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.155 μs |   3.4993 μs |  0.1918 μs | 10.099 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.810 μs |   0.8057 μs |  0.0442 μs | 26.830 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.636 μs |  73.6022 μs |  4.0344 μs | 15.931 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.091 μs | 182.6243 μs | 10.0103 μs | 19.406 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.891 μs |   5.6468 μs |  0.3095 μs | 17.754 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.351 μs |   6.5776 μs |  0.3605 μs | 20.357 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.441 μs |   2.1661 μs |  0.1187 μs |  4.377 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 18.759 μs | 246.7386 μs | 13.5246 μs | 11.091 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,377.2 ns |     912.2 ns |     50.00 ns |  1,376.5 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,335.3 ns |     862.2 ns |     47.26 ns |  1,352.0 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,567.0 ns |   2,803.1 ns |    153.65 ns |  1,624.0 ns |  0.37 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,574.3 ns |   1,749.0 ns |     95.87 ns |  2,584.0 ns |  0.61 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    835.3 ns |   2,048.2 ns |    112.27 ns |    791.0 ns |  0.20 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,403.0 ns |  14,276.5 ns |    782.54 ns | 39,724.0 ns |  9.40 |    0.16 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,193.7 ns |     278.7 ns |     15.28 ns |  4,197.0 ns |  1.00 |    0.00 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | 11,210.0 ns | 218,437.9 ns | 11,973.32 ns |  4,748.0 ns |  2.67 |    2.47 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.299 μs |  25.338 μs |  1.3889 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   528.096 μs | 380.031 μs | 20.8308 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.249 μs |   6.236 μs |  0.3418 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,638.194 μs | 502.735 μs | 27.5566 μs |    1280 B |


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