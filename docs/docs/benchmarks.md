---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 12:06 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,323.66 μs** |  **1,083.59 μs** |    **59.395 μs** |  **1.00** |    **0.01** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,600.50 μs |  5,849.98 μs |   320.657 μs |  0.25 |    0.04 |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,531.53 μs** |    **643.25 μs** |    **35.259 μs** |  **1.00** |    **0.01** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,240.48 μs |    427.54 μs |    23.435 μs |  0.30 |    0.00 |  3.9063 |  339.44 KB |        0.32 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **7,724.62 μs** | **21,477.40 μs** | **1,177.249 μs** |  **1.02** |    **0.19** |       **-** |  **194.03 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,120.84 μs |     89.05 μs |     4.881 μs |  0.15 |    0.02 |       - |   36.29 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,527.69 μs** |  **1,958.25 μs** |   **107.338 μs** |  **1.00** |    **0.02** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,149.25 μs | 22,731.56 μs | 1,245.993 μs |  0.60 |    0.13 |       - |  361.55 KB |        0.19 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **147.35 μs** |     **64.07 μs** |     **3.512 μs** |  **1.00** |    **0.03** |  **0.4883** |   **42.08 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    61.07 μs |    375.70 μs |    20.593 μs |  0.41 |    0.12 |       - |   12.54 KB |        0.30 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **2,243.49 μs** | **10,309.45 μs** |   **565.096 μs** |  **1.05** |    **0.35** |  **3.9063** |  **421.72 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   560.97 μs |  1,684.54 μs |    92.335 μs |  0.26 |    0.07 |       - |   41.78 KB |        0.10 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   171.80 μs |    624.03 μs |    34.205 μs |     ? |       ? |       - |   24.25 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,889.32 μs |  2,841.48 μs |   155.751 μs |     ? |       ? |       - |  944.91 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,481.64 μs** |    **280.80 μs** |    **15.392 μs** |  **1.00** |    **0.00** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,301.66 μs |    115.08 μs |     6.308 μs |  0.24 |    0.00 |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,470.96 μs** |    **431.06 μs** |    **23.628 μs** |  **1.00** |    **0.01** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,214.83 μs |  1,924.79 μs |   105.504 μs |  0.22 |    0.02 |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,878.81 μs** |  **5,763.41 μs** |   **315.912 μs** |  **1.00** |    **0.07** |       **-** |    **2.24 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,399.57 μs |    278.57 μs |    15.270 μs |  0.24 |    0.01 |       - |    1.14 KB |        0.51 |
|                         |               |             |           |             |              |              |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,468.06 μs** |    **165.75 μs** |     **9.086 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,299.48 μs |    246.25 μs |    13.498 μs |  0.24 |    0.00 |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error        | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-------------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171.287 ms** |     **9.868 ms** |   **0.5409 ms** | **1.000** |    **0.00** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.525 ms |    44.506 ms |   2.4395 ms | 0.006 |    0.00 |  594.29 KB |        7.96 |
|                      |            |              |             |              |              |             |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.661 ms** |    **10.236 ms** |   **0.5611 ms** | **1.000** |    **0.00** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.953 ms |    14.545 ms |   0.7972 ms | 0.005 |    0.00 |  785.68 KB |        3.14 |
|                      |            |              |             |              |              |             |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.923 ms** |    **17.432 ms** |   **0.9555 ms** | **1.000** |    **0.00** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    22.123 ms |   101.900 ms |   5.5855 ms | 0.007 |    0.00 | 1090.01 KB |        1.81 |
|                      |            |              |             |              |              |             |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,374.222 ms** | **6,571.394 ms** | **360.2003 ms** | **1.007** |    **0.13** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.090 ms |    27.511 ms |   1.5080 ms | 0.005 |    0.00 | 2761.69 KB |        1.17 |
|                      |            |              |             |              |              |             |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.684 ms** |    **28.644 ms** |   **1.5701 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.972 ms |    20.987 ms |   1.1503 ms | 0.002 |    0.00 |  187.19 KB |       77.79 |
|                      |            |              |             |              |              |             |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.728 ms** |    **25.418 ms** |   **1.3932 ms** | **1.000** |    **0.00** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.975 ms |    14.258 ms |   0.7815 ms | 0.002 |    0.00 |  186.48 KB |       44.78 |
|                      |            |              |             |              |              |             |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.272 ms** |    **50.345 ms** |   **2.7596 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.142 ms |    14.599 ms |   0.8002 ms | 0.002 |    0.00 |  184.59 KB |       76.71 |
|                      |            |              |             |              |              |             |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.485 ms** |    **15.528 ms** |   **0.8511 ms** | **1.000** |    **0.00** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.694 ms |    10.776 ms |   0.5907 ms | 0.002 |    0.00 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error    | StdDev    | Allocated |
|------------------------------------------------ |----------:|---------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.046 μs | 1.583 μs | 0.0868 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.524 μs | 1.951 μs | 0.1069 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.263 μs | 3.110 μs | 0.1705 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.749 μs | 1.016 μs | 0.0557 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.879 μs | 1.242 μs | 0.0681 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.407 μs | 4.488 μs | 0.2460 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.094 μs | 7.268 μs | 0.3984 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.355 μs | 6.882 μs | 0.3772 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.785 μs | 4.970 μs | 0.2724 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.460 μs | 5.007 μs | 0.2744 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error       | StdDev     | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.513 μs |   6.1889 μs |  0.3392 μs |  1.403 μs |  0.35 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.459 μs |   2.5301 μs |  0.1387 μs |  1.423 μs |  0.34 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  2.772 μs |  10.4814 μs |  0.5745 μs |  2.845 μs |  0.64 |    0.12 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.878 μs |   2.6079 μs |  0.1429 μs |  2.845 μs |  0.67 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.178 μs |   0.6407 μs |  0.0351 μs |  1.181 μs |  0.27 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 62.106 μs | 413.1521 μs | 22.6463 μs | 49.672 μs | 14.40 |    4.56 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.313 μs |   1.7403 μs |  0.0954 μs |  4.323 μs |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.778 μs |  11.1106 μs |  0.6090 μs |  4.468 μs |  1.11 |    0.12 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev     | Allocated |
|------------------------ |-------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.152 μs |     2.107 μs |  0.1155 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   544.321 μs |   696.143 μs | 38.1580 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.738 μs |    12.449 μs |  0.6824 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,710.214 μs | 1,283.060 μs | 70.3289 μs |    1280 B |


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