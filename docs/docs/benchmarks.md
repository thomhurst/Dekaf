---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 15:19 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|-------------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,135.51 μs** |    **839.94 μs** |  **46.040 μs** |  **6,125.49 μs** |  **1.00** |    **0.01** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,399.31 μs |  3,181.65 μs | 174.397 μs |  1,318.46 μs |  0.23 |    0.02 |        - |        - |   35.03 KB |        0.33 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,372.70 μs** |    **374.70 μs** |  **20.538 μs** |  **7,382.30 μs** |  **1.00** |    **0.00** |  **62.5000** |  **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,359.36 μs |    706.01 μs |  38.699 μs |  2,371.69 μs |  0.32 |    0.00 |  15.6250 |        - |  340.02 KB |        0.32 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,540.16 μs** |    **566.05 μs** |  **31.027 μs** |  **6,557.67 μs** |  **1.00** |    **0.01** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,680.47 μs | 14,106.20 μs | 773.209 μs |  1,329.28 μs |  0.26 |    0.10 |        - |        - |   37.23 KB |        0.19 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,245.34 μs** |  **1,256.93 μs** |  **68.896 μs** | **12,247.53 μs** |  **1.00** |    **0.01** | **109.3750** |  **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,644.27 μs |  8,107.54 μs | 444.402 μs |  6,775.78 μs |  0.54 |    0.03 |  15.6250 |        - |  372.16 KB |        0.19 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.98 μs** |     **69.08 μs** |   **3.787 μs** |    **133.71 μs** |  **1.00** |    **0.03** |   **2.4414** |        **-** |   **41.53 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     80.35 μs |     74.34 μs |   4.075 μs |     82.31 μs |  0.60 |    0.03 |   0.4883 |        - |   14.52 KB |        0.35 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,390.36 μs** |    **948.55 μs** |  **51.993 μs** |  **1,382.98 μs** |  **1.00** |    **0.05** |  **23.4375** |        **-** |  **421.44 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    761.64 μs |    509.59 μs |  27.932 μs |    776.46 μs |  0.55 |    0.02 |   3.9063 |        - |  123.83 KB |        0.29 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    371.24 μs |    769.07 μs |  42.155 μs |    371.06 μs |     ? |       ? |  12.6953 |  11.7188 |   273.8 KB |           ? |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,520.42 μs |  2,948.63 μs | 161.624 μs |  3,607.16 μs |     ? |       ? | 109.3750 | 101.5625 | 2449.04 KB |           ? |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,398.59 μs** |     **55.94 μs** |   **3.066 μs** |  **5,398.34 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,110.83 μs |     17.46 μs |   0.957 μs |  1,111.22 μs |  0.21 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,394.70 μs** |     **74.15 μs** |   **4.064 μs** |  **5,392.75 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,110.67 μs |     23.22 μs |   1.273 μs |  1,111.30 μs |  0.21 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,396.98 μs** |     **49.42 μs** |   **2.709 μs** |  **5,395.74 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,392.66 μs |    302.41 μs |  16.576 μs |  1,389.80 μs |  0.26 |    0.00 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |              |            |              |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,403.48 μs** |     **14.12 μs** |   **0.774 μs** |  **5,403.15 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,358.55 μs |    687.37 μs |  37.677 μs |  1,358.98 μs |  0.25 |    0.01 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.251 ms** | **15.7806 ms** | **0.8650 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.230 ms | 23.5868 ms | 1.2929 ms | 0.005 |  406.34 KB |        5.45 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.728 ms** | **34.6850 ms** | **1.9012 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.220 ms |  8.8941 ms | 0.4875 ms | 0.004 |  595.13 KB |        2.38 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.149 ms** | **19.4880 ms** | **1.0682 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.779 ms | 52.1567 ms | 2.8589 ms | 0.005 |  808.67 KB |        1.34 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.125 ms** | **16.3400 ms** | **0.8956 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.565 ms | 48.2981 ms | 2.6474 ms | 0.005 | 2576.64 KB |        1.09 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,153.989 ms** | **39.2287 ms** | **2.1503 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.618 ms |  6.2081 ms | 0.3403 ms | 0.002 |   183.7 KB |       76.34 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.796 ms** | **20.5803 ms** | **1.1281 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.254 ms |  0.7210 ms | 0.0395 ms | 0.002 |  183.22 KB |       44.00 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.866 ms** | **36.9312 ms** | **2.0243 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.502 ms |  7.9385 ms | 0.4351 ms | 0.002 |  182.87 KB |       76.00 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.307 ms** | **29.8925 ms** | **1.6385 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.108 ms | 32.5209 ms | 1.7826 ms | 0.002 |  183.87 KB |       43.99 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.536 μs |  2.484 μs | 0.1361 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.718 μs |  5.450 μs | 0.2987 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.206 μs |  3.973 μs | 0.2178 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.763 μs |  4.297 μs | 0.2355 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.891 μs |  1.703 μs | 0.0933 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.341 μs |  1.976 μs | 0.1083 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.408 μs | 10.884 μs | 0.5966 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.424 μs |  3.793 μs | 0.2079 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.649 μs |  1.104 μs | 0.0605 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.337 μs |  7.813 μs | 0.4282 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,815.2 ns | 10,528.9 ns | 577.13 ns |  0.44 |    0.12 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,345.8 ns |    459.1 ns |  25.17 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,416.3 ns |  1,695.1 ns |  92.92 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,554.3 ns |  4,121.6 ns | 225.92 ns |  0.62 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    782.8 ns |    680.3 ns |  37.29 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,717.7 ns |  1,921.3 ns | 105.31 ns | 10.09 |    0.22 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,137.0 ns |  1,895.9 ns | 103.92 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,005.0 ns |    904.3 ns |  49.57 ns |  0.97 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.234 μs |   4.550 μs |  0.2494 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   540.568 μs | 140.174 μs |  7.6834 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.355 μs |   2.259 μs |  0.1238 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,643.291 μs | 404.543 μs | 22.1744 μs |    1280 B |


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