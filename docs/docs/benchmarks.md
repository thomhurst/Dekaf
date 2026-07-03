---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 22:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,251.06 μs** |   **829.00 μs** |  **45.440 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,396.09 μs |   468.71 μs |  25.692 μs |  0.22 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,435.08 μs** |   **989.66 μs** |  **54.246 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,333.83 μs |   257.29 μs |  14.103 μs |  0.31 |    0.00 |  15.6250 |       - |  339.55 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,125.44 μs** | **1,105.33 μs** |  **60.587 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,339.53 μs | 1,980.10 μs | 108.536 μs |  0.22 |    0.02 |        - |       - |   36.34 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,038.65 μs** | **2,583.42 μs** | **141.606 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,844.71 μs | 4,295.01 μs | 235.424 μs |  0.52 |    0.02 |  15.6250 |       - |  361.77 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **153.50 μs** |   **212.14 μs** |  **11.628 μs** |  **1.00** |    **0.09** |   **2.4414** |       **-** |   **41.93 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     83.83 μs |   306.90 μs |  16.822 μs |  0.55 |    0.10 |        - |       - |     8.4 KB |        0.20 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,459.89 μs** |   **399.19 μs** |  **21.881 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **420.91 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    618.17 μs |   881.01 μs |  48.291 μs |  0.42 |    0.03 |        - |       - |  105.21 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    219.63 μs |   337.49 μs |  18.499 μs |     ? |       ? |   0.9766 |       - |  104.82 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,129.49 μs | 2,970.48 μs | 162.822 μs |     ? |       ? |   7.8125 |       - | 1001.29 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,450.29 μs** |    **62.81 μs** |   **3.443 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,311.69 μs | 1,211.08 μs |  66.383 μs |  0.24 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,454.89 μs** |    **86.37 μs** |   **4.734 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,421.36 μs |   615.29 μs |  33.726 μs |  0.26 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,461.97 μs** |    **13.76 μs** |   **0.754 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,401.71 μs |   919.42 μs |  50.397 μs |  0.26 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,467.83 μs** |   **136.89 μs** |   **7.503 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,398.37 μs |   368.74 μs |  20.212 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.272 ms** | **14.880 ms** | **0.8156 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.835 ms |  5.544 ms | 0.3039 ms | 0.005 |  592.41 KB |        7.94 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,169.082 ms** | **80.303 ms** | **4.4017 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.266 ms | 25.212 ms | 1.3819 ms | 0.005 |  780.66 KB |        3.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.670 ms** | **18.323 ms** | **1.0044 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.867 ms | 19.876 ms | 1.0895 ms | 0.007 |  1012.9 KB |        1.68 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,169.198 ms** | **22.556 ms** | **1.2364 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.949 ms | 20.796 ms | 1.1399 ms | 0.006 | 2762.17 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.894 ms** | **38.444 ms** | **2.1073 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.821 ms | 32.180 ms | 1.7639 ms | 0.002 |  208.45 KB |       86.63 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,158.078 ms** | **43.300 ms** | **2.3734 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     8.967 ms | 56.013 ms | 3.0703 ms | 0.003 |  188.59 KB |       45.29 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.732 ms** | **30.879 ms** | **1.6926 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.654 ms |  9.060 ms | 0.4966 ms | 0.002 |   205.2 KB |       85.28 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.828 ms** | **37.714 ms** | **2.0672 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.737 ms |  6.730 ms | 0.3689 ms | 0.002 |  190.62 KB |       45.61 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.673 μs |  3.872 μs | 0.2122 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.919 μs | 19.521 μs | 1.0700 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.698 μs | 16.847 μs | 0.9235 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.830 μs |  3.523 μs | 0.1931 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.115 μs |  1.734 μs | 0.0950 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 24.820 μs | 77.644 μs | 4.2559 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.172 μs | 17.055 μs | 0.9348 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.283 μs |  9.006 μs | 0.4936 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.601 μs | 11.598 μs | 0.6357 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.570 μs |  7.763 μs | 0.4255 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,168.7 ns |  4,113.3 ns | 225.46 ns |  0.28 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,268.7 ns |  2,281.1 ns | 125.03 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,328.7 ns |  2,114.5 ns | 115.90 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,614.3 ns |    326.5 ns |  17.90 ns |  0.63 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    704.7 ns |  1,325.7 ns |  72.67 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,273.3 ns | 11,818.7 ns | 647.82 ns |  8.49 |    0.82 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,186.2 ns |  7,918.7 ns | 434.05 ns |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,758.5 ns |  8,269.6 ns | 453.29 ns |  0.90 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.139 μs |   7.697 μs |  0.4219 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   806.004 μs |  10.458 μs |  0.5733 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.433 μs |   7.022 μs |  0.3849 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,637.068 μs | 375.981 μs | 20.6088 μs |    1280 B |


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