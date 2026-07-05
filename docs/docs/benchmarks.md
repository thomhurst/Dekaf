---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 22:41 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,608.89 μs** |    **246.06 μs** |    **13.488 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,106.66 μs |    139.35 μs |     7.638 μs |  0.20 |    0.00 |   1.9531 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,657.83 μs** |    **888.13 μs** |    **48.681 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 1,302.65 μs |  3,300.13 μs |   180.891 μs |  0.20 |    0.02 |  19.5313 |  3.9063 |   339.1 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **5,948.78 μs** |    **644.40 μs** |    **35.321 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,382.68 μs |    772.62 μs |    42.350 μs |  0.23 |    0.01 |   1.9531 |       - |   36.33 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,728.73 μs** | **14,596.76 μs** |   **800.098 μs** |  **1.01** |    **0.13** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 3,387.49 μs |  9,909.50 μs |   543.173 μs |  0.44 |    0.07 |  15.6250 |       - |  361.72 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **66.97 μs** |     **92.07 μs** |     **5.047 μs** |  **1.00** |    **0.09** |   **2.4414** |       **-** |   **41.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    28.23 μs |     68.90 μs |     3.777 μs |  0.42 |    0.06 |   0.2441 |       - |   10.37 KB |        0.25 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **608.75 μs** |     **51.78 μs** |     **2.838 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **419.13 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   446.46 μs |    587.07 μs |    32.179 μs |  0.73 |    0.05 |   1.9531 |       - |  148.52 KB |        0.35 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   142.94 μs |    615.72 μs |    33.750 μs |     ? |       ? |   0.4883 |       - |    12.5 KB |           ? |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 3,683.54 μs | 27,074.15 μs | 1,484.026 μs |     ? |       ? |   3.9063 |       - |  104.01 KB |           ? |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,240.88 μs** |    **551.21 μs** |    **30.214 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,215.41 μs |    108.85 μs |     5.966 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,417.96 μs** |  **5,474.84 μs** |   **300.095 μs** |  **1.00** |    **0.07** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,415.85 μs |  4,104.97 μs |   225.007 μs |  0.26 |    0.04 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,246.81 μs** |    **544.26 μs** |    **29.833 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,136.46 μs |  1,154.05 μs |    63.257 μs |  0.22 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,230.05 μs** |    **112.20 μs** |     **6.150 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,227.44 μs |    107.79 μs |     5.909 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,163.929 ms** | **14.657 ms** | **0.8034 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    12.115 ms | 35.653 ms | 1.9542 ms | 0.004 |  596.53 KB |        7.99 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,162.510 ms** | **23.641 ms** | **1.2959 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    10.905 ms | 12.287 ms | 0.6735 ms | 0.003 |  779.23 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,163.013 ms** |  **7.001 ms** | **0.3837 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    12.800 ms | 40.999 ms | 2.2473 ms | 0.004 | 1069.84 KB |        1.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163.723 ms** | **13.741 ms** | **0.7532 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    12.891 ms | 61.768 ms | 3.3857 ms | 0.004 | 2841.34 KB |        1.20 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.628 ms** |  **2.355 ms** | **0.1291 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     4.857 ms | 14.107 ms | 0.7732 ms | 0.002 |  204.07 KB |       84.81 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.468 ms** | **65.582 ms** | **3.5948 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.969 ms |  3.212 ms | 0.1761 ms | 0.002 |  186.61 KB |       44.81 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.438 ms** | **73.003 ms** | **4.0016 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     5.029 ms |  4.716 ms | 0.2585 ms | 0.002 |  184.88 KB |       76.83 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.287 ms** | **12.966 ms** | **0.7107 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.976 ms | 36.553 ms | 2.0036 ms | 0.002 |   187.2 KB |       44.79 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 38.167 μs |  41.506 μs | 2.2751 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.775 μs |  25.733 μs | 1.4105 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.174 μs |  18.137 μs | 0.9942 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.771 μs |  15.761 μs | 0.8639 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 15.893 μs |  15.528 μs | 0.8511 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.756 μs |  13.980 μs | 0.7663 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 24.440 μs |  95.482 μs | 5.2337 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.541 μs | 119.181 μs | 6.5327 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.872 μs |  35.395 μs | 1.9401 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.348 μs |  55.508 μs | 3.0426 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error     | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|----------:|----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  3.195 μs |  8.986 μs | 0.4925 μs |  3.0695 μs |  1.08 |    0.40 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.391 μs | 10.514 μs | 0.5763 μs |  1.1855 μs |  0.47 |    0.24 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.313 μs | 14.410 μs | 0.7899 μs |  0.9630 μs |  0.44 |    0.29 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.647 μs | 17.600 μs | 0.9647 μs |  2.3545 μs |  0.89 |    0.43 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.437 μs | 30.581 μs | 1.6762 μs |  0.5205 μs |  0.49 |    0.54 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 31.916 μs | 35.445 μs | 1.9429 μs | 31.4415 μs | 10.79 |    3.74 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3.390 μs | 30.193 μs | 1.6550 μs |  2.6495 μs |  1.15 |    0.64 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.481 μs | 28.486 μs | 1.5614 μs |  3.9045 μs |  1.51 |    0.71 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    16.628 μs |  30.926 μs |  1.695 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   484.206 μs | 168.977 μs |  9.262 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.982 μs |  26.945 μs |  1.477 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,205.874 μs | 240.138 μs | 13.163 μs |    1280 B |


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