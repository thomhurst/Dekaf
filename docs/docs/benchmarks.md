---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 18:33 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,101.35 μs** |   **245.61 μs** |  **13.462 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,368.07 μs | 1,589.00 μs |  87.099 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,190.69 μs** |   **748.67 μs** |  **41.037 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,323.22 μs |   465.29 μs |  25.504 μs |  0.32 |    0.00 |  15.6250 |       - |  339.67 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,617.54 μs** |   **548.22 μs** |  **30.050 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,239.26 μs | 2,202.01 μs | 120.700 μs |  0.19 |    0.02 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,648.88 μs** | **5,629.18 μs** | **308.554 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,731.20 μs | 3,247.53 μs | 178.008 μs |  0.49 |    0.02 |  15.6250 |       - |  361.83 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.16 μs** |    **62.18 μs** |   **3.408 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **41.21 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.59 μs |   128.26 μs |   7.030 μs |  0.53 |    0.05 |        - |       - |    7.22 KB |        0.18 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,241.61 μs** |   **885.49 μs** |  **48.536 μs** |  **1.00** |    **0.05** |  **27.3438** |       **-** |   **467.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    560.47 μs |   197.33 μs |  10.816 μs |  0.45 |    0.02 |        - |       - |   72.13 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    207.99 μs |   287.27 μs |  15.746 μs |     ? |       ? |   0.4883 |       - |   25.52 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,996.75 μs | 1,936.80 μs | 106.162 μs |     ? |       ? |   7.8125 |       - |  941.47 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,407.12 μs** |    **38.06 μs** |   **2.086 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,381.80 μs |   223.90 μs |  12.273 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,403.68 μs** |    **30.41 μs** |   **1.667 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,105.66 μs |    33.64 μs |   1.844 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.96 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,412.02 μs** |    **18.93 μs** |   **1.038 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,109.30 μs |   120.01 μs |   6.578 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,417.13 μs** |   **189.70 μs** |  **10.398 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,284.16 μs |   403.12 μs |  22.096 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.952 ms** | **22.645 ms** | **1.2413 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.342 ms | 15.689 ms | 0.8600 ms | 0.005 | 591.61 KB |        7.93 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.778 ms** | **20.955 ms** | **1.1486 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.060 ms | 45.336 ms | 2.4850 ms | 0.005 | 773.19 KB |        3.09 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.762 ms** | **43.728 ms** | **2.3969 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.131 ms | 13.751 ms | 0.7538 ms | 0.005 | 993.03 KB |        1.65 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.906 ms** | **11.391 ms** | **0.6244 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.456 ms | 57.401 ms | 3.1463 ms | 0.006 | 2832.3 KB |        1.20 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.943 ms** | **18.026 ms** | **0.9881 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.641 ms |  8.350 ms | 0.4577 ms | 0.002 | 182.28 KB |       75.75 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.256 ms** | **10.412 ms** | **0.5707 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.599 ms | 33.392 ms | 1.8303 ms | 0.002 | 184.22 KB |       44.24 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.843 ms** | **58.200 ms** | **3.1902 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.630 ms |  7.040 ms | 0.3859 ms | 0.002 | 182.49 KB |       75.84 |
|                      |            |              |             |              |           |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.415 ms** | **66.322 ms** | **3.6353 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.192 ms |  7.247 ms | 0.3972 ms | 0.002 |  184.9 KB |       44.24 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.817 μs |   1.2771 μs |  0.0700 μs | 14.787 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.632 μs |   3.4438 μs |  0.1888 μs |  9.589 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.973 μs |  26.1439 μs |  1.4330 μs | 12.734 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.838 μs |   5.3115 μs |  0.2911 μs | 26.701 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.894 μs |   0.7595 μs |  0.0416 μs |  8.907 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.280 μs |   3.9753 μs |  0.2179 μs | 19.196 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.833 μs |   6.5505 μs |  0.3591 μs | 17.672 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.902 μs |  52.6808 μs |  2.8876 μs | 20.512 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.648 μs |   4.8554 μs |  0.2661 μs |  4.658 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 18.961 μs | 262.0309 μs | 14.3628 μs | 10.709 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,820.2 ns |   421.3 ns |  23.09 ns |  0.45 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,383.0 ns | 1,277.1 ns |  70.00 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,278.7 ns |   379.8 ns |  20.82 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,685.3 ns | 2,706.0 ns | 148.33 ns |  0.67 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    819.0 ns | 1,169.2 ns |  64.09 ns |  0.20 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,823.3 ns | 6,860.7 ns | 376.06 ns | 10.18 |    0.22 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,011.3 ns | 1,655.4 ns |  90.74 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,007.0 ns | 1,851.5 ns | 101.49 ns |  1.00 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.967 μs |   8.565 μs |  0.4695 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   536.450 μs | 382.267 μs | 20.9534 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.975 μs |   3.882 μs |  0.2128 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,684.562 μs | 101.565 μs |  5.5671 μs |    1280 B |


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