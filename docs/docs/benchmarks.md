---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 14:54 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Median       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,133.22 μs** |   **699.80 μs** |  **38.359 μs** |  **6,119.75 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,443.38 μs |   834.02 μs |  45.715 μs |  1,468.40 μs |  0.24 |    0.01 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,392.89 μs** |   **835.69 μs** |  **45.807 μs** |  **7,409.10 μs** |  **1.00** |    **0.01** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,327.98 μs |   995.40 μs |  54.561 μs |  2,358.33 μs |  0.31 |    0.01 | 15.6250 |       - |  339.62 KB |        0.32 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,267.70 μs** |   **150.95 μs** |   **8.274 μs** |  **6,264.07 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,375.30 μs | 1,488.94 μs |  81.614 μs |  1,340.96 μs |  0.22 |    0.01 |       - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,767.78 μs** | **4,696.34 μs** | **257.422 μs** | **12,776.58 μs** |  **1.00** |    **0.02** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,017.96 μs |   140.60 μs |   7.707 μs |  6,021.14 μs |  0.47 |    0.01 | 15.6250 |       - |  361.83 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.61 μs** |    **84.82 μs** |   **4.649 μs** |    **139.49 μs** |  **1.00** |    **0.04** |  **2.4414** |       **-** |   **41.87 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     50.37 μs |    61.28 μs |   3.359 μs |     49.83 μs |  0.36 |    0.02 |       - |       - |    8.89 KB |        0.21 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,400.17 μs** |   **295.82 μs** |  **16.215 μs** |  **1,396.48 μs** |  **1.00** |    **0.01** | **25.3906** |       **-** |   **416.4 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    644.33 μs |   631.47 μs |  34.613 μs |    636.36 μs |  0.46 |    0.02 |       - |       - |   58.51 KB |        0.14 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    290.47 μs | 2,624.76 μs | 143.872 μs |    210.33 μs |     ? |       ? |  0.4883 |       - |   11.85 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,130.91 μs | 1,439.02 μs |  78.877 μs |  2,121.83 μs |     ? |       ? |  7.8125 |       - | 1014.36 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,420.36 μs** |    **59.91 μs** |   **3.284 μs** |  **5,419.95 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,392.33 μs |   240.11 μs |  13.161 μs |  1,396.57 μs |  0.26 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,420.20 μs** |    **25.42 μs** |   **1.394 μs** |  **5,420.46 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,342.41 μs |   592.37 μs |  32.470 μs |  1,355.61 μs |  0.25 |    0.01 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,424.73 μs** |   **102.52 μs** |   **5.620 μs** |  **5,425.74 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,123.02 μs |   101.60 μs |   5.569 μs |  1,120.10 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,429.84 μs** |    **90.36 μs** |   **4.953 μs** |  **5,430.99 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,146.14 μs |   175.33 μs |   9.610 μs |  1,146.53 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.846 ms** | **16.478 ms** | **0.9032 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.628 ms | 42.446 ms | 2.3266 ms | 0.006 |  592.08 KB |        7.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.852 ms** | **66.000 ms** | **3.6177 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.856 ms | 17.290 ms | 0.9477 ms | 0.004 |  783.63 KB |        3.13 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.000 ms** | **32.986 ms** | **1.8081 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.111 ms | 26.746 ms | 1.4660 ms | 0.005 |  994.38 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.932 ms** | **13.171 ms** | **0.7220 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.541 ms | 42.918 ms | 2.3525 ms | 0.005 | 2762.77 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.422 ms** | **32.631 ms** | **1.7886 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.663 ms |  6.064 ms | 0.3324 ms | 0.002 |  184.48 KB |       76.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.889 ms** | **29.335 ms** | **1.6080 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.772 ms | 12.317 ms | 0.6751 ms | 0.002 |  186.41 KB |       44.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,158.113 ms** | **15.015 ms** | **0.8230 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.354 ms |  2.962 ms | 0.1623 ms | 0.002 |  256.65 KB |      106.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.853 ms** | **29.185 ms** | **1.5997 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.082 ms |  3.958 ms | 0.2169 ms | 0.002 |  191.13 KB |       45.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.928 μs |  4.243 μs | 0.2326 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.106 μs |  3.220 μs | 0.1765 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.556 μs |  5.280 μs | 0.2894 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.894 μs |  4.197 μs | 0.2301 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.990 μs |  2.793 μs | 0.1531 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.309 μs |  1.850 μs | 0.1014 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.674 μs | 42.652 μs | 2.3379 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.907 μs | 24.654 μs | 1.3514 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.354 μs | 11.977 μs | 0.6565 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.836 μs |  3.030 μs | 0.1661 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,427.5 ns |  4,319.6 ns | 236.8 ns |  0.35 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,432.5 ns |  2,385.7 ns | 130.8 ns |  0.35 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,656.7 ns |  3,931.9 ns | 215.5 ns |  0.41 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,718.3 ns |  4,458.8 ns | 244.4 ns |  0.67 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    745.0 ns |  2,605.5 ns | 142.8 ns |  0.18 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,306.3 ns |  7,187.7 ns | 394.0 ns |  9.89 |    0.40 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,082.8 ns |  3,476.4 ns | 190.6 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,559.3 ns | 15,557.5 ns | 852.8 ns |  1.12 |    0.19 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev     | Allocated |
|------------------------ |------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.00 μs |     3.205 μs |   0.176 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   536.98 μs |   153.915 μs |   8.437 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.82 μs |    31.981 μs |   1.753 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,850.71 μs | 3,228.802 μs | 176.982 μs |    1280 B |


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