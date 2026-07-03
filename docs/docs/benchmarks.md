---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 14:17 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,142.39 μs** |   **156.78 μs** |   **8.594 μs** |  **1.00** |    **0.00** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,307.03 μs | 1,825.90 μs | 100.084 μs |  0.21 |    0.01 |        - |        - |   35.04 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,344.23 μs** | **1,257.74 μs** |  **68.941 μs** |  **1.00** |    **0.01** |  **62.5000** |  **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,355.77 μs |   857.70 μs |  47.014 μs |  0.32 |    0.01 |  15.6250 |        - |  340.04 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,413.96 μs** |   **695.39 μs** |  **38.117 μs** |  **1.00** |    **0.01** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,544.08 μs | 3,266.49 μs | 179.048 μs |  0.24 |    0.02 |        - |        - |   37.22 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,640.87 μs** | **6,071.01 μs** | **332.772 μs** |  **1.00** |    **0.03** | **109.3750** |  **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,348.03 μs |   938.30 μs |  51.431 μs |  0.50 |    0.01 |  15.6250 |        - |   372.1 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.12 μs** |    **52.66 μs** |   **2.887 μs** |  **1.00** |    **0.03** |   **2.4414** |        **-** |   **42.36 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.06 μs |    71.16 μs |   3.900 μs |  0.49 |    0.03 |   0.9766 |   0.7324 |   20.66 KB |        0.49 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    762.78 μs |   924.73 μs |  50.687 μs |     ? |       ? |   3.9063 |        - |   131.6 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    355.07 μs |   172.08 μs |   9.432 μs |     ? |       ? |  11.7188 |  10.7422 |  259.29 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,637.52 μs | 2,094.98 μs | 114.833 μs |     ? |       ? | 117.1875 | 109.3750 |  2577.9 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,402.59 μs** |    **74.49 μs** |   **4.083 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,228.42 μs |   119.66 μs |   6.559 μs |  0.23 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,406.48 μs** |    **95.39 μs** |   **5.229 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,258.39 μs |   183.50 μs |  10.058 μs |  0.23 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,404.78 μs** |    **62.06 μs** |   **3.402 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,278.32 μs |   260.00 μs |  14.252 μs |  0.24 |    0.00 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,402.26 μs** |    **55.69 μs** |   **3.053 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,292.50 μs |    42.44 μs |   2.326 μs |  0.24 |    0.00 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.165 ms** | **15.646 ms** | **0.8576 ms** | **3,166.946 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.604 ms | 32.643 ms | 1.7893 ms |    15.199 ms | 0.005 |  407.41 KB |        5.46 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.626 ms** |  **4.414 ms** | **0.2419 ms** | **3,164.562 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.740 ms | 38.155 ms | 2.0914 ms |    13.674 ms | 0.004 |  598.91 KB |        2.39 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.702 ms** |  **5.215 ms** | **0.2859 ms** | **3,164.672 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.940 ms | 37.584 ms | 2.0601 ms |    17.099 ms | 0.005 |  810.64 KB |        1.35 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.147 ms** | **25.823 ms** | **1.4154 ms** | **3,164.216 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.716 ms | 43.105 ms | 2.3627 ms |    15.455 ms | 0.005 | 2616.72 KB |        1.11 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.273 ms** | **14.582 ms** | **0.7993 ms** | **3,155.658 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.156 ms |  7.556 ms | 0.4142 ms |     6.206 ms | 0.002 |  184.95 KB |       76.86 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.497 ms** | **21.453 ms** | **1.1759 ms** | **3,155.689 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.089 ms | 68.654 ms | 3.7632 ms |     4.931 ms | 0.002 |  192.27 KB |       46.17 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.367 ms** | **34.877 ms** | **1.9117 ms** | **3,156.046 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.341 ms | 22.795 ms | 1.2495 ms |     6.901 ms | 0.002 |  190.63 KB |       79.22 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.869 ms** | **17.974 ms** | **0.9852 ms** | **3,156.724 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.639 ms |  9.337 ms | 0.5118 ms |     5.389 ms | 0.002 |  225.76 KB |       54.01 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.400 μs |   0.7011 μs |  0.0384 μs | 14.417 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.864 μs |  41.2679 μs |  2.2620 μs |  9.608 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 18.434 μs | 254.3732 μs | 13.9431 μs | 10.419 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.716 μs |   0.3725 μs |  0.0204 μs | 26.709 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.061 μs |   5.0145 μs |  0.2749 μs |  8.907 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.380 μs |   1.2147 μs |  0.0666 μs | 19.396 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.247 μs |  31.2183 μs |  1.7112 μs | 21.160 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.417 μs |   9.6122 μs |  0.5269 μs | 20.638 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.691 μs |   1.3934 μs |  0.0764 μs |  4.708 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.607 μs |  21.6152 μs |  1.1848 μs | 12.107 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,759.7 ns |     936.2 ns |    51.32 ns |  0.44 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,288.3 ns |   3,110.0 ns |   170.47 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,331.8 ns |     637.3 ns |    34.93 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,682.7 ns |   2,909.5 ns |   159.48 ns |  0.67 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    821.7 ns |   3,300.1 ns |   180.89 ns |  0.20 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 50,174.7 ns | 160,152.5 ns | 8,778.50 ns | 12.44 |    1.92 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,037.3 ns |   2,593.6 ns |   142.16 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,895.5 ns |   2,631.0 ns |   144.21 ns |  0.97 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev     | Allocated |
|------------------------ |-------------:|------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.464 μs |   0.5494 μs |  0.0301 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   558.553 μs | 803.2710 μs | 44.0300 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.459 μs |   3.5276 μs |  0.1934 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,676.262 μs | 640.6538 μs | 35.1164 μs |    1280 B |


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