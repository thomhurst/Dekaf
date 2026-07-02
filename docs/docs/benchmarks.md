---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-01 20:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error      | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-----------:|-----------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,130.93 μs** | **122.995 μs** |  **81.353 μs** |  **6,122.80 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,130.32 μs |  16.374 μs |  10.831 μs |  1,125.77 μs |  0.18 |    0.00 |        - |       - |   32.03 KB |        0.30 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,381.94 μs** |  **83.615 μs** |  **55.306 μs** |  **7,381.68 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,329.20 μs |  73.393 μs |  48.545 μs |  2,312.01 μs |  0.32 |    0.01 |  15.6250 |       - |  309.61 KB |        0.29 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,371.72 μs** |  **45.958 μs** |  **27.349 μs** |  **6,377.74 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,467.10 μs |  71.499 μs |  42.548 μs |  1,468.56 μs |  0.23 |    0.01 |        - |       - |   34.45 KB |        0.18 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,737.18 μs** | **695.482 μs** | **413.870 μs** | **12,603.75 μs** |  **1.00** |    **0.04** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,524.72 μs | 147.169 μs |  76.972 μs |  6,506.34 μs |  0.51 |    0.02 |  15.6250 |       - |   343.8 KB |        0.18 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.56 μs** |   **8.251 μs** |   **4.315 μs** |    **140.96 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **42.87 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.45 μs |   8.991 μs |   5.947 μs |     63.21 μs |  0.44 |    0.04 |   0.4883 |       - |   13.92 KB |        0.32 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,410.30 μs** |  **51.409 μs** |  **34.004 μs** |  **1,407.39 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **419.15 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    669.39 μs |  81.728 μs |  54.058 μs |    667.07 μs |  0.47 |    0.04 |   3.9063 |       - |  149.09 KB |        0.36 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |         **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    884.75 μs | 675.927 μs | 447.084 μs |  1,101.70 μs |     ? |       ? |   0.4883 |       - |   15.05 KB |           ? |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |         **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,774.00 μs | 207.051 μs | 108.292 μs |  2,741.36 μs |     ? |       ? |   7.8125 |       - |  209.18 KB |           ? |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,420.39 μs** |   **2.408 μs** |   **1.592 μs** |  **5,419.82 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,253.09 μs | 191.751 μs | 126.831 μs |  1,270.87 μs |  0.23 |    0.02 |        - |       - |    1.29 KB |        1.10 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,417.87 μs** |   **5.285 μs** |   **3.496 μs** |  **5,418.05 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,395.69 μs |  30.907 μs |  20.443 μs |  1,396.88 μs |  0.26 |    0.00 |        - |       - |    1.29 KB |        1.10 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,422.24 μs** |   **5.049 μs** |   **3.004 μs** |  **5,422.04 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,356.20 μs |  31.321 μs |  20.717 μs |  1,361.32 μs |  0.25 |    0.00 |        - |       - |    1.29 KB |        0.63 |
|                         |               |             |           |              |            |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,429.16 μs** |   **4.452 μs** |   **2.328 μs** |  **5,428.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,156.86 μs |  20.781 μs |  13.746 μs |  1,154.50 μs |  0.21 |    0.00 |        - |       - |    1.29 KB |        0.63 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ORZUYQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ORZUYQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|----------:|----------:|----------:|------------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.309 ms** | **4.1265 ms** | **0.6386 ms** | **1.000** |         **-** |         **-** |         **-** |    **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.873 ms | 7.6986 ms | 1.1914 ms | 0.005 |         - |         - |         - |  8797.61 KB |      117.90 |
|                      |            |              |             |              |           |           |       |           |           |           |             |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.307 ms** | **2.2993 ms** | **0.5971 ms** | **1.000** |         **-** |         **-** |         **-** |    **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.780 ms | 5.3280 ms | 1.3837 ms | 0.004 | 1000.0000 | 1000.0000 | 1000.0000 |  9230.65 KB |       36.86 |
|                      |            |              |             |              |           |           |       |           |           |           |             |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.824 ms** | **6.2815 ms** | **1.6313 ms** | **1.000** |         **-** |         **-** |         **-** |   **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.986 ms | 8.9025 ms | 2.3119 ms | 0.005 | 1000.0000 | 1000.0000 | 1000.0000 |  9446.56 KB |       15.69 |
|                      |            |              |             |              |           |           |       |           |           |           |             |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.620 ms** | **9.6075 ms** | **2.4950 ms** | **1.000** |         **-** |         **-** |         **-** |   **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.473 ms | 8.8636 ms | 2.3018 ms | 0.005 | 1000.0000 | 1000.0000 | 1000.0000 | 14015.91 KB |        5.92 |
|                      |            |              |             |              |           |           |       |           |           |           |             |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.318 ms** | **5.7558 ms** | **1.4948 ms** | **1.000** |         **-** |         **-** |         **-** |     **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.597 ms | 1.6761 ms | 0.4353 ms | 0.002 |         - |         - |         - |   355.81 KB |      147.87 |
|                      |            |              |             |              |           |           |       |           |           |           |             |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.262 ms** | **5.3864 ms** | **0.8336 ms** | **1.000** |         **-** |         **-** |         **-** |     **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.170 ms | 3.0216 ms | 0.7847 ms | 0.002 |         - |         - |         - |   805.79 KB |      193.51 |
|                      |            |              |             |              |           |           |       |           |           |           |             |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.359 ms** | **7.2896 ms** | **1.8931 ms** | **1.000** |         **-** |         **-** |         **-** |     **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.767 ms | 2.1927 ms | 0.5694 ms | 0.002 |         - |         - |         - |   813.05 KB |      337.89 |
|                      |            |              |             |              |           |           |       |           |           |           |             |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,154.401 ms** | **9.5297 ms** | **2.4748 ms** | **1.000** |         **-** |         **-** |         **-** |     **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.811 ms | 0.7764 ms | 0.2016 ms | 0.002 |         - |         - |         - |  2416.73 KB |      578.21 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 22.347 μs |  9.2238 μs | 5.4889 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.683 μs |  0.1863 μs | 0.1108 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 12.293 μs |  1.6312 μs | 1.0789 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 34.261 μs |  2.6444 μs | 1.5736 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 16.991 μs | 13.8854 μs | 8.2630 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 26.477 μs |  8.5485 μs | 5.0871 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.110 μs |  2.0675 μs | 1.2303 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  3.853 μs |  0.1059 μs | 0.0630 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.125 μs |  0.4344 μs | 0.2873 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,388.9 ns |  92.45 ns |  48.35 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,830.9 ns | 283.07 ns | 187.23 ns |  0.43 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,656.3 ns | 115.52 ns |  68.75 ns |  0.39 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,131.1 ns | 126.38 ns |  75.21 ns |  0.50 |    0.03 |     224 B |        0.48 |
| &#39;Serialize Int32&#39;                    |    609.9 ns |  82.21 ns |  43.00 ns |  0.14 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 32,602.3 ns | 552.57 ns | 365.49 ns |  7.71 |    0.40 |    3920 B |        8.45 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,238.0 ns | 351.27 ns | 232.34 ns |  1.00 |    0.07 |     464 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,405.9 ns | 216.07 ns | 128.58 ns |  0.81 |    0.05 |     280 B |        0.60 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.520 μs |  0.8321 μs |  0.4952 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   494.821 μs |  7.8657 μs |  4.6808 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.637 μs |  0.7052 μs |  0.4665 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,469.789 μs | 37.5546 μs | 24.8401 μs |    1280 B |


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