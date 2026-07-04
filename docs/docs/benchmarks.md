---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 03:19 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,129.16 μs** |   **364.72 μs** |  **19.992 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,349.69 μs | 1,795.38 μs |  98.411 μs |  0.22 |    0.01 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,291.26 μs** |   **228.00 μs** |  **12.498 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,334.00 μs |   934.77 μs |  51.238 μs |  0.32 |    0.01 |  15.6250 |       - |  339.61 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,552.66 μs** | **1,367.15 μs** |  **74.938 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,245.25 μs | 2,084.57 μs | 114.262 μs |  0.19 |    0.02 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,273.78 μs** | **3,802.78 μs** | **208.443 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,502.91 μs | 6,553.98 μs | 359.246 μs |  0.53 |    0.03 |  15.6250 |       - |  361.88 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.66 μs** |    **42.01 μs** |   **2.303 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.96 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     56.20 μs |   132.02 μs |   7.236 μs |  0.40 |    0.04 |   0.2441 |       - |    8.89 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,387.51 μs** |   **546.33 μs** |  **29.946 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **422.96 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    633.96 μs | 1,001.86 μs |  54.915 μs |  0.46 |    0.04 |        - |       - |   62.67 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    197.07 μs |   441.74 μs |  24.213 μs |     ? |       ? |   0.9766 |       - |   99.06 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,850.70 μs | 1,649.04 μs |  90.389 μs |     ? |       ? |   7.8125 |       - |  954.56 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,399.40 μs** |    **67.05 μs** |   **3.675 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,111.12 μs |    60.14 μs |   3.297 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,398.78 μs** |    **23.35 μs** |   **1.280 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,110.63 μs |    24.69 μs |   1.353 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,399.96 μs** |   **119.08 μs** |   **6.527 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,111.31 μs |    46.83 μs |   2.567 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,399.31 μs** |    **39.05 μs** |   **2.141 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,110.43 μs |    26.14 μs |   1.433 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.514 ms** | **24.725 ms** | **1.3553 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.943 ms | 33.316 ms | 1.8262 ms | 0.005 |  594.45 KB |        7.97 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.508 ms** |  **6.745 ms** | **0.3697 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.152 ms | 82.408 ms | 4.5170 ms | 0.005 |  787.67 KB |        3.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.355 ms** | **10.517 ms** | **0.5765 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.460 ms | 25.262 ms | 1.3847 ms | 0.005 |  996.11 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.973 ms** |  **7.790 ms** | **0.4270 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.467 ms | 82.575 ms | 4.5262 ms | 0.006 | 2760.53 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.191 ms** | **28.922 ms** | **1.5853 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.209 ms |  6.875 ms | 0.3769 ms | 0.002 |  186.62 KB |       77.56 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.174 ms** |  **8.481 ms** | **0.4649 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.113 ms |  5.645 ms | 0.3094 ms | 0.002 |  186.38 KB |       44.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.830 ms** | **24.894 ms** | **1.3645 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.733 ms | 23.264 ms | 1.2752 ms | 0.002 |  256.71 KB |      106.69 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.292 ms** | **10.327 ms** | **0.5661 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.726 ms | 36.136 ms | 1.9807 ms | 0.002 |  188.13 KB |       45.01 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 23.632 μs | 277.2238 μs | 15.1956 μs | 14.859 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.985 μs |  11.7894 μs |  0.6462 μs |  9.658 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 19.586 μs | 279.3908 μs | 15.3144 μs | 10.809 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.914 μs |   3.7403 μs |  0.2050 μs | 26.801 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.926 μs |   0.8057 μs |  0.0442 μs |  8.946 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.694 μs | 204.4041 μs | 11.2041 μs | 19.245 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.213 μs |  13.3402 μs |  0.7312 μs | 18.234 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.148 μs |   7.7730 μs |  0.4261 μs | 20.299 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.129 μs |  10.5704 μs |  0.5794 μs |  5.370 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.464 μs |  24.9596 μs |  1.3681 μs | 10.690 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,226.0 ns |    111.0 ns |     6.08 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,179.3 ns |    382.8 ns |    20.98 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,365.7 ns |  2,044.6 ns |   112.07 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,682.0 ns |  3,150.8 ns |   172.70 ns |  0.66 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    774.7 ns |  1,540.5 ns |    84.44 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 45,936.3 ns | 85,179.4 ns | 4,668.97 ns | 11.33 |    1.02 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,057.0 ns |  1,590.5 ns |    87.18 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,901.3 ns |  2,161.2 ns |   118.46 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.152 μs |   6.384 μs |  0.3499 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   549.691 μs | 863.492 μs | 47.3309 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.328 μs |   6.669 μs |  0.3655 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,659.002 μs | 180.899 μs |  9.9157 μs |    1280 B |


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