---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 08:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,099.78 μs** |   **701.06 μs** |  **38.427 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,332.48 μs | 1,543.60 μs |  84.610 μs |  0.22 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,253.35 μs** |   **913.56 μs** |  **50.075 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,294.09 μs |   326.37 μs |  17.889 μs |  0.32 |    0.00 |  15.6250 |       - |  339.76 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,657.68 μs** |   **558.57 μs** |  **30.617 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,185.00 μs |   786.43 μs |  43.107 μs |  0.18 |    0.01 |        - |       - |   36.92 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,546.86 μs** | **2,158.28 μs** | **118.303 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,379.00 μs | 4,495.88 μs | 246.434 μs |  0.55 |    0.02 |  15.6250 |       - |  369.37 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **126.05 μs** |    **69.92 μs** |   **3.832 μs** |  **1.00** |    **0.04** |   **2.5635** |       **-** |   **42.04 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     57.00 μs |    44.59 μs |   2.444 μs |  0.45 |    0.02 |   0.6104 |  0.4883 |   17.33 KB |        0.41 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,172.93 μs** |    **68.33 μs** |   **3.745 μs** |  **1.00** |    **0.00** |  **25.3906** |       **-** |  **425.73 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    655.17 μs |   953.95 μs |  52.289 μs |  0.56 |    0.04 |   5.8594 |  3.9063 |  139.87 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    330.56 μs |    55.28 μs |   3.030 μs |     ? |       ? |   6.8359 |  5.8594 |  203.27 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,162.45 μs | 1,047.19 μs |  57.400 μs |     ? |       ? |  62.5000 | 54.6875 | 1855.06 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,401.99 μs** |    **58.15 μs** |   **3.187 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,354.76 μs |   368.01 μs |  20.172 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,400.33 μs** |    **48.68 μs** |   **2.669 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,103.32 μs |    45.21 μs |   2.478 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.46 μs** |    **25.50 μs** |   **1.398 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,103.86 μs |    34.02 μs |   1.865 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,413.69 μs** |    **73.54 μs** |   **4.031 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,284.51 μs |   548.30 μs |  30.054 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        0.60 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.931 ms** | **13.252 ms** | **0.7264 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.558 ms | 92.089 ms | 5.0477 ms | 0.006 |  407.75 KB |        5.46 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.249 ms** |  **6.237 ms** | **0.3419 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.811 ms | 22.321 ms | 1.2235 ms | 0.004 |  592.88 KB |        2.37 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.565 ms** | **18.533 ms** | **1.0159 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.474 ms | 34.163 ms | 1.8726 ms | 0.005 |  809.23 KB |        1.34 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.264 ms** | **13.549 ms** | **0.7427 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.268 ms | 20.075 ms | 1.1004 ms | 0.005 | 2576.17 KB |        1.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.959 ms** | **20.233 ms** | **1.1090 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.550 ms | 12.353 ms | 0.6771 ms | 0.002 |  181.24 KB |       75.32 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.592 ms** |  **9.819 ms** | **0.5382 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.615 ms |  9.313 ms | 0.5105 ms | 0.002 |  187.77 KB |       45.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.522 ms** |  **4.419 ms** | **0.2422 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.201 ms | 30.373 ms | 1.6649 ms | 0.003 |  199.45 KB |       82.89 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.166 ms** | **10.824 ms** | **0.5933 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.132 ms |  3.711 ms | 0.2034 ms | 0.002 |  183.84 KB |       43.98 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 14.722 μs |  2.487 μs | 0.1363 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.236 μs |  3.424 μs | 0.1877 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.997 μs | 28.602 μs | 1.5678 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 26.589 μs |  2.545 μs | 0.1395 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  9.002 μs |  2.040 μs | 0.1118 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 19.274 μs |  1.221 μs | 0.0669 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.089 μs |  8.858 μs | 0.4856 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.923 μs |  3.130 μs | 0.1716 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.800 μs | 11.635 μs | 0.6378 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,560.0 ns |  5,158.7 ns |   282.77 ns |  1,433.0 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  2,944.3 ns | 52,986.3 ns | 2,904.36 ns |  1,268.0 ns |  0.63 |    0.54 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,423.3 ns |    305.5 ns |    16.74 ns |  1,433.0 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,947.2 ns |  2,966.1 ns |   162.58 ns |  2,890.5 ns |  0.63 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    740.7 ns |  1,115.8 ns |    61.16 ns |    711.0 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,667.3 ns | 12,808.9 ns |   702.10 ns | 41,527.0 ns |  8.90 |    0.27 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,684.3 ns |  2,659.5 ns |   145.77 ns |  4,674.0 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,164.2 ns |  2,429.5 ns |   133.17 ns |  4,097.5 ns |  0.89 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev    | Allocated |
|------------------------ |------------:|-------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.53 μs |     1.005 μs |  0.055 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   568.01 μs |   407.416 μs | 22.332 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.16 μs |     4.495 μs |  0.246 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,696.06 μs | 1,195.108 μs | 65.508 μs |    1280 B |


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