---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 10:41 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,124.10 μs** |   **936.69 μs** |  **51.343 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,316.68 μs |   432.06 μs |  23.683 μs |  0.22 |    0.00 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,365.11 μs** |   **844.50 μs** |  **46.290 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,337.75 μs |   668.32 μs |  36.633 μs |  0.32 |    0.00 |  15.6250 |       - |  339.75 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,402.90 μs** |   **481.38 μs** |  **26.386 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,279.71 μs | 1,462.64 μs |  80.172 μs |  0.20 |    0.01 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,349.25 μs** | **2,853.66 μs** | **156.419 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.94 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,161.15 μs | 2,125.82 μs | 116.524 μs |  0.50 |    0.01 |  15.6250 |       - |  369.39 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.06 μs** |    **80.04 μs** |   **4.387 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **41.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.15 μs |    30.55 μs |   1.675 μs |  0.42 |    0.02 |   0.4883 |       - |    17.9 KB |        0.43 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,406.82 μs** | **4,244.33 μs** | **232.646 μs** |  **1.02** |    **0.21** |  **25.3906** |       **-** |  **427.64 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    695.89 μs | 1,336.66 μs |  73.267 μs |  0.50 |    0.08 |   3.9063 |       - |     122 KB |        0.29 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    331.87 μs |   171.80 μs |   9.417 μs |     ? |       ? |   6.8359 |  5.8594 |  200.53 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,282.88 μs |   899.80 μs |  49.321 μs |     ? |       ? |  70.3125 | 62.5000 | 2046.71 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,409.38 μs** |    **93.22 μs** |   **5.110 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,113.47 μs |    58.13 μs |   3.186 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,412.00 μs** |   **116.43 μs** |   **6.382 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,110.57 μs |    22.46 μs |   1.231 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,407.52 μs** |    **47.42 μs** |   **2.599 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,113.42 μs |    52.64 μs |   2.885 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,410.53 μs** |   **126.16 μs** |   **6.915 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,111.51 μs |    50.61 μs |   2.774 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.702 ms** | **16.182 ms** | **0.8870 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.637 ms | 22.919 ms | 1.2563 ms | 0.005 |  407.91 KB |        5.47 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.688 ms** | **23.307 ms** | **1.2775 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.815 ms |  8.158 ms | 0.4471 ms | 0.004 |  589.82 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.062 ms** |  **7.061 ms** | **0.3870 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.658 ms | 36.513 ms | 2.0014 ms | 0.005 |  889.78 KB |        1.48 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.430 ms** | **23.476 ms** | **1.2868 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.075 ms | 34.209 ms | 1.8751 ms | 0.004 | 2650.08 KB |        1.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.523 ms** | **30.244 ms** | **1.6578 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.635 ms | 27.461 ms | 1.5052 ms | 0.002 |  181.27 KB |       75.33 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.350 ms** | **29.891 ms** | **1.6384 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.435 ms |  7.741 ms | 0.4243 ms | 0.002 |  183.19 KB |       43.99 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.339 ms** | **44.003 ms** | **2.4119 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.508 ms | 11.648 ms | 0.6385 ms | 0.002 |  181.49 KB |       75.43 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.634 ms** | **31.732 ms** | **1.7393 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.889 ms | 19.509 ms | 1.0693 ms | 0.002 |  183.84 KB |       43.98 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 15.350 μs |   4.825 μs |  0.2645 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  9.974 μs |   6.638 μs |  0.3639 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 11.311 μs |  10.307 μs |  0.5650 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 33.956 μs |  28.228 μs |  1.5473 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  9.211 μs |   1.645 μs |  0.0902 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 27.316 μs | 184.879 μs | 10.1339 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.140 μs |  23.698 μs |  1.2990 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.310 μs |   8.366 μs |  0.4585 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.267 μs |  18.652 μs |  1.0224 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,340.7 ns |  4,877.6 ns |   267.4 ns |  0.34 |    0.09 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,179.3 ns |  4,321.1 ns |   236.9 ns |  0.30 |    0.08 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,732.7 ns |  5,224.9 ns |   286.4 ns |  0.44 |    0.11 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,573.8 ns |  4,527.3 ns |   248.2 ns |  0.65 |    0.14 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    661.7 ns |  2,176.5 ns |   119.3 ns |  0.17 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,473.0 ns | 10,327.2 ns |   566.1 ns |  8.96 |    1.77 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,119.2 ns | 19,172.3 ns | 1,050.9 ns |  1.04 |    0.31 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,084.2 ns |  5,514.9 ns |   302.3 ns |  1.03 |    0.21 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.735 μs |  17.429 μs |  0.9554 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   532.737 μs |  50.065 μs |  2.7442 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.944 μs |   5.914 μs |  0.3241 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,609.688 μs | 222.329 μs | 12.1866 μs |    1280 B |


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