---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 13:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,270.78 μs** |   **550.17 μs** |  **30.157 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,406.04 μs | 1,184.38 μs |  64.920 μs |  0.22 |    0.01 |        - |       - |   34.92 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,446.61 μs** | **1,472.54 μs** |  **80.715 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,396.06 μs | 1,049.51 μs |  57.527 μs |  0.32 |    0.01 |  15.6250 |       - |  339.82 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,155.70 μs** |   **190.03 μs** |  **10.416 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,345.08 μs | 2,509.52 μs | 137.555 μs |  0.22 |    0.02 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,872.46 μs** | **3,800.53 μs** | **208.320 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,589.13 μs | 5,757.68 μs | 315.598 μs |  0.51 |    0.02 |  15.6250 |       - |  369.42 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **160.71 μs** |   **275.87 μs** |  **15.121 μs** |  **1.01** |    **0.12** |   **2.4414** |       **-** |   **43.09 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     72.31 μs |   158.35 μs |   8.680 μs |  0.45 |    0.06 |   0.7324 |  0.4883 |   19.17 KB |        0.44 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,496.03 μs** |   **346.66 μs** |  **19.001 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |  **417.24 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    744.70 μs |   187.40 μs |  10.272 μs |  0.50 |    0.01 |   3.9063 |       - |  119.44 KB |        0.29 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    312.85 μs |   418.36 μs |  22.932 μs |     ? |       ? |   6.8359 |  5.8594 |  202.92 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,317.54 μs | 3,368.80 μs | 184.656 μs |     ? |       ? |  62.5000 | 54.6875 | 1853.35 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,439.62 μs** |    **99.14 μs** |   **5.434 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,297.26 μs |   663.57 μs |  36.373 μs |  0.24 |    0.01 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,480.62 μs** |   **571.40 μs** |  **31.320 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,169.06 μs |   479.83 μs |  26.301 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,499.55 μs** |   **285.08 μs** |  **15.626 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,112.74 μs |    26.99 μs |   1.480 μs |  0.20 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,441.89 μs** |    **10.85 μs** |   **0.595 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,116.34 μs |    77.06 μs |   4.224 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.60 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.526 ms** |  **42.316 ms** | **2.3195 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.511 ms |  22.849 ms | 1.2524 ms | 0.005 |  418.09 KB |        5.60 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.618 ms** |   **3.780 ms** | **0.2072 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.294 ms |  33.504 ms | 1.8365 ms | 0.005 |  590.71 KB |        2.36 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.051 ms** |  **24.399 ms** | **1.3374 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.647 ms | 135.871 ms | 7.4476 ms | 0.006 |  809.15 KB |        1.34 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.815 ms** |  **21.900 ms** | **1.2004 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.264 ms |   3.317 ms | 0.1818 ms | 0.005 | 2602.25 KB |        1.10 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.899 ms** |   **7.309 ms** | **0.4006 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.640 ms |   8.228 ms | 0.4510 ms | 0.002 |  194.95 KB |       81.02 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.291 ms** |  **22.034 ms** | **1.2078 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.344 ms |   1.756 ms | 0.0963 ms | 0.002 |  192.33 KB |       46.19 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.870 ms** |  **23.984 ms** | **1.3147 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     8.375 ms |  15.298 ms | 0.8385 ms | 0.003 |  226.49 KB |       94.13 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.915 ms** |   **7.785 ms** | **0.4267 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.242 ms |  39.204 ms | 2.1489 ms | 0.002 |  210.82 KB |       50.44 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 11.981 μs |  3.182 μs | 0.1744 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           |  7.542 μs |  4.721 μs | 0.2587 μs |         - |
| &#39;Write 100 CompactStrings&#39;                |  8.964 μs | 13.035 μs | 0.7145 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 25.311 μs |  3.633 μs | 0.1991 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  7.187 μs |  1.320 μs | 0.0723 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 17.180 μs |  7.702 μs | 0.4222 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 14.326 μs |  8.879 μs | 0.4867 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  3.542 μs |  6.060 μs | 0.3321 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; |  8.607 μs | 12.251 μs | 0.6715 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |    897.0 ns |  2,124.2 ns |   116.43 ns |  0.28 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,017.3 ns |  2,587.8 ns |   141.85 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,035.7 ns |  3,119.1 ns |   170.97 ns |  0.32 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  1,849.7 ns |  3,443.8 ns |   188.77 ns |  0.57 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    654.3 ns |  2,732.5 ns |   149.78 ns |  0.20 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 32,846.3 ns | 81,945.4 ns | 4,491.70 ns | 10.12 |    1.23 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,248.2 ns |  1,781.9 ns |    97.67 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  2,981.2 ns |  5,902.5 ns |   323.53 ns |  0.92 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     9.877 μs |  13.821 μs |  0.7576 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   400.346 μs | 206.307 μs | 11.3084 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     7.214 μs |  10.489 μs |  0.5749 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,174.531 μs | 374.326 μs | 20.5181 μs |    1280 B |


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