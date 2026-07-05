---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 21:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,137.91 μs** |   **312.50 μs** |  **17.129 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,322.56 μs | 1,480.72 μs |  81.163 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,381.99 μs** |   **365.02 μs** |  **20.008 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,392.59 μs |   689.45 μs |  37.791 μs |  0.32 |    0.00 |  15.6250 |       - |  339.64 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,210.31 μs** |   **845.56 μs** |  **46.348 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,356.69 μs | 1,595.82 μs |  87.472 μs |  0.22 |    0.01 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,625.32 μs** | **2,622.15 μs** | **143.729 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,614.03 μs | 4,118.22 μs | 225.734 μs |  0.52 |    0.02 |  15.6250 |       - |  361.64 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.73 μs** |    **35.17 μs** |   **1.928 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.46 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     56.53 μs |    38.43 μs |   2.107 μs |  0.40 |    0.01 |   0.2441 |       - |    9.16 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,406.97 μs** |   **365.73 μs** |  **20.047 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **419.65 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    616.74 μs |   808.78 μs |  44.332 μs |  0.44 |    0.03 |        - |       - |   124.9 KB |        0.30 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    223.25 μs |   165.53 μs |   9.073 μs |     ? |       ? |   0.9766 |       - |  102.93 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,040.22 μs | 2,425.32 μs | 132.940 μs |     ? |       ? |   7.8125 |       - |  1007.8 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,422.86 μs** |    **62.56 μs** |   **3.429 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,183.57 μs |    99.39 μs |   5.448 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,423.16 μs** |    **20.55 μs** |   **1.127 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,218.20 μs |   494.13 μs |  27.085 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,423.83 μs** |    **53.90 μs** |   **2.954 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,261.51 μs |   523.19 μs |  28.678 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,426.28 μs** |    **81.82 μs** |   **4.485 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,302.08 μs |   442.63 μs |  24.262 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.781 ms** |  **8.512 ms** | **0.4666 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.732 ms | 36.205 ms | 1.9845 ms | 0.005 |   598.1 KB |        8.02 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.224 ms** | **17.236 ms** | **0.9448 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.085 ms | 38.163 ms | 2.0919 ms | 0.004 |  778.93 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.934 ms** | **24.222 ms** | **1.3277 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.849 ms | 90.199 ms | 4.9441 ms | 0.006 | 1015.38 KB |        1.69 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.386 ms** | **29.785 ms** | **1.6326 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.289 ms | 23.335 ms | 1.2791 ms | 0.005 | 2763.85 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.817 ms** | **45.743 ms** | **2.5073 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.757 ms |  6.346 ms | 0.3478 ms | 0.002 |  185.79 KB |       77.21 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.820 ms** | **17.217 ms** | **0.9437 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.320 ms |  3.056 ms | 0.1675 ms | 0.002 |  186.96 KB |       44.90 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.418 ms** | **13.121 ms** | **0.7192 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.845 ms | 20.704 ms | 1.1348 ms | 0.002 |  220.71 KB |       91.72 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.770 ms** |  **8.601 ms** | **0.4715 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.890 ms |  5.287 ms | 0.2898 ms | 0.002 |  187.04 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.754 μs |  1.5810 μs | 0.0867 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.575 μs |  3.1872 μs | 0.1747 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.497 μs |  0.2787 μs | 0.0153 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.131 μs |  6.5401 μs | 0.3585 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.317 μs |  4.6143 μs | 0.2529 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.461 μs |  2.0506 μs | 0.1124 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.903 μs | 22.7463 μs | 1.2468 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.465 μs |  3.4214 μs | 0.1875 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.728 μs |  7.5303 μs | 0.4128 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.120 μs | 11.3606 μs | 0.6227 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.283 μs |   1.286 μs |  0.0705 μs |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.357 μs |   3.912 μs |  0.2144 μs |  0.31 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.641 μs |   5.603 μs |  0.3071 μs |  0.38 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.598 μs |   1.417 μs |  0.0777 μs |  0.60 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.157 μs |   4.603 μs |  0.2523 μs |  0.27 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 50.089 μs | 297.973 μs | 16.3329 μs | 11.54 |    3.36 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.360 μs |   6.340 μs |  0.3475 μs |  1.00 |    0.10 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.776 μs |  23.202 μs |  1.2718 μs |  1.10 |    0.27 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.260 μs |   1.401 μs |  0.0768 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   517.119 μs | 227.956 μs | 12.4950 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.742 μs |   9.674 μs |  0.5303 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,734.488 μs | 553.662 μs | 30.3481 μs |    1280 B |


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