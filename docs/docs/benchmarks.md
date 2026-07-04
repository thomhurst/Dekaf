---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 21:38 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,155.00 μs** |   **613.00 μs** |  **33.601 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,304.80 μs | 1,066.75 μs |  58.472 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,209.04 μs** |   **763.10 μs** |  **41.828 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,291.00 μs |   344.25 μs |  18.870 μs |  0.32 |    0.00 |  15.6250 |       - |  339.48 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,613.94 μs** |   **650.94 μs** |  **35.680 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,239.17 μs | 2,330.28 μs | 127.730 μs |  0.19 |    0.02 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,609.40 μs** | **4,495.02 μs** | **246.387 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,785.69 μs | 7,445.20 μs | 408.097 μs |  0.58 |    0.03 |  15.6250 |       - |  361.65 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.01 μs** |    **57.52 μs** |   **3.153 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |    **42.1 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     57.38 μs |    89.59 μs |   4.911 μs |  0.46 |    0.04 |   0.2441 |       - |   10.01 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,263.52 μs** |   **140.06 μs** |   **7.677 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **419.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    677.45 μs |   845.26 μs |  46.332 μs |  0.54 |    0.03 |        - |       - |   88.62 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    195.29 μs |   189.46 μs |  10.385 μs |     ? |       ? |   0.4883 |       - |   12.03 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,967.71 μs | 1,279.01 μs |  70.107 μs |     ? |       ? |   7.8125 |       - |  950.81 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,407.07 μs** |   **167.15 μs** |   **9.162 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,341.47 μs |   401.22 μs |  21.992 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,408.33 μs** |    **58.07 μs** |   **3.183 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,343.26 μs |   419.48 μs |  22.993 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,409.78 μs** |    **68.68 μs** |   **3.765 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,361.34 μs |   428.28 μs |  23.475 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,415.32 μs** |    **67.75 μs** |   **3.714 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,352.23 μs |   465.91 μs |  25.538 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,153.101 ms** | **535.499 ms** | **29.3525 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.158 ms |  23.867 ms |  1.3082 ms | 0.005 |  593.27 KB |        7.95 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.912 ms** |   **4.074 ms** |  **0.2233 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.833 ms |  17.965 ms |  0.9847 ms | 0.005 |  777.01 KB |        3.10 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.923 ms** |  **31.344 ms** |  **1.7180 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.122 ms |  38.966 ms |  2.1359 ms | 0.005 | 1108.73 KB |        1.84 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.765 ms** |  **15.991 ms** |  **0.8765 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.537 ms |  27.687 ms |  1.5176 ms | 0.005 | 2760.77 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.524 ms** |  **11.142 ms** |  **0.6108 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.291 ms |  19.441 ms |  1.0657 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.875 ms** |  **37.378 ms** |  **2.0488 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.938 ms |  16.579 ms |  0.9087 ms | 0.002 |  196.93 KB |       47.29 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.980 ms** |  **30.444 ms** |  **1.6688 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.211 ms |  12.783 ms |  0.7007 ms | 0.002 |  256.62 KB |      106.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.907 ms** |  **36.019 ms** |  **1.9743 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.991 ms |   7.646 ms |  0.4191 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 34.983 μs | 288.832 μs | 15.8319 μs | 25.883 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.596 μs |  27.800 μs |  1.5238 μs | 10.741 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.431 μs |   3.854 μs |  0.2113 μs | 10.405 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.923 μs |   1.942 μs |  0.1064 μs | 27.907 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.096 μs |   1.530 μs |  0.0839 μs |  9.053 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.514 μs |  39.251 μs |  2.1515 μs | 20.378 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.127 μs |   6.561 μs |  0.3596 μs | 17.993 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 29.946 μs | 306.305 μs | 16.7896 μs | 20.389 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.589 μs |   4.005 μs |  0.2195 μs |  4.680 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.791 μs |   4.844 μs |  0.2655 μs | 10.691 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,850.3 ns |  3,596.4 ns | 197.13 ns |  0.43 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,428.0 ns |    647.8 ns |  35.51 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,415.7 ns |  2,011.8 ns | 110.27 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,162.8 ns |  7,436.7 ns | 407.63 ns |  0.73 |    0.09 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    761.0 ns |    182.4 ns |  10.00 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,963.7 ns | 12,251.1 ns | 671.52 ns |  9.95 |    0.65 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,334.0 ns |  5,673.4 ns | 310.98 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,081.0 ns |  1,837.7 ns | 100.73 ns |  0.94 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.429 μs |   1.724 μs |  0.0945 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   537.609 μs | 442.378 μs | 24.2482 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.250 μs |   1.420 μs |  0.0778 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,634.580 μs |  96.456 μs |  5.2871 μs |    1280 B |


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