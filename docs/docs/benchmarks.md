---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-02 12:26 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-----------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,237.66 μs** | **157.921 μs** | **104.455 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,145.95 μs |  61.319 μs |  36.490 μs |  0.18 |    0.01 |        - |       - |   35.16 KB |        0.33 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,306.49 μs** |  **85.361 μs** |  **56.461 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,298.22 μs |  78.483 μs |  46.704 μs |  0.31 |    0.01 |  15.6250 |       - |  340.51 KB |        0.32 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,605.42 μs** | **156.854 μs** | **103.749 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,124.74 μs |  56.295 μs |  29.444 μs |  0.17 |    0.00 |        - |       - |   37.62 KB |        0.19 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,289.99 μs** | **907.643 μs** | **540.124 μs** |  **1.00** |    **0.06** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,848.57 μs | 190.731 μs |  99.756 μs |  0.48 |    0.02 |  15.6250 |       - |   375.1 KB |        0.19 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.37 μs** |   **4.257 μs** |   **2.533 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.02 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.09 μs |   7.398 μs |   4.893 μs |  0.47 |    0.04 |   0.4883 |  0.2441 |   15.02 KB |        0.36 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,327.21 μs** | **169.105 μs** | **111.853 μs** |  **1.01** |    **0.12** |  **25.3906** |       **-** |  **437.74 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    661.94 μs |  44.697 μs |  26.599 μs |  0.50 |    0.05 |   3.9063 |       - |   121.6 KB |        0.28 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |         **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    307.78 μs |  36.346 μs |  24.041 μs |     ? |       ? |   6.8359 |  5.8594 |  195.47 KB |           ? |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |         **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,101.21 μs | 426.634 μs | 223.138 μs |     ? |       ? |   7.8125 |       - |  208.21 KB |           ? |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,508.14 μs** |  **14.485 μs** |   **8.620 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,106.24 μs |   6.623 μs |   3.941 μs |  0.20 |    0.00 |        - |       - |    1.33 KB |        1.13 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,480.96 μs** |  **39.521 μs** |  **23.518 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,175.23 μs |  41.977 μs |  27.765 μs |  0.21 |    0.00 |        - |       - |     1.3 KB |        1.10 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,492.65 μs** |  **50.613 μs** |  **33.478 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,315.06 μs |  24.692 μs |  14.694 μs |  0.24 |    0.00 |        - |       - |     1.3 KB |        0.63 |
|                         |               |             |           |              |            |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,472.24 μs** |  **15.195 μs** |   **9.042 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,358.08 μs |  27.974 μs |  18.503 μs |  0.25 |    0.00 |        - |       - |     1.3 KB |        0.63 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ORZUYQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ORZUYQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.353 ms** |  **2.792 ms** | **0.4320 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.570 ms |  5.507 ms | 1.4302 ms | 0.005 |  609.14 KB |        8.16 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.804 ms** |  **1.506 ms** | **0.2330 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.151 ms |  8.075 ms | 2.0970 ms | 0.005 |  1020.7 KB |        4.08 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.282 ms** |  **3.125 ms** | **0.4835 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.905 ms | 12.220 ms | 3.1735 ms | 0.005 | 1244.65 KB |        2.07 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.643 ms** |  **5.138 ms** | **1.3344 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.401 ms |  6.635 ms | 1.7232 ms | 0.005 | 5816.42 KB |        2.46 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.549 ms** |  **4.223 ms** | **1.0968 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.788 ms |  5.095 ms | 0.7885 ms | 0.002 |  360.47 KB |      149.81 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.312 ms** |  **7.503 ms** | **1.9485 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.364 ms |  4.999 ms | 1.2981 ms | 0.002 |  810.36 KB |      194.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.622 ms** |  **4.403 ms** | **1.1435 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.415 ms |  2.298 ms | 0.5967 ms | 0.002 |  805.26 KB |      334.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.887 ms** |  **2.966 ms** | **0.7703 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.847 ms |  7.278 ms | 1.1263 ms | 0.002 | 2342.45 KB |      560.44 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 24.036 μs | 16.8203 μs | 10.0095 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 13.509 μs |  2.1686 μs |  1.4344 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 11.068 μs |  0.2598 μs |  0.1718 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 36.479 μs | 12.7432 μs |  7.5833 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 15.321 μs |  9.7615 μs |  5.8089 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 25.117 μs |  9.5348 μs |  5.6740 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.578 μs |  0.6480 μs |  0.3856 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.592 μs |  0.7084 μs |  0.4685 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; |  9.835 μs |  0.2234 μs |  0.1168 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error    | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|---------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,611.6 ns | 248.8 ns | 164.54 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,598.8 ns | 129.0 ns |  67.48 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,657.4 ns | 134.5 ns |  88.94 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,661.1 ns | 173.4 ns | 103.19 ns |  0.53 |    0.02 |     224 B |        0.48 |
| &#39;Serialize Int32&#39;                    |    850.9 ns | 247.2 ns | 163.52 ns |  0.17 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,041.3 ns | 482.4 ns | 319.06 ns |  7.02 |    0.16 |    3920 B |        8.45 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,993.4 ns | 165.2 ns | 109.27 ns |  1.00 |    0.03 |     464 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,155.6 ns | 174.3 ns |  91.14 ns |  0.63 |    0.02 |     280 B |        0.60 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.644 μs |  0.2900 μs |  0.1725 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.549 μs | 59.4749 μs | 39.3389 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.292 μs |  0.6449 μs |  0.3838 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,654.823 μs | 26.3959 μs | 17.4593 μs |    1280 B |


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