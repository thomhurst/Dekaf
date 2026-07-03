---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 01:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,144.02 μs** |   **380.80 μs** |  **20.873 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,408.23 μs | 3,076.62 μs | 168.640 μs |  0.23 |    0.02 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,361.73 μs** | **1,657.62 μs** |  **90.860 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,330.10 μs |   154.83 μs |   8.487 μs |  0.32 |    0.00 |  15.6250 |       - |   339.8 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,475.07 μs** |   **340.35 μs** |  **18.656 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,193.40 μs | 1,061.33 μs |  58.175 μs |  0.18 |    0.01 |        - |       - |    36.9 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,567.07 μs** | **2,952.16 μs** | **161.818 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,750.77 μs | 7,049.79 μs | 386.423 μs |  0.54 |    0.03 |  15.6250 |       - |   369.6 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.40 μs** |    **88.63 μs** |   **4.858 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **41.99 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.43 μs |    11.92 μs |   0.653 μs |  0.41 |    0.01 |   0.4883 |       - |   14.97 KB |        0.36 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,409.63 μs** |   **198.99 μs** |  **10.907 μs** |  **1.00** |    **0.01** |  **23.4375** |       **-** |  **421.22 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    698.38 μs |   724.99 μs |  39.739 μs |  0.50 |    0.02 |   3.9063 |       - |  146.25 KB |        0.35 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    333.59 μs |   262.08 μs |  14.365 μs |     ? |       ? |   6.8359 |  5.8594 |  212.59 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,201.16 μs | 4,152.87 μs | 227.633 μs |     ? |       ? |  70.3125 | 62.5000 | 1990.69 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,404.27 μs** |   **132.68 μs** |   **7.273 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,330.69 μs |    33.16 μs |   1.818 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,403.99 μs** |   **106.24 μs** |   **5.823 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,328.29 μs |   160.15 μs |   8.778 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,413.14 μs** |   **111.66 μs** |   **6.121 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,344.18 μs |   133.66 μs |   7.326 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,405.27 μs** |    **76.54 μs** |   **4.195 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,327.44 μs |   235.40 μs |  12.903 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.538 ms** | **25.040 ms** | **1.3725 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.816 ms | 42.310 ms | 2.3192 ms | 0.005 |  451.87 KB |        6.06 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.606 ms** | **28.711 ms** | **1.5737 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.499 ms | 36.931 ms | 2.0243 ms | 0.004 |  850.51 KB |        3.40 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.739 ms** |  **9.450 ms** | **0.5180 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.456 ms | 47.503 ms | 2.6038 ms | 0.006 | 1067.84 KB |        1.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.604 ms** | **12.904 ms** | **0.7073 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.834 ms | 37.321 ms | 2.0457 ms | 0.005 | 5727.23 KB |        2.42 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.657 ms** | **14.897 ms** | **0.8166 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.751 ms | 26.091 ms | 1.4301 ms | 0.002 |   252.7 KB |      105.02 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.769 ms** | **32.032 ms** | **1.7558 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.122 ms | 42.668 ms | 2.3388 ms | 0.002 |  696.98 KB |      167.38 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.550 ms** | **32.302 ms** | **1.7706 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.657 ms |  7.882 ms | 0.4321 ms | 0.002 |  692.04 KB |      287.60 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.834 ms** | **22.299 ms** | **1.2223 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.454 ms | 10.529 ms | 0.5771 ms | 0.002 | 2230.38 KB |      533.62 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.677 μs |   2.223 μs |  0.1219 μs | 14.698 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.919 μs |   6.229 μs |  0.3414 μs |  9.989 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 11.115 μs |  21.327 μs |  1.1690 μs | 10.851 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 37.586 μs | 342.439 μs | 18.7702 μs | 26.759 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  9.334 μs |   5.185 μs |  0.2842 μs |  9.477 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.926 μs |   2.966 μs |  0.1626 μs | 19.892 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 19.576 μs |  26.878 μs |  1.4733 μs | 19.055 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  5.854 μs |   6.661 μs |  0.3651 μs |  5.990 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 14.305 μs |  65.534 μs |  3.5922 μs | 12.388 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,593.5 ns |  2,919.0 ns | 160.00 ns |  0.40 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,386.2 ns |  1,925.6 ns | 105.55 ns |  0.35 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,983.0 ns | 10,385.0 ns | 569.24 ns |  0.49 |    0.12 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,794.7 ns |  1,103.8 ns |  60.50 ns |  0.70 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    831.0 ns |  1,277.1 ns |  70.00 ns |  0.21 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,465.7 ns | 17,881.6 ns | 980.15 ns | 10.09 |    0.22 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,010.3 ns |    459.1 ns |  25.17 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,525.5 ns |  7,942.8 ns | 435.37 ns |  1.13 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.99 μs |   3.499 μs |  0.192 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.07 μs | 410.109 μs | 22.479 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.21 μs |   2.465 μs |  0.135 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,674.98 μs | 232.675 μs | 12.754 μs |    1280 B |


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