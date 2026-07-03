---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 16:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,265.62 μs** |    **718.00 μs** |    **39.356 μs** |  **1.00** |    **0.01** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,339.12 μs |  1,684.47 μs |    92.332 μs |  0.21 |    0.01 |        - |        - |   35.03 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,495.35 μs** |    **770.35 μs** |    **42.225 μs** |  **1.00** |    **0.01** |  **62.5000** |  **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,384.41 μs |    280.38 μs |    15.369 μs |  0.32 |    0.00 |  15.6250 |        - |  340.03 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,133.45 μs** |    **697.18 μs** |    **38.215 μs** |  **1.00** |    **0.01** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,324.00 μs |  2,558.08 μs |   140.217 μs |  0.22 |    0.02 |        - |        - |   37.26 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,028.51 μs** |  **1,984.73 μs** |   **108.790 μs** |  **1.00** |    **0.01** | **109.3750** |  **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,997.42 μs |  8,340.22 μs |   457.156 μs |  0.54 |    0.03 |  15.6250 |        - |  372.23 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.79 μs** |    **166.61 μs** |     **9.132 μs** |  **1.00** |    **0.08** |   **2.4414** |        **-** |   **42.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     71.52 μs |    141.64 μs |     7.764 μs |  0.50 |    0.05 |   0.4883 |        - |   15.41 KB |        0.36 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,464.53 μs** |    **161.48 μs** |     **8.851 μs** |  **1.00** |    **0.01** |  **25.3906** |        **-** |  **421.53 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    810.36 μs |  2,931.97 μs |   160.711 μs |  0.55 |    0.10 |  11.7188 |   7.8125 |  245.18 KB |        0.58 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    357.82 μs |    617.46 μs |    33.845 μs |     ? |       ? |  12.6953 |  11.7188 |  270.38 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,489.99 μs |  2,171.61 μs |   119.033 μs |     ? |       ? | 117.1875 | 109.3750 | 2518.29 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,462.85 μs** |    **462.64 μs** |    **25.359 μs** |  **1.00** |    **0.01** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,383.91 μs |    253.01 μs |    13.868 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,478.17 μs** |    **270.79 μs** |    **14.843 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,406.90 μs |    555.15 μs |    30.430 μs |  0.26 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **6,140.25 μs** | **21,062.27 μs** | **1,154.494 μs** |  **1.02** |    **0.23** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,381.47 μs |    224.50 μs |    12.306 μs |  0.23 |    0.03 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |              |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,464.53 μs** |    **178.11 μs** |     **9.763 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,392.09 μs |    820.80 μs |    44.991 μs |  0.25 |    0.01 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.963 ms** | **12.533 ms** | **0.6870 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.447 ms | 22.071 ms | 1.2098 ms | 0.006 |   416.8 KB |        5.59 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.701 ms** | **20.206 ms** | **1.1075 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.475 ms | 20.822 ms | 1.1413 ms | 0.005 |  590.33 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.129 ms** | **13.733 ms** | **0.7527 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.452 ms | 36.874 ms | 2.0212 ms | 0.005 |  808.73 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.171 ms** | **63.146 ms** | **3.4612 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.054 ms | 21.068 ms | 1.1548 ms | 0.005 | 2574.73 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.790 ms** | **54.735 ms** | **3.0002 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.559 ms |  3.475 ms | 0.1905 ms | 0.002 |  185.73 KB |       77.19 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.385 ms** |  **5.720 ms** | **0.3135 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.891 ms |  7.038 ms | 0.3858 ms | 0.002 |  184.63 KB |       44.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.766 ms** | **61.393 ms** | **3.3652 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.004 ms |  5.491 ms | 0.3010 ms | 0.002 |  191.86 KB |       79.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.004 ms** | **20.537 ms** | **1.1257 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.595 ms | 39.765 ms | 2.1796 ms | 0.002 |   183.8 KB |       43.98 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.095 μs |   6.195 μs | 0.3396 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.514 μs |  26.812 μs | 1.4697 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.440 μs |  15.558 μs | 0.8528 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.281 μs | 172.089 μs | 9.4328 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.928 μs |   4.512 μs | 0.2473 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.240 μs |   2.497 μs | 0.1368 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.449 μs |  25.975 μs | 1.4238 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.145 μs |  13.205 μs | 0.7238 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.380 μs |  12.116 μs | 0.6641 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.535 μs |   6.472 μs | 0.3547 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,405.7 ns |  2,084.9 ns | 114.3 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,298.7 ns |  2,106.6 ns | 115.5 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,934.3 ns |  7,423.2 ns | 406.9 ns |  0.46 |    0.08 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,066.3 ns | 14,726.9 ns | 807.2 ns |  0.72 |    0.17 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    925.7 ns |  4,321.1 ns | 236.9 ns |  0.22 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,737.0 ns |  4,423.9 ns | 242.5 ns |  9.61 |    0.25 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,241.7 ns |  2,272.1 ns | 124.5 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,842.3 ns | 11,093.2 ns | 608.1 ns |  1.14 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Allocated |
|------------------------ |------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.58 μs |  22.20 μs |  1.217 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   525.60 μs | 446.12 μs | 24.453 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.88 μs |  11.43 μs |  0.626 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,702.85 μs | 455.65 μs | 24.976 μs |    1280 B |


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