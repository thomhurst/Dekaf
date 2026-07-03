---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 20:25 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,092.99 μs** |   **251.43 μs** |  **13.782 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,435.48 μs | 2,245.45 μs | 123.081 μs |  0.24 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,188.54 μs** | **1,417.60 μs** |  **77.703 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,299.02 μs |   281.29 μs |  15.418 μs |  0.32 |    0.00 |  15.6250 |       - |  339.57 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,646.87 μs** |   **245.89 μs** |  **13.478 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,191.92 μs |   606.38 μs |  33.238 μs |  0.18 |    0.00 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,545.36 μs** | **5,478.40 μs** | **300.290 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,654.36 μs |   373.50 μs |  20.473 μs |  0.49 |    0.01 |  15.6250 |       - |  361.58 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.37 μs** |    **65.29 μs** |   **3.579 μs** |  **1.00** |    **0.03** |   **2.6855** |       **-** |   **43.89 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     61.89 μs |   349.02 μs |  19.131 μs |  0.43 |    0.12 |   0.2441 |       - |    8.74 KB |        0.20 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,151.15 μs** |   **206.88 μs** |  **11.340 μs** |  **1.00** |    **0.01** |  **23.4375** |       **-** |  **412.34 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    636.21 μs |   265.35 μs |  14.545 μs |  0.55 |    0.01 |        - |       - |   42.04 KB |        0.10 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    239.68 μs | 1,466.75 μs |  80.398 μs |     ? |       ? |   0.4883 |       - |    11.8 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,076.26 μs | 6,851.16 μs | 375.535 μs |     ? |       ? |   7.8125 |       - | 1010.15 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,408.30 μs** |   **111.75 μs** |   **6.125 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,360.48 μs |   601.19 μs |  32.953 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,456.14 μs** |   **200.47 μs** |  **10.988 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,323.21 μs |   398.20 μs |  21.827 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,419.92 μs** |   **245.04 μs** |  **13.432 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,107.75 μs |    39.37 μs |   2.158 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,425.13 μs** |   **459.53 μs** |  **25.188 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,374.27 μs |   209.77 μs |  11.498 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.344 ms** | **16.680 ms** | **0.9143 ms** | **3,170.040 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.727 ms | 18.039 ms | 0.9888 ms |    16.750 ms | 0.005 |  597.48 KB |        8.01 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.947 ms** | **49.676 ms** | **2.7229 ms** | **3,166.574 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.605 ms | 17.682 ms | 0.9692 ms |    16.103 ms | 0.005 |  776.85 KB |        3.10 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.167 ms** | **17.102 ms** | **0.9374 ms** | **3,166.602 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.518 ms | 36.593 ms | 2.0058 ms |    15.987 ms | 0.005 |  994.46 KB |        1.65 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.746 ms** |  **8.900 ms** | **0.4878 ms** | **3,166.580 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.622 ms | 48.188 ms | 2.6413 ms |    16.978 ms | 0.006 | 2762.98 KB |        1.17 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.892 ms** | **42.387 ms** | **2.3234 ms** | **3,156.086 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.734 ms | 27.329 ms | 1.4980 ms |     5.939 ms | 0.002 |  194.84 KB |       80.97 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.787 ms** | **21.325 ms** | **1.1689 ms** | **3,155.416 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.721 ms |  2.207 ms | 0.1210 ms |     5.786 ms | 0.002 |   186.3 KB |       44.74 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.661 ms** | **23.834 ms** | **1.3064 ms** | **3,157.337 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.190 ms | 13.538 ms | 0.7421 ms |     6.954 ms | 0.002 |  184.64 KB |       76.73 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.466 ms** |  **9.086 ms** | **0.4980 ms** | **3,156.702 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.411 ms | 53.109 ms | 2.9111 ms |     5.853 ms | 0.002 |  197.37 KB |       47.22 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.711 μs |   1.183 μs |  0.0649 μs | 15.739 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 16.203 μs | 196.617 μs | 10.7773 μs | 10.421 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.168 μs |   4.389 μs |  0.2406 μs | 11.161 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.435 μs |   1.188 μs |  0.0651 μs | 32.439 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.901 μs |   5.021 μs |  0.2752 μs |  8.818 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.121 μs |   3.379 μs |  0.1852 μs | 21.132 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.294 μs |  11.626 μs |  0.6373 μs | 18.187 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.005 μs |  25.707 μs |  1.4091 μs | 20.721 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.021 μs |   6.878 μs |  0.3770 μs |  4.878 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.197 μs |  23.551 μs |  1.2909 μs | 12.643 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,584.3 ns |  2,954.9 ns |   161.97 ns |  0.38 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,262.7 ns |  4,846.5 ns |   265.65 ns |  0.30 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,181.7 ns |  4,082.7 ns |   223.79 ns |  0.29 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,438.8 ns |  5,244.2 ns |   287.45 ns |  0.59 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    810.8 ns |  1,455.0 ns |    79.75 ns |  0.20 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,692.5 ns | 18,993.6 ns | 1,041.10 ns |  8.62 |    0.42 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,148.2 ns |  3,727.0 ns |   204.29 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,239.7 ns |  4,171.4 ns |   228.65 ns |  1.02 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.096 μs |  39.545 μs |  2.1676 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   588.378 μs | 418.442 μs | 22.9362 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.357 μs |   9.866 μs |  0.5408 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,568.027 μs | 433.692 μs | 23.7721 μs |    1280 B |


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