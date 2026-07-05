---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 18:34 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,108.86 μs** |   **546.32 μs** |  **29.946 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,369.87 μs | 2,998.20 μs | 164.342 μs |  0.22 |    0.02 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,363.40 μs** | **1,113.49 μs** |  **61.034 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,255.73 μs |   425.80 μs |  23.340 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.35 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,427.10 μs** | **1,982.27 μs** | **108.655 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,214.77 μs | 1,098.16 μs |  60.194 μs |  0.19 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,224.66 μs** | **2,090.38 μs** | **114.581 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,146.92 μs | 2,590.45 μs | 141.991 μs |  0.50 |    0.01 |  15.6250 |       - |  361.78 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.24 μs** |    **38.11 μs** |   **2.089 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.23 μs |   184.10 μs |  10.091 μs |  0.43 |    0.06 |   0.2441 |       - |    9.04 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,377.09 μs** | **2,965.66 μs** | **162.558 μs** |  **1.01** |    **0.15** |  **23.4375** |       **-** |  **412.23 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    627.76 μs | 1,153.22 μs |  63.212 μs |  0.46 |    0.06 |        - |       - |   67.86 KB |        0.16 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    199.44 μs |   219.79 μs |  12.047 μs |     ? |       ? |   0.9766 |       - |  109.22 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,984.89 μs | 3,249.30 μs | 178.105 μs |     ? |       ? |   7.8125 |       - |  975.81 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,407.92 μs** |    **29.83 μs** |   **1.635 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,114.62 μs |    40.70 μs |   2.231 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,411.96 μs** |   **145.17 μs** |   **7.957 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,115.69 μs |    39.55 μs |   2.168 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,417.53 μs** |    **30.20 μs** |   **1.656 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,114.67 μs |    19.45 μs |   1.066 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,416.44 μs** |    **94.34 μs** |   **5.171 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,114.99 μs |    53.81 μs |   2.949 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.749 ms** | **11.556 ms** | **0.6334 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.539 ms | 64.740 ms | 3.5486 ms | 0.006 |  597.58 KB |        8.01 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.802 ms** | **21.278 ms** | **1.1663 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.354 ms | 17.167 ms | 0.9410 ms | 0.004 |  788.69 KB |        3.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.084 ms** |  **3.217 ms** | **0.1763 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.595 ms | 31.188 ms | 1.7095 ms | 0.005 |  998.27 KB |        1.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.230 ms** |  **4.186 ms** | **0.2295 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.419 ms | 35.038 ms | 1.9205 ms | 0.005 | 2765.29 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.165 ms** | **19.036 ms** | **1.0434 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.498 ms |  5.710 ms | 0.3130 ms | 0.002 |  193.62 KB |       80.46 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.035 ms** |  **6.672 ms** | **0.3657 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.733 ms |  9.579 ms | 0.5251 ms | 0.002 |  197.72 KB |       47.48 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.427 ms** | **17.785 ms** | **0.9748 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.993 ms |  8.985 ms | 0.4925 ms | 0.002 |  184.73 KB |       76.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.727 ms** | **21.183 ms** | **1.1611 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.361 ms | 25.611 ms | 1.4038 ms | 0.002 |   187.1 KB |       44.76 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.182 μs |  8.189 μs | 0.4489 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.536 μs |  6.450 μs | 0.3536 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.501 μs |  3.434 μs | 0.1882 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 29.613 μs | 84.242 μs | 4.6176 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.494 μs | 69.522 μs | 3.8107 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.257 μs |  2.104 μs | 0.1153 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.759 μs | 32.766 μs | 1.7960 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.980 μs |  9.113 μs | 0.4995 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.901 μs |  3.940 μs | 0.2160 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.687 μs |  3.975 μs | 0.2179 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  2,030.2 ns |  6,090.1 ns |   333.82 ns |  0.50 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,273.0 ns |    912.2 ns |    50.00 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,484.2 ns |    459.1 ns |    25.17 ns |  0.36 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,761.0 ns |  1,925.3 ns |   105.53 ns |  0.68 |    0.04 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    705.3 ns |    379.8 ns |    20.82 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 43,500.3 ns | 53,082.8 ns | 2,909.64 ns | 10.64 |    0.78 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,096.7 ns |  4,012.8 ns |   219.96 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,032.5 ns |  1,672.1 ns |    91.65 ns |  0.99 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.82 μs |  22.704 μs |  1.244 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   933.35 μs |  78.641 μs |  4.311 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.17 μs |   8.414 μs |  0.461 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,659.63 μs | 523.052 μs | 28.670 μs |    1280 B |


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