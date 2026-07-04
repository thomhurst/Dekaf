---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 19:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,101.63 μs** |   **548.98 μs** |  **30.091 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,340.70 μs | 1,398.59 μs |  76.662 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,348.71 μs** | **1,949.08 μs** | **106.836 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,341.46 μs |   643.71 μs |  35.284 μs |  0.32 |    0.01 |  15.6250 |       - |  339.52 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,514.60 μs** |   **990.46 μs** |  **54.290 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,318.91 μs | 1,984.32 μs | 108.767 μs |  0.20 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,054.08 μs** | **1,298.94 μs** |  **71.199 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,947.35 μs | 1,079.14 μs |  59.151 μs |  0.49 |    0.00 |  15.6250 |       - |  361.81 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **146.18 μs** |    **13.20 μs** |   **0.723 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **42.07 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     50.15 μs |    40.75 μs |   2.233 μs |  0.34 |    0.01 |   0.2441 |       - |    8.91 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,377.37 μs** |   **499.14 μs** |  **27.359 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |   **418.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    587.81 μs |   629.07 μs |  34.482 μs |  0.43 |    0.02 |        - |       - |   65.89 KB |        0.16 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    206.37 μs |   183.48 μs |  10.057 μs |     ? |       ? |   0.9766 |       - |  101.88 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,076.24 μs | 3,252.11 μs | 178.259 μs |     ? |       ? |   7.8125 |       - |  950.37 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,409.92 μs** |    **78.23 μs** |   **4.288 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,113.75 μs |    31.72 μs |   1.739 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,408.65 μs** |    **38.54 μs** |   **2.113 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,113.13 μs |    28.94 μs |   1.586 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,413.74 μs** |    **97.05 μs** |   **5.319 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,151.23 μs |   249.74 μs |  13.689 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,410.01 μs** |    **42.98 μs** |   **2.356 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,257.32 μs |   405.60 μs |  22.232 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.216 ms** | **31.810 ms** | **1.7436 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.881 ms | 27.719 ms | 1.5194 ms | 0.005 |  591.73 KB |        7.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.113 ms** | **69.580 ms** | **3.8139 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.912 ms |  6.704 ms | 0.3674 ms | 0.004 |  774.67 KB |        3.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.394 ms** |  **3.200 ms** | **0.1754 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.473 ms | 46.221 ms | 2.5335 ms | 0.006 | 1111.97 KB |        1.85 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.786 ms** |  **4.433 ms** | **0.2430 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.531 ms | 36.479 ms | 1.9996 ms | 0.005 | 2760.47 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.208 ms** | **29.209 ms** | **1.6010 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.692 ms |  5.299 ms | 0.2905 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.444 ms** | **21.357 ms** | **1.1706 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.289 ms |  9.135 ms | 0.5007 ms | 0.002 |  186.42 KB |       44.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.088 ms** |  **9.772 ms** | **0.5356 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.250 ms | 32.441 ms | 1.7782 ms | 0.002 |  193.65 KB |       80.48 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.744 ms** | **35.656 ms** | **1.9544 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.703 ms |  9.327 ms | 0.5112 ms | 0.002 |  261.61 KB |       62.59 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.089 μs |   2.3717 μs | 0.1300 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.369 μs |  12.3568 μs | 0.6773 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.797 μs |   8.5290 μs | 0.4675 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 30.641 μs | 124.2035 μs | 6.8080 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.926 μs |   0.4827 μs | 0.0265 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.295 μs |   0.9195 μs | 0.0504 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.727 μs |  10.3426 μs | 0.5669 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.031 μs |   7.2289 μs | 0.3962 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.535 μs |   1.7340 μs | 0.0950 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.760 μs |   1.7509 μs | 0.0960 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,800.7 ns | 15,627.8 ns | 856.61 ns |  1,383.0 ns |  0.45 |    0.18 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,276.0 ns |  1,998.2 ns | 109.53 ns |  1,312.0 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,453.0 ns |  1,094.6 ns |  60.00 ns |  1,453.0 ns |  0.36 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,571.7 ns |    936.2 ns |  51.32 ns |  2,585.0 ns |  0.64 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    731.7 ns |    182.7 ns |  10.02 ns |    731.0 ns |  0.18 |    0.00 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,926.7 ns |  8,818.4 ns | 483.36 ns | 41,077.0 ns | 10.18 |    0.23 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,021.3 ns |  1,655.4 ns |  90.74 ns |  4,058.0 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,993.7 ns |  1,417.1 ns |  77.67 ns |  4,017.0 ns |  0.99 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev     | Allocated |
|------------------------ |-------------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.814 μs |     2.733 μs |  0.1498 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   531.713 μs |   391.085 μs | 21.4367 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.983 μs |     5.026 μs |  0.2755 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,701.064 μs | 1,576.055 μs | 86.3889 μs |    1280 B |


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