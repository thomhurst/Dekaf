---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 23:25 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,081.46 μs** |    **660.12 μs** |  **36.183 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,378.46 μs |  2,945.09 μs | 161.430 μs |  0.23 |    0.02 |       - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,227.16 μs** |    **650.17 μs** |  **35.638 μs** |  **1.00** |    **0.01** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,342.97 μs |    777.14 μs |  42.598 μs |  0.32 |    0.01 | 15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,546.73 μs** |  **1,025.58 μs** |  **56.215 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,170.50 μs |    954.40 μs |  52.314 μs |  0.18 |    0.01 |       - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,154.08 μs** |  **1,480.20 μs** |  **81.135 μs** |  **1.00** |    **0.01** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,035.82 μs | 17,269.93 μs | 946.623 μs |  0.58 |    0.07 | 15.6250 |       - |  361.56 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.35 μs** |     **11.32 μs** |   **0.621 μs** |  **1.00** |    **0.01** |  **2.4414** |       **-** |   **41.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     75.18 μs |     96.25 μs |   5.276 μs |  0.55 |    0.03 |       - |       - |    8.23 KB |        0.20 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,405.83 μs** |    **189.75 μs** |  **10.401 μs** |  **1.00** |    **0.01** | **25.3906** |       **-** |     **417 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    699.53 μs |    278.94 μs |  15.290 μs |  0.50 |    0.01 |       - |       - |    65.6 KB |        0.16 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    209.79 μs |    543.25 μs |  29.777 μs |     ? |       ? |  0.9766 |       - |   99.88 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,878.72 μs |  1,049.34 μs |  57.518 μs |     ? |       ? |  7.8125 |       - |  977.16 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,397.15 μs** |    **115.77 μs** |   **6.346 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,342.04 μs |    184.60 μs |  10.118 μs |  0.25 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **6,134.24 μs** |  **6,426.30 μs** | **352.247 μs** |  **1.00** |    **0.07** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,319.59 μs |    180.62 μs |   9.900 μs |  0.22 |    0.01 |       - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,401.90 μs** |     **65.33 μs** |   **3.581 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,291.02 μs |    307.90 μs |  16.877 μs |  0.24 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.59 μs** |    **143.91 μs** |   **7.888 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,127.27 μs |    140.77 μs |   7.716 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.309 ms** |  **18.303 ms** |  **1.0032 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.314 ms |  22.067 ms |  1.2096 ms | 0.005 |  590.37 KB |        7.91 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.799 ms** |  **34.735 ms** |  **1.9039 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.394 ms |  30.522 ms |  1.6730 ms | 0.004 |  774.79 KB |        3.09 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.096 ms** |  **20.332 ms** |  **1.1145 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    21.047 ms | 142.752 ms |  7.8247 ms | 0.007 |  994.84 KB |        1.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.844 ms** |  **33.123 ms** |  **1.8156 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.108 ms |  26.586 ms |  1.4572 ms | 0.005 | 2760.73 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,140.086 ms** | **494.636 ms** | **27.1127 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.960 ms |  12.942 ms |  0.7094 ms | 0.002 |  190.34 KB |       79.10 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.292 ms** |  **31.032 ms** |  **1.7010 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.585 ms |  10.095 ms |  0.5533 ms | 0.002 |  186.32 KB |       44.74 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.778 ms** |  **42.775 ms** |  **2.3446 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.602 ms |   2.725 ms |  0.1494 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.123 ms** |  **45.938 ms** |  **2.5180 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.955 ms |   8.971 ms |  0.4917 ms | 0.002 |  187.04 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 31.780 μs | 188.0707 μs | 10.3088 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.309 μs |  36.0483 μs |  1.9759 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.373 μs |   3.6730 μs |  0.2013 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 30.205 μs | 107.9142 μs |  5.9151 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.984 μs |   0.7373 μs |  0.0404 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.349 μs |   3.0232 μs |  0.1657 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.657 μs |  49.7570 μs |  2.7274 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.106 μs |   6.5778 μs |  0.3606 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.962 μs |  15.3436 μs |  0.8410 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.665 μs |   7.4501 μs |  0.4084 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,213.0 ns |   657.8 ns |  36.06 ns |  0.27 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,323.0 ns |   632.0 ns |  34.64 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,293.0 ns | 2,849.8 ns | 156.20 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,610.7 ns |   650.2 ns |  35.64 ns |  0.59 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    695.3 ns |   526.7 ns |  28.87 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,717.3 ns | 4,724.2 ns | 258.95 ns |  8.93 |    0.45 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,458.7 ns | 4,593.3 ns | 251.77 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,923.7 ns | 3,448.7 ns | 189.03 ns |  0.88 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.790 μs |   2.289 μs |  0.1255 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   540.875 μs | 356.585 μs | 19.5457 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.392 μs |  32.575 μs |  1.7855 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,913.668 μs | 520.705 μs | 28.5416 μs |    1280 B |


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