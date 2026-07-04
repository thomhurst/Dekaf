---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 09:18 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,154.93 μs** | **1,344.82 μs** |  **73.714 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,328.68 μs | 3,375.40 μs | 185.017 μs |  0.22 |    0.03 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,347.06 μs** | **1,374.97 μs** |  **75.367 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,333.68 μs |   831.41 μs |  45.572 μs |  0.32 |    0.01 |  15.6250 |       - |  339.45 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,512.83 μs** | **1,494.79 μs** |  **81.934 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,429.72 μs | 5,848.45 μs | 320.573 μs |  0.22 |    0.04 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,156.75 μs** |   **489.95 μs** |  **26.856 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,854.57 μs | 1,296.93 μs |  71.089 μs |  0.48 |    0.01 |  15.6250 |       - |  361.78 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.56 μs** |    **44.15 μs** |   **2.420 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.23 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.93 μs |    37.04 μs |   2.030 μs |  0.45 |    0.01 |        - |       - |    7.21 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,411.99 μs** | **1,746.78 μs** |  **95.747 μs** |  **1.00** |    **0.08** |  **23.4375** |       **-** |  **412.25 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    649.30 μs |   739.33 μs |  40.525 μs |  0.46 |    0.04 |        - |       - |   76.83 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    207.35 μs |   323.17 μs |  17.714 μs |     ? |       ? |   0.9766 |       - |  101.08 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,454.27 μs | 8,595.27 μs | 471.135 μs |     ? |       ? |   7.8125 |       - | 1004.93 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,390.00 μs** |    **28.91 μs** |   **1.585 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,108.88 μs |    11.00 μs |   0.603 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,392.34 μs** |    **57.72 μs** |   **3.164 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,111.40 μs |    21.65 μs |   1.187 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,412.79 μs** |   **329.41 μs** |  **18.056 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,327.34 μs |   253.32 μs |  13.886 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.20 μs** |    **43.80 μs** |   **2.401 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,383.31 μs |   407.76 μs |  22.351 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.228 ms** | **45.5841 ms** | **2.4986 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.211 ms | 52.0807 ms | 2.8547 ms | 0.005 | 601.84 KB |        8.07 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.143 ms** | **14.4175 ms** | **0.7903 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.958 ms | 50.7541 ms | 2.7820 ms | 0.005 | 775.56 KB |        3.10 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.987 ms** | **22.0534 ms** | **1.2088 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.326 ms | 64.1091 ms | 3.5140 ms | 0.006 | 994.74 KB |        1.65 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.620 ms** |  **0.8871 ms** | **0.0486 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.271 ms | 88.7231 ms | 4.8632 ms | 0.005 | 2771.6 KB |        1.17 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.956 ms** | **29.8501 ms** | **1.6362 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.617 ms | 10.7623 ms | 0.5899 ms | 0.002 | 184.38 KB |       76.62 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.636 ms** | **31.2189 ms** | **1.7112 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.204 ms |  9.0716 ms | 0.4972 ms | 0.002 | 186.29 KB |       44.74 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.650 ms** | **33.1933 ms** | **1.8194 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     5.983 ms |  6.9309 ms | 0.3799 ms | 0.002 | 184.56 KB |       76.70 |
|                      |            |              |             |              |            |           |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.026 ms** | **15.7868 ms** | **0.8653 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.813 ms | 63.3445 ms | 3.4721 ms | 0.002 | 268.05 KB |       64.13 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.845 μs |  0.5555 μs | 0.0304 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.055 μs |  3.1980 μs | 0.1753 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.180 μs |  1.7499 μs | 0.0959 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.733 μs |  3.4821 μs | 0.1909 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.939 μs |  1.8989 μs | 0.1041 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.307 μs |  0.9122 μs | 0.0500 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.576 μs | 12.7078 μs | 0.6966 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.980 μs |  8.0288 μs | 0.4401 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.582 μs |  3.0327 μs | 0.1662 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.432 μs |  7.1676 μs | 0.3929 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,792.8 ns |   2,689.3 ns |    147.41 ns |  1,823.5 ns |  0.40 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,270.8 ns |   1,950.7 ns |    106.93 ns |  1,327.5 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,335.0 ns |   3,349.0 ns |    183.57 ns |  1,271.0 ns |  0.30 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,564.3 ns |   1,631.8 ns |     89.44 ns |  2,534.0 ns |  0.58 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    747.3 ns |   2,192.3 ns |    120.17 ns |    740.0 ns |  0.17 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 52,000.5 ns | 380,508.6 ns | 20,856.96 ns | 40,195.5 ns | 11.74 |    4.23 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,463.7 ns |   9,027.3 ns |    494.81 ns |  4,183.0 ns |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,730.7 ns |   5,254.1 ns |    287.99 ns |  3,687.0 ns |  0.84 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.033 μs |   6.420 μs |  0.3519 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   557.357 μs |  91.817 μs |  5.0328 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.242 μs |   3.386 μs |  0.1856 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,695.852 μs | 253.548 μs | 13.8978 μs |    1280 B |


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