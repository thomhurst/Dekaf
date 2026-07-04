---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 22:00 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,138.00 μs** |   **380.44 μs** |  **20.853 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,296.77 μs |   717.50 μs |  39.329 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,378.57 μs** |   **951.18 μs** |  **52.137 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,351.10 μs |   614.52 μs |  33.684 μs |  0.32 |    0.00 |  15.6250 |       - |  339.42 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,211.56 μs** |   **373.74 μs** |  **20.486 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,239.36 μs | 1,180.70 μs |  64.718 μs |  0.20 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,808.90 μs** | **5,370.04 μs** | **294.350 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,571.69 μs | 4,474.22 μs | 245.247 μs |  0.51 |    0.02 |  15.6250 |       - |  361.72 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.66 μs** |    **71.29 μs** |   **3.908 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.18 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.00 μs |   103.93 μs |   5.697 μs |  0.44 |    0.04 |        - |       - |    8.17 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,411.86 μs** | **1,989.16 μs** | **109.032 μs** |  **1.00** |    **0.09** |  **23.4375** |       **-** |  **426.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    631.53 μs |   176.50 μs |   9.674 μs |  0.45 |    0.03 |        - |       - |   63.69 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    198.68 μs |   114.90 μs |   6.298 μs |     ? |       ? |   0.9766 |       - |   97.94 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,263.33 μs | 7,392.97 μs | 405.234 μs |     ? |       ? |   7.8125 |       - | 1000.98 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,436.21 μs** |   **452.24 μs** |  **24.789 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,394.16 μs |   401.29 μs |  21.996 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,421.97 μs** |    **72.66 μs** |   **3.983 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,383.38 μs |   442.39 μs |  24.249 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,428.23 μs** |   **311.09 μs** |  **17.052 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,403.32 μs |   586.51 μs |  32.149 μs |  0.26 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,424.30 μs** |   **129.38 μs** |   **7.092 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,380.89 μs |   538.05 μs |  29.492 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.603 ms** |   **6.023 ms** | **0.3302 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.447 ms |  34.641 ms | 1.8988 ms | 0.005 |  591.98 KB |        7.93 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.095 ms** |  **15.500 ms** | **0.8496 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.485 ms |  61.987 ms | 3.3977 ms | 0.005 |  775.57 KB |        3.10 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.492 ms** |  **14.400 ms** | **0.7893 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.338 ms | 117.655 ms | 6.4491 ms | 0.006 |   994.9 KB |        1.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.340 ms** |   **5.483 ms** | **0.3005 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.881 ms |  18.808 ms | 1.0309 ms | 0.005 | 2760.81 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.864 ms** |  **26.151 ms** | **1.4334 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.998 ms |  10.752 ms | 0.5894 ms | 0.002 |  184.41 KB |       76.64 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.334 ms** |  **25.219 ms** | **1.3824 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.458 ms |   7.828 ms | 0.4291 ms | 0.002 |  186.48 KB |       44.78 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.411 ms** |  **14.685 ms** | **0.8049 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.927 ms |  28.818 ms | 1.5796 ms | 0.003 |  256.59 KB |      106.63 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.707 ms** |  **12.112 ms** | **0.6639 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.948 ms |  12.235 ms | 0.6706 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.112 μs |  4.4073 μs | 0.2416 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.306 μs |  4.8790 μs | 0.2674 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.422 μs |  3.0107 μs | 0.1650 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.796 μs | 12.4781 μs | 0.6840 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.984 μs |  0.5931 μs | 0.0325 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.163 μs | 76.0907 μs | 4.1708 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.218 μs | 16.1607 μs | 0.8858 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.460 μs | 29.1777 μs | 1.5993 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.268 μs | 18.0236 μs | 0.9879 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.477 μs | 10.6053 μs | 0.5813 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,582.8 ns |  6,829.1 ns |   374.33 ns |  0.38 |    0.08 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,232.0 ns |  1,621.5 ns |    88.88 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,633.3 ns |  2,892.7 ns |   158.56 ns |  0.39 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,544.0 ns |  1,493.3 ns |    81.85 ns |  0.61 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    842.3 ns |  3,802.4 ns |   208.42 ns |  0.20 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,585.3 ns | 30,398.7 ns | 1,666.26 ns | 10.24 |    0.42 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,160.8 ns |  1,950.7 ns |   106.93 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,539.7 ns | 14,832.9 ns |   813.04 ns |  1.09 |    0.17 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    18.173 μs |  35.155 μs |  1.927 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   929.288 μs | 210.230 μs | 11.523 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.991 μs |  27.260 μs |  1.494 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,680.242 μs | 330.597 μs | 18.121 μs |    1280 B |


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