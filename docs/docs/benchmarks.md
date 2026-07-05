---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 15:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,289.88 μs** | **1,648.07 μs** |  **90.336 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,406.37 μs | 2,408.87 μs | 132.038 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,485.45 μs** | **1,984.98 μs** | **108.803 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,340.29 μs |   426.72 μs |  23.390 μs |  0.31 |    0.00 |  15.6250 |       - |  339.42 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,151.18 μs** |   **816.57 μs** |  **44.759 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,518.19 μs | 1,817.20 μs |  99.607 μs |  0.25 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,104.61 μs** | **1,884.92 μs** | **103.319 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,740.83 μs | 6,101.45 μs | 334.441 μs |  0.51 |    0.02 |  15.6250 |       - |  361.97 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.79 μs** |   **242.01 μs** |  **13.265 μs** |  **1.01** |    **0.12** |   **2.1973** |       **-** |   **39.31 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     84.48 μs |   130.21 μs |   7.137 μs |  0.60 |    0.07 |        - |       - |    9.62 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,439.47 μs** |   **427.74 μs** |  **23.446 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |  **421.78 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    656.00 μs |   701.25 μs |  38.438 μs |  0.46 |    0.02 |        - |       - |   91.33 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    250.59 μs |   740.46 μs |  40.587 μs |     ? |       ? |   0.9766 |       - |  100.25 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,164.03 μs | 4,980.75 μs | 273.012 μs |     ? |       ? |   7.8125 |       - | 1020.58 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,516.25 μs** |   **141.18 μs** |   **7.739 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,133.72 μs |   376.92 μs |  20.660 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,617.56 μs** | **1,724.80 μs** |  **94.542 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,130.22 μs |   200.59 μs |  10.995 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,434.44 μs** |    **22.19 μs** |   **1.216 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,118.94 μs |    32.25 μs |   1.768 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,442.01 μs** |   **251.64 μs** |  **13.793 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,125.26 μs |    57.38 μs |   3.145 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.818 ms** | **13.812 ms** | **0.7571 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.095 ms | 21.716 ms | 1.1903 ms | 0.006 | 594.95 KB |        7.97 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.725 ms** | **24.275 ms** | **1.3306 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.520 ms | 10.193 ms | 0.5587 ms | 0.005 | 780.57 KB |        3.12 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.303 ms** | **28.377 ms** | **1.5554 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.682 ms | 47.873 ms | 2.6241 ms | 0.006 | 997.13 KB |        1.66 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.478 ms** | **10.121 ms** | **0.5548 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.057 ms | 22.335 ms | 1.2243 ms | 0.006 |   2764 KB |        1.17 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.398 ms** | **24.888 ms** | **1.3642 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     8.618 ms | 38.097 ms | 2.0882 ms | 0.003 | 193.64 KB |       80.47 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.296 ms** |  **9.523 ms** | **0.5220 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.154 ms |  5.693 ms | 0.3121 ms | 0.002 | 186.41 KB |       44.77 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,158.002 ms** | **24.365 ms** | **1.3355 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.808 ms | 17.102 ms | 0.9374 ms | 0.002 | 256.68 KB |      106.67 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.099 ms** | **16.308 ms** | **0.8939 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.712 ms | 26.317 ms | 1.4425 ms | 0.002 | 188.44 KB |       45.08 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.096 μs |  4.336 μs | 0.2377 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.199 μs |  2.410 μs | 0.1321 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.412 μs |  5.255 μs | 0.2881 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.923 μs |  3.516 μs | 0.1927 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.043 μs |  3.702 μs | 0.2029 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.412 μs |  1.734 μs | 0.0950 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.314 μs |  9.155 μs | 0.5018 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.110 μs | 18.658 μs | 1.0227 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.932 μs |  4.058 μs | 0.2224 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.655 μs |  3.690 μs | 0.2022 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,666.2 ns |  7,515.9 ns | 411.97 ns |  0.35 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,351.7 ns |  1,283.6 ns |  70.36 ns |  0.28 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,530.0 ns |  5,429.4 ns | 297.60 ns |  0.32 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,705.7 ns |  3,388.1 ns | 185.72 ns |  0.56 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    728.7 ns |    640.7 ns |  35.12 ns |  0.15 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,522.5 ns | 17,245.1 ns | 945.26 ns |  8.44 |    0.19 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,799.5 ns |    912.2 ns |  50.00 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,291.0 ns | 18,014.8 ns | 987.45 ns |  0.89 |    0.18 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.185 μs |  19.823 μs |  1.0866 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   509.209 μs |  54.829 μs |  3.0054 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.510 μs |   4.224 μs |  0.2315 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,680.001 μs | 450.192 μs | 24.6765 μs |    1280 B |


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