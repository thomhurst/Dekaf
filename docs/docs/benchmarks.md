---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 06:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,223.81 μs** |   **504.40 μs** |  **27.648 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,388.47 μs |   891.60 μs |  48.872 μs |  0.22 |    0.01 |       - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,376.44 μs** |   **847.15 μs** |  **46.435 μs** |  **1.00** |    **0.01** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,327.57 μs |   550.68 μs |  30.185 μs |  0.32 |    0.00 | 15.6250 |       - |  339.88 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,153.94 μs** | **1,129.85 μs** |  **61.931 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,279.01 μs | 1,648.60 μs |  90.365 μs |  0.21 |    0.01 |       - |       - |   36.89 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,816.60 μs** | **4,741.33 μs** | **259.888 μs** |  **1.00** |    **0.02** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,883.86 μs | 7,697.00 μs | 421.899 μs |  0.54 |    0.03 | 15.6250 |       - |  369.56 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.20 μs** |   **180.27 μs** |   **9.881 μs** |  **1.00** |    **0.10** |  **2.4414** |       **-** |   **41.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.47 μs |    86.35 μs |   4.733 μs |  0.49 |    0.05 |  0.6104 |  0.4883 |   16.89 KB |        0.41 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,481.97 μs** | **1,509.32 μs** |  **82.731 μs** |  **1.00** |    **0.07** | **25.3906** |       **-** |  **425.69 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    738.96 μs |   613.86 μs |  33.648 μs |  0.50 |    0.03 |  3.9063 |       - |  102.61 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    311.60 μs |   393.36 μs |  21.561 μs |     ? |       ? |  6.8359 |  5.8594 |  199.67 KB |           ? |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,206.55 μs |   825.33 μs |  45.239 μs |     ? |       ? | 70.3125 | 62.5000 | 1948.48 KB |           ? |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,430.33 μs** |   **119.33 μs** |   **6.541 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,120.23 μs |    24.74 μs |   1.356 μs |  0.21 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,433.22 μs** |   **122.31 μs** |   **6.704 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,149.45 μs |   123.88 μs |   6.790 μs |  0.21 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,440.64 μs** |    **11.29 μs** |   **0.619 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,254.12 μs |   310.74 μs |  17.033 μs |  0.23 |    0.00 |       - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,437.37 μs** |   **286.21 μs** |  **15.688 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,329.81 μs |   416.17 μs |  22.812 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.232 ms** |   **7.063 ms** | **0.3872 ms** | **3,169.334 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.031 ms |  28.775 ms | 1.5773 ms |    13.553 ms | 0.004 |   410.8 KB |        5.51 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.961 ms** |  **21.957 ms** | **1.2035 ms** | **3,166.130 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.726 ms |  29.552 ms | 1.6198 ms |    14.242 ms | 0.005 |  588.27 KB |        2.35 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.519 ms** |  **23.970 ms** | **1.3139 ms** | **3,166.813 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.385 ms | 182.327 ms | 9.9939 ms |    16.123 ms | 0.006 |  878.97 KB |        1.46 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.957 ms** |   **7.148 ms** | **0.3918 ms** | **3,165.957 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.622 ms |  15.123 ms | 0.8289 ms |    14.475 ms | 0.005 | 2575.31 KB |        1.09 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.989 ms** |  **11.366 ms** | **0.6230 ms** | **3,155.278 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.273 ms |  12.848 ms | 0.7043 ms |     6.238 ms | 0.002 |  185.88 KB |       77.25 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.012 ms** |  **21.027 ms** | **1.1526 ms** | **3,157.105 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.105 ms |  11.981 ms | 0.6567 ms |     6.461 ms | 0.002 |  183.74 KB |       44.13 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.856 ms** |  **32.152 ms** | **1.7624 ms** | **3,158.076 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.125 ms |  35.899 ms | 1.9678 ms |     6.378 ms | 0.002 |  181.38 KB |       75.38 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.152 ms** |  **33.310 ms** | **1.8258 ms** | **3,156.243 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.371 ms |  33.346 ms | 1.8278 ms |     7.129 ms | 0.002 |  183.84 KB |       43.99 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error    | StdDev    | Allocated |
|------------------------------------------ |----------:|---------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.508 μs | 3.777 μs | 0.2071 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.231 μs | 1.827 μs | 0.1002 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 12.554 μs | 6.489 μs | 0.3557 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.740 μs | 3.831 μs | 0.2100 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.894 μs | 2.608 μs | 0.1429 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.285 μs | 2.394 μs | 0.1312 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.134 μs | 7.596 μs | 0.4164 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.532 μs | 2.429 μs | 0.1332 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.607 μs | 1.173 μs | 0.0643 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,681.2 ns |  3,138.3 ns | 172.02 ns |  0.36 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,526.2 ns |  5,734.2 ns | 314.31 ns |  0.33 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,477.8 ns |  4,593.3 ns | 251.77 ns |  0.32 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,601.7 ns |  1,799.9 ns |  98.66 ns |  0.56 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    735.3 ns |  1,069.0 ns |  58.59 ns |  0.16 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,120.0 ns |    735.9 ns |  40.34 ns |  8.42 |    0.89 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,695.3 ns | 11,196.4 ns | 613.71 ns |  1.01 |    0.16 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,977.3 ns |  3,654.7 ns | 200.33 ns |  0.86 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev    | Allocated |
|------------------------ |------------:|-------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.17 μs |     4.804 μs |  0.263 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   574.76 μs | 1,168.503 μs | 64.050 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.51 μs |     3.030 μs |  0.166 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,684.64 μs |   339.311 μs | 18.599 μs |    1280 B |


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