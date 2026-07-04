---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 07:34 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,211.36 μs** |   **484.43 μs** |  **26.553 μs** |  **6,208.06 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,377.05 μs | 1,889.72 μs | 103.582 μs |  1,354.94 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,421.13 μs** |   **377.21 μs** |  **20.676 μs** |  **7,415.49 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,274.84 μs |   491.82 μs |  26.958 μs |  2,266.56 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.54 KB |        0.32 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,408.04 μs** | **7,373.60 μs** | **404.172 μs** |  **6,189.37 μs** |  **1.00** |    **0.08** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,489.88 μs |   606.29 μs |  33.233 μs |  1,497.49 μs |  0.23 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,854.71 μs** | **4,909.94 μs** | **269.130 μs** | **12,788.84 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,806.39 μs | 7,018.17 μs | 384.689 μs |  6,739.79 μs |  0.53 |    0.03 |  15.6250 |       - |  361.72 KB |        0.19 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **148.38 μs** |    **73.87 μs** |   **4.049 μs** |    **148.09 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.24 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.72 μs |   131.36 μs |   7.200 μs |     60.60 μs |  0.44 |    0.04 |        - |       - |   10.44 KB |        0.25 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,451.27 μs** |   **417.56 μs** |  **22.888 μs** |  **1,452.43 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |  **414.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    606.91 μs |   753.69 μs |  41.312 μs |    603.47 μs |  0.42 |    0.03 |        - |       - |   76.19 KB |        0.18 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    267.88 μs | 2,680.25 μs | 146.914 μs |    187.21 μs |     ? |       ? |   0.4883 |       - |   11.85 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,163.13 μs | 5,232.10 μs | 286.789 μs |  2,035.95 μs |     ? |       ? |   7.8125 |       - | 1056.62 KB |           ? |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,437.50 μs** |    **89.58 μs** |   **4.910 μs** |  **5,438.26 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,383.60 μs |   355.35 μs |  19.478 μs |  1,393.37 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,440.28 μs** |   **105.81 μs** |   **5.800 μs** |  **5,438.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,360.03 μs |   262.81 μs |  14.405 μs |  1,358.52 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,449.57 μs** |   **153.09 μs** |   **8.391 μs** |  **5,453.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,314.51 μs |   248.48 μs |  13.620 μs |  1,311.29 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,440.90 μs** |    **99.10 μs** |   **5.432 μs** |  **5,443.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,134.31 μs |    61.03 μs |   3.345 μs |  1,134.69 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.754 ms** | **17.668 ms** | **0.9684 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.564 ms | 36.945 ms | 2.0251 ms | 0.005 |   591.8 KB |        7.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.524 ms** | **66.202 ms** | **3.6288 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.170 ms | 50.411 ms | 2.7632 ms | 0.005 |  776.34 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.137 ms** | **48.604 ms** | **2.6641 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.490 ms | 66.817 ms | 3.6625 ms | 0.006 | 1031.88 KB |        1.71 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.384 ms** | **25.168 ms** | **1.3795 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.032 ms | 50.937 ms | 2.7920 ms | 0.006 | 2762.15 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.191 ms** |  **9.091 ms** | **0.4983 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.279 ms | 15.597 ms | 0.8549 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.563 ms** | **30.554 ms** | **1.6748 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.998 ms |  6.180 ms | 0.3388 ms | 0.002 |  188.59 KB |       45.29 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.301 ms** |  **8.952 ms** | **0.4907 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.359 ms |  7.850 ms | 0.4303 ms | 0.002 |  195.09 KB |       81.07 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.192 ms** | **35.544 ms** | **1.9483 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.869 ms | 22.571 ms | 1.2372 ms | 0.002 |  188.52 KB |       45.10 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 16.044 μs |  7.142 μs | 0.3915 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.426 μs | 10.523 μs | 0.5768 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.820 μs |  5.222 μs | 0.2862 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.305 μs |  2.273 μs | 0.1246 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.378 μs | 13.018 μs | 0.7135 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 15.673 μs | 14.863 μs | 0.8147 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.755 μs | 56.844 μs | 3.1158 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.429 μs | 69.555 μs | 3.8126 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.055 μs | 12.459 μs | 0.6829 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  9.513 μs | 21.893 μs | 1.2001 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,679.3 ns |   5,489.0 ns |   300.87 ns |  1,718.0 ns |  0.41 |    0.08 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,708.8 ns |   7,074.3 ns |   387.77 ns |  1,580.5 ns |  0.42 |    0.10 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  6,186.3 ns | 149,354.1 ns | 8,186.60 ns |  1,712.0 ns |  1.51 |    1.75 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,175.3 ns |   6,051.7 ns |   331.71 ns |  2,252.0 ns |  0.53 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    924.2 ns |   1,035.3 ns |    56.75 ns |    910.5 ns |  0.23 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,625.8 ns |  88,104.7 ns | 4,829.32 ns | 39,798.5 ns |  9.66 |    1.62 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,164.0 ns |  10,986.4 ns |   602.20 ns |  4,241.0 ns |  1.01 |    0.18 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,871.3 ns |  15,770.1 ns |   864.41 ns |  3,672.0 ns |  0.94 |    0.22 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev      | Allocated |
|------------------------ |-------------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.116 μs |    14.852 μs |   0.8141 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   666.254 μs | 3,100.255 μs | 169.9354 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.704 μs |    16.632 μs |   0.9116 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,361.987 μs |   267.804 μs |  14.6792 μs |    1280 B |


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