---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 19:12 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,263.50 μs** |    **941.56 μs** |  **51.610 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,364.25 μs |  1,968.13 μs | 107.880 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,298.09 μs** |  **1,768.41 μs** |  **96.932 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,230.36 μs |    182.62 μs |  10.010 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.43 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **7,085.69 μs** | **15,286.57 μs** | **837.909 μs** |  **1.01** |    **0.14** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,156.42 μs |    401.02 μs |  21.981 μs |  0.16 |    0.02 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,636.35 μs** |  **1,697.73 μs** |  **93.058 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,724.76 μs |  1,508.07 μs |  82.663 μs |  0.49 |    0.01 |  15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.10 μs** |    **175.39 μs** |   **9.613 μs** |  **1.00** |    **0.08** |   **2.4414** |       **-** |   **40.99 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     57.79 μs |    109.35 μs |   5.994 μs |  0.41 |    0.04 |   0.2441 |       - |    11.2 KB |        0.27 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,359.17 μs** |    **563.87 μs** |  **30.908 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |  **420.24 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    623.24 μs |    244.60 μs |  13.408 μs |  0.46 |    0.01 |        - |       - |   79.43 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    215.70 μs |    211.66 μs |  11.602 μs |     ? |       ? |   0.4883 |       - |   13.68 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,031.25 μs |  3,335.86 μs | 182.850 μs |     ? |       ? |   7.8125 |       - |  975.36 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,408.19 μs** |    **189.21 μs** |  **10.371 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,320.04 μs |    412.84 μs |  22.629 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,405.05 μs** |     **78.01 μs** |   **4.276 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,346.81 μs |    175.03 μs |   9.594 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,409.03 μs** |    **129.12 μs** |   **7.078 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,361.06 μs |    214.02 μs |  11.731 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,412.24 μs** |     **75.18 μs** |   **4.121 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,107.08 μs |     65.79 μs |   3.606 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.243 ms** |  **3.734 ms** | **0.2047 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.117 ms | 51.810 ms | 2.8399 ms | 0.005 |  599.86 KB |        8.04 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.014 ms** | **38.678 ms** | **2.1201 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.455 ms | 20.585 ms | 1.1283 ms | 0.005 |  775.91 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.078 ms** | **16.127 ms** | **0.8840 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.393 ms | 20.429 ms | 1.1198 ms | 0.005 |  994.84 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.829 ms** | **13.851 ms** | **0.7592 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.459 ms | 27.884 ms | 1.5284 ms | 0.005 | 2759.74 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.800 ms** |  **7.739 ms** | **0.4242 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.058 ms |  3.541 ms | 0.1941 ms | 0.002 |   186.7 KB |       77.59 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.848 ms** | **18.088 ms** | **0.9915 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.565 ms |  2.573 ms | 0.1410 ms | 0.002 |  190.97 KB |       45.86 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.208 ms** | **27.426 ms** | **1.5033 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.450 ms | 14.564 ms | 0.7983 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.944 ms** | **12.662 ms** | **0.6940 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.909 ms | 21.489 ms | 1.1779 ms | 0.002 |  187.59 KB |       44.88 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.674 μs |   2.229 μs |  0.1222 μs | 25.648 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.794 μs |   5.466 μs |  0.2996 μs | 10.851 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.754 μs |   8.413 μs |  0.4611 μs | 10.741 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.631 μs |   1.961 μs |  0.1075 μs | 26.575 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.844 μs |   1.951 μs |  0.1069 μs |  8.787 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.276 μs | 187.183 μs | 10.2601 μs | 20.558 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.956 μs |   6.703 μs |  0.3674 μs | 17.998 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.225 μs |   5.553 μs |  0.3044 μs | 24.065 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.153 μs |  10.712 μs |  0.5872 μs |  4.819 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.799 μs |   2.477 μs |  0.1358 μs | 10.789 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.187 μs |  1.435 μs | 0.0787 μs |  0.29 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.673 μs |  3.340 μs | 0.1831 μs |  0.42 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.530 μs |  6.187 μs | 0.3391 μs |  0.38 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.498 μs |  1.069 μs | 0.0586 μs |  0.62 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.055 μs |  3.805 μs | 0.2086 μs |  0.26 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46.373 μs | 79.570 μs | 4.3615 μs | 11.51 |    0.95 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.028 μs |  1.142 μs | 0.0626 μs |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.119 μs |  6.149 μs | 0.3371 μs |  1.02 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Allocated |
|------------------------ |-------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.197 μs |  8.595 μs | 0.4711 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   505.009 μs | 93.350 μs | 5.1168 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.802 μs | 14.164 μs | 0.7764 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,832.174 μs | 76.900 μs | 4.2151 μs |    1280 B |


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