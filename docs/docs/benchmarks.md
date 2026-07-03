---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 16:44 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,254.85 μs** | **11,419.68 μs** | **625.951 μs** |  **1.01** |    **0.12** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,250.31 μs |    650.15 μs |  35.637 μs |  0.20 |    0.02 |        - |        - |   35.03 KB |        0.33 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,028.70 μs** |    **528.09 μs** |  **28.946 μs** |  **1.00** |    **0.01** |  **62.5000** |  **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,147.05 μs |    841.66 μs |  46.134 μs |  0.31 |    0.01 |  19.5313 |   3.9063 |  340.07 KB |        0.32 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,375.31 μs** |    **194.33 μs** |  **10.652 μs** |  **1.00** |    **0.00** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,418.17 μs |  4,314.13 μs | 236.472 μs |  0.22 |    0.03 |        - |        - |   37.23 KB |        0.19 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,159.42 μs** |  **2,195.36 μs** | **120.335 μs** |  **1.00** |    **0.02** | **109.3750** |  **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,555.74 μs |  2,704.39 μs | 148.237 μs |  0.50 |    0.02 |  15.6250 |        - |  372.22 KB |        0.19 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **135.70 μs** |    **306.18 μs** |  **16.783 μs** |  **1.01** |    **0.15** |   **2.1973** |        **-** |   **37.73 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    47.09 μs |     70.83 μs |   3.882 μs |  0.35 |    0.04 |   1.2207 |   1.0986 |   24.28 KB |        0.64 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,057.07 μs** |  **2,975.68 μs** | **163.107 μs** |  **1.02** |    **0.19** |  **26.3672** |        **-** |  **440.46 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   565.63 μs |    388.84 μs |  21.314 μs |  0.54 |    0.07 |   3.9063 |        - |  138.16 KB |        0.31 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   305.37 μs |    436.10 μs |  23.904 μs |     ? |       ? |  11.7188 |  10.7422 |  260.75 KB |           ? |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,907.26 μs |  2,801.65 μs | 153.568 μs |     ? |       ? | 117.1875 | 109.3750 | 2575.04 KB |           ? |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,331.05 μs** |     **59.98 μs** |   **3.288 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,398.12 μs |  2,055.98 μs | 112.695 μs |  0.26 |    0.02 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,371.50 μs** |  **1,263.93 μs** |  **69.280 μs** |  **1.00** |    **0.02** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,336.10 μs |  2,455.74 μs | 134.607 μs |  0.25 |    0.02 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,853.44 μs** |  **8,847.09 μs** | **484.939 μs** |  **1.00** |    **0.10** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,155.25 μs |  2,022.67 μs | 110.869 μs |  0.20 |    0.02 |        - |        - |     1.3 KB |        0.64 |
|                         |               |             |           |             |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,383.99 μs** |  **1,583.21 μs** |  **86.781 μs** |  **1.00** |    **0.02** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,282.51 μs |    346.59 μs |  18.998 μs |  0.24 |    0.00 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,164.911 ms** |  **16.662 ms** |  **0.9133 ms** | **3,165.429 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.975 ms | 225.503 ms | 12.3606 ms |    12.130 ms | 0.006 |  407.16 KB |        5.46 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.800 ms** |  **28.040 ms** |  **1.5370 ms** | **3,164.118 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    10.245 ms |  19.978 ms |  1.0951 ms |     9.827 ms | 0.003 |  590.65 KB |        2.36 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,163.889 ms** |  **11.196 ms** |  **0.6137 ms** | **3,163.734 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    12.562 ms |  59.050 ms |  3.2367 ms |    13.278 ms | 0.004 |  808.77 KB |        1.34 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,162.962 ms** |   **2.116 ms** |  **0.1160 ms** | **3,162.922 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    12.206 ms |  16.790 ms |  0.9203 ms |    12.407 ms | 0.004 | 2573.74 KB |        1.09 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.426 ms** |  **37.144 ms** |  **2.0360 ms** | **3,156.667 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.974 ms |  19.119 ms |  1.0480 ms |     6.313 ms | 0.002 |  182.31 KB |       75.77 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.035 ms** |  **10.403 ms** |  **0.5702 ms** | **3,156.025 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.653 ms |   4.934 ms |  0.2704 ms |     4.794 ms | 0.001 |  183.37 KB |       44.04 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.598 ms** |  **17.289 ms** |  **0.9477 ms** | **3,156.951 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     5.323 ms |   5.548 ms |  0.3041 ms |     5.273 ms | 0.002 |   182.8 KB |       75.97 |
|                      |            |              |             |              |            |            |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.999 ms** |  **14.508 ms** |  **0.7952 ms** | **3,156.930 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.101 ms |   5.933 ms |  0.3252 ms |     4.969 ms | 0.002 |  183.27 KB |       43.85 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.590 μs |   1.9253 μs |  0.1055 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.351 μs |  25.6400 μs |  1.4054 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  9.988 μs |   3.0138 μs |  0.1652 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.629 μs |   0.3055 μs |  0.0167 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.937 μs |   1.1038 μs |  0.0605 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.256 μs |   2.0825 μs |  0.1142 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 27.565 μs | 230.6284 μs | 12.6415 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.321 μs |   6.3911 μs |  0.3503 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.466 μs |   2.0775 μs |  0.1139 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.432 μs |   4.3949 μs |  0.2409 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,704.8 ns |  1,466.4 ns |  80.38 ns |  0.35 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,438.0 ns |  6,019.6 ns | 329.96 ns |  0.30 |    0.07 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,402.0 ns |  1,139.3 ns |  62.45 ns |  0.29 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,597.3 ns |    557.4 ns |  30.55 ns |  0.54 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    742.0 ns |    316.0 ns |  17.32 ns |  0.15 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,424.7 ns |  9,530.4 ns | 522.39 ns |  8.58 |    1.25 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,912.5 ns | 13,948.5 ns | 764.56 ns |  1.02 |    0.20 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,037.7 ns |  2,734.8 ns | 149.90 ns |  0.84 |    0.12 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.07 μs |   3.404 μs |  0.187 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   544.55 μs | 482.201 μs | 26.431 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.41 μs |  13.767 μs |  0.755 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,637.98 μs | 181.713 μs |  9.960 μs |    1280 B |


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