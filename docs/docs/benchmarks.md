---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 17:44 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,099.03 μs** |   **784.72 μs** |  **43.013 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,452.57 μs | 2,372.00 μs | 130.017 μs |  0.24 |    0.02 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,149.72 μs** |   **416.07 μs** |  **22.806 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,382.02 μs | 2,084.75 μs | 114.272 μs |  0.33 |    0.01 |  15.6250 |       - |   339.3 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,648.20 μs** |   **357.31 μs** |  **19.585 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,173.30 μs |   988.62 μs |  54.190 μs |  0.18 |    0.01 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,681.91 μs** | **3,900.51 μs** | **213.800 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,756.18 μs |   473.21 μs |  25.938 μs |  0.49 |    0.01 |  15.6250 |       - |  361.71 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **151.32 μs** |   **208.27 μs** |  **11.416 μs** |  **1.00** |    **0.09** |   **2.4414** |       **-** |   **40.83 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.04 μs |   203.81 μs |  11.172 μs |  0.41 |    0.07 |   0.2441 |       - |    9.29 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,357.16 μs** | **3,372.06 μs** | **184.834 μs** |  **1.01** |    **0.17** |  **23.4375** |       **-** |  **405.66 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    574.07 μs |   273.31 μs |  14.981 μs |  0.43 |    0.05 |        - |       - |   80.99 KB |        0.20 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    220.63 μs |   277.55 μs |  15.213 μs |     ? |       ? |   0.4883 |       - |   12.27 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,067.82 μs | 3,321.66 μs | 182.072 μs |     ? |       ? |   7.8125 |       - |  966.78 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,402.20 μs** |    **69.46 μs** |   **3.807 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,346.90 μs |   259.84 μs |  14.243 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,403.53 μs** |    **87.72 μs** |   **4.808 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,346.11 μs |   222.49 μs |  12.195 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,400.68 μs** |    **26.77 μs** |   **1.467 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.24 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,371.95 μs |   403.77 μs |  22.132 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.51 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,402.70 μs** |    **44.23 μs** |   **2.424 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,381.91 μs |   132.87 μs |   7.283 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.565 ms** |  **31.064 ms** | **1.7027 ms** | **3,168.680 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.196 ms |  10.620 ms | 0.5821 ms |    16.864 ms | 0.005 |  599.95 KB |        8.04 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.805 ms** |   **9.635 ms** | **0.5281 ms** | **3,166.032 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.621 ms |  12.528 ms | 0.6867 ms |    13.276 ms | 0.004 |  779.69 KB |        3.11 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.926 ms** |  **28.342 ms** | **1.5535 ms** | **3,166.209 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.582 ms | 159.536 ms | 8.7447 ms |    14.878 ms | 0.006 |  1069.8 KB |        1.78 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.418 ms** |  **16.958 ms** | **0.9295 ms** | **3,165.449 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.436 ms |  47.008 ms | 2.5767 ms |    16.834 ms | 0.005 | 2766.19 KB |        1.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.003 ms** |  **28.361 ms** | **1.5545 ms** | **3,157.296 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.645 ms |  12.654 ms | 0.6936 ms |     6.500 ms | 0.002 |  188.09 KB |       78.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.995 ms** |  **28.002 ms** | **1.5349 ms** | **3,157.295 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.073 ms |  14.706 ms | 0.8061 ms |     6.188 ms | 0.002 |  199.96 KB |       48.02 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.829 ms** |  **38.539 ms** | **2.1124 ms** | **3,157.078 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.907 ms |  39.469 ms | 2.1634 ms |     6.671 ms | 0.003 |  256.66 KB |      106.66 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.222 ms** |   **6.090 ms** | **0.3338 ms** | **3,157.049 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.384 ms |   8.643 ms | 0.4738 ms |     6.603 ms | 0.002 |  259.24 KB |       62.02 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.871 μs |  4.350 μs | 0.2384 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.337 μs |  2.130 μs | 0.1168 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.847 μs | 35.126 μs | 1.9254 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 35.694 μs |  3.737 μs | 0.2049 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.946 μs |  2.111 μs | 0.1157 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.278 μs |  1.323 μs | 0.0725 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.039 μs | 33.350 μs | 1.8281 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.312 μs |  9.849 μs | 0.5399 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.739 μs | 12.514 μs | 0.6859 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.840 μs |  5.399 μs | 0.2960 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,626.7 ns |  1,525.3 ns |    83.61 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,446.7 ns |  3,633.2 ns |   199.15 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,513.2 ns |  1,136.4 ns |    62.29 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,185.5 ns | 15,103.3 ns |   827.86 ns |  0.71 |    0.16 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    721.5 ns |    547.3 ns |    30.00 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 51,624.0 ns | 98,927.5 ns | 5,422.55 ns | 11.48 |    1.11 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,501.8 ns |  3,103.2 ns |   170.10 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,588.7 ns |  6,826.2 ns |   374.17 ns |  1.02 |    0.08 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev      | Allocated |
|------------------------ |-------------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.407 μs |     9.790 μs |   0.5366 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   565.808 μs |   383.980 μs |  21.0472 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.113 μs |     5.975 μs |   0.3275 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,786.280 μs | 3,020.124 μs | 165.5432 μs |    1280 B |


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