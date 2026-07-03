---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 08:56 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,240.29 μs** | **1,300.33 μs** |  **71.276 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,444.91 μs | 2,431.94 μs | 133.303 μs |  0.23 |    0.02 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,294.65 μs** | **1,560.42 μs** |  **85.532 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,358.49 μs |   600.36 μs |  32.908 μs |  0.32 |    0.01 |  15.6250 |       - |  339.93 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,515.48 μs** |   **904.56 μs** |  **49.582 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,149.41 μs |   953.50 μs |  52.265 μs |  0.18 |    0.01 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,662.78 μs** | **5,927.82 μs** | **324.924 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,358.84 μs | 6,327.45 μs | 346.829 μs |  0.55 |    0.03 |  15.6250 |       - |  369.35 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.18 μs** |   **174.04 μs** |   **9.540 μs** |  **1.00** |    **0.09** |   **2.5635** |       **-** |   **42.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     81.79 μs |   111.91 μs |   6.134 μs |  0.62 |    0.06 |   0.4883 |       - |   14.85 KB |        0.35 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,255.95 μs** |   **776.08 μs** |  **42.540 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |   **408.1 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    749.34 μs | 2,621.98 μs | 143.719 μs |  0.60 |    0.10 |   3.9063 |       - |   89.13 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    316.64 μs |   170.42 μs |   9.341 μs |     ? |       ? |   6.8359 |  5.8594 |  202.98 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,341.94 μs | 5,549.53 μs | 304.188 μs |     ? |       ? |  62.5000 | 54.6875 | 1933.62 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,515.96 μs** |   **630.94 μs** |  **34.584 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,314.15 μs |   732.73 μs |  40.163 μs |  0.24 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,449.04 μs** |   **647.30 μs** |  **35.480 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,305.27 μs |   553.41 μs |  30.334 μs |  0.24 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,447.96 μs** |   **386.29 μs** |  **21.174 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,330.57 μs |   263.32 μs |  14.433 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,483.30 μs** |   **472.08 μs** |  **25.876 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,346.10 μs |   429.58 μs |  23.547 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.874 ms** |   **2.519 ms** | **0.1381 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.409 ms |  64.463 ms | 3.5334 ms | 0.006 |  417.95 KB |        5.60 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.798 ms** |  **29.279 ms** | **1.6049 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.261 ms | 110.675 ms | 6.0665 ms | 0.005 |  593.16 KB |        2.37 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.287 ms** |  **26.190 ms** | **1.4356 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.704 ms |  50.021 ms | 2.7418 ms | 0.006 |  808.45 KB |        1.34 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.071 ms** |   **9.267 ms** | **0.5080 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.352 ms |  56.028 ms | 3.0711 ms | 0.005 | 2650.06 KB |        1.12 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.705 ms** |  **41.577 ms** | **2.2790 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.485 ms |  10.043 ms | 0.5505 ms | 0.002 |  188.03 KB |       78.14 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.164 ms** |   **3.373 ms** | **0.1849 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.263 ms |   8.885 ms | 0.4870 ms | 0.002 |  184.59 KB |       44.33 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.947 ms** |  **11.853 ms** | **0.6497 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.931 ms |   8.749 ms | 0.4795 ms | 0.002 |  222.04 KB |       92.28 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.769 ms** |   **5.112 ms** | **0.2802 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.872 ms |   3.587 ms | 0.1966 ms | 0.002 |   183.9 KB |       44.00 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 20.682 μs | 94.410 μs | 5.1749 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.435 μs |  1.302 μs | 0.0714 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.322 μs |  2.958 μs | 0.1621 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 27.099 μs | 10.433 μs | 0.5719 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.971 μs |  2.978 μs | 0.1632 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.350 μs |  2.356 μs | 0.1292 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.078 μs |  9.088 μs | 0.4982 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.505 μs |  2.335 μs | 0.1280 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.672 μs | 14.572 μs | 0.7987 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,312.2 ns | 3,187.2 ns | 174.70 ns |  0.31 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,356.3 ns | 1,551.6 ns |  85.05 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,365.0 ns | 3,123.7 ns | 171.22 ns |  0.32 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,458.3 ns | 1,551.6 ns |  85.05 ns |  0.58 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    666.7 ns |   489.6 ns |  26.84 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 38,892.2 ns | 3,154.7 ns | 172.92 ns |  9.18 |    0.81 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,268.0 ns | 8,394.3 ns | 460.12 ns |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,746.3 ns | 2,728.1 ns | 149.53 ns |  0.88 |    0.08 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.27 μs |   6.130 μs |  0.336 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.41 μs | 810.965 μs | 44.452 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.51 μs |   3.808 μs |  0.209 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,648.37 μs | 250.955 μs | 13.756 μs |    1280 B |


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