---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 15:40 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,121.57 μs** |   **746.26 μs** |  **40.905 μs** |  **1.00** |    **0.01** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,403.17 μs | 1,667.97 μs |  91.427 μs |  0.23 |    0.01 |        - |        - |   35.03 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,179.42 μs** |   **852.58 μs** |  **46.733 μs** |  **1.00** |    **0.01** |  **62.5000** |  **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,306.03 μs |   396.16 μs |  21.715 μs |  0.32 |    0.00 |  15.6250 |        - |  339.92 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,630.89 μs** |   **474.42 μs** |  **26.005 μs** |  **1.00** |    **0.00** |   **7.8125** |        **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,171.73 μs | 1,199.90 μs |  65.771 μs |  0.18 |    0.01 |        - |        - |   37.24 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,700.36 μs** |   **607.70 μs** |  **33.310 μs** |  **1.00** |    **0.00** | **109.3750** |  **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,139.89 μs | 7,327.39 μs | 401.639 μs |  0.52 |    0.03 |  15.6250 |        - |  372.13 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.33 μs** |    **98.88 μs** |   **5.420 μs** |  **1.00** |    **0.05** |   **2.4414** |        **-** |   **41.61 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     70.46 μs |   107.76 μs |   5.907 μs |  0.57 |    0.05 |   0.9766 |   0.4883 |   20.87 KB |        0.50 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    709.42 μs | 1,246.91 μs |  68.347 μs |     ? |       ? |   3.9063 |        - |  143.29 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    376.18 μs |    23.83 μs |   1.306 μs |     ? |       ? |  11.7188 |  10.7422 |  244.51 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,563.45 μs | 3,229.04 μs | 176.995 μs |     ? |       ? | 117.1875 | 109.3750 | 2465.33 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,410.61 μs** |   **185.40 μs** |  **10.163 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,292.90 μs |    49.39 μs |   2.707 μs |  0.24 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,421.87 μs** |   **579.67 μs** |  **31.773 μs** |  **1.00** |    **0.01** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,104.65 μs |    10.52 μs |   0.577 μs |  0.20 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,404.65 μs** |    **83.34 μs** |   **4.568 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,368.91 μs |   124.18 μs |   6.807 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |             |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,412.70 μs** |   **135.62 μs** |   **7.434 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,369.46 μs |   157.66 μs |   8.642 μs |  0.25 |    0.00 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,154.462 ms** | **525.142 ms** | **28.7848 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.538 ms |  46.357 ms |  2.5410 ms | 0.005 |   407.2 KB |        5.46 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.316 ms** |  **21.528 ms** |  **1.1800 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.317 ms |  63.840 ms |  3.4993 ms | 0.005 |   604.6 KB |        2.41 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.646 ms** |  **12.766 ms** |  **0.6998 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.939 ms |  18.155 ms |  0.9952 ms | 0.005 |  818.12 KB |        1.36 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.098 ms** |  **31.301 ms** |  **1.7157 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.865 ms |  33.025 ms |  1.8102 ms | 0.005 | 2575.66 KB |        1.09 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.067 ms** |  **53.726 ms** |  **2.9449 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.340 ms |  17.540 ms |  0.9614 ms | 0.002 |  181.18 KB |       75.30 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.521 ms** |  **24.469 ms** |  **1.3412 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.770 ms |   5.621 ms |  0.3081 ms | 0.002 |  195.88 KB |       47.04 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.220 ms** |  **24.382 ms** |  **1.3365 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.109 ms |   7.504 ms |  0.4113 ms | 0.002 |  253.45 KB |      105.33 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.974 ms** |  **26.029 ms** |  **1.4267 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.122 ms |   3.434 ms |  0.1882 ms | 0.002 |  185.47 KB |       44.37 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.624 μs |  2.5766 μs | 0.1412 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.418 μs |  2.3920 μs | 0.1311 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.036 μs |  2.7439 μs | 0.1504 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.751 μs |  3.6350 μs | 0.1992 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.904 μs |  1.1586 μs | 0.0635 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.277 μs |  0.6578 μs | 0.0361 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.583 μs | 10.6136 μs | 0.5818 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.238 μs |  8.8257 μs | 0.4838 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.472 μs |  1.7999 μs | 0.0987 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.506 μs |  2.4295 μs | 0.1332 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,382.3 ns |     801.5 ns |     43.94 ns |  0.35 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,325.0 ns |   1,385.4 ns |     75.94 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,379.7 ns |     936.2 ns |     51.32 ns |  0.35 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,472.0 ns |   1,047.5 ns |     57.42 ns |  0.62 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    773.2 ns |     737.3 ns |     40.41 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 46,821.3 ns | 209,558.4 ns | 11,486.60 ns | 11.72 |    2.50 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,997.0 ns |   1,196.3 ns |     65.57 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,038.0 ns |   2,072.1 ns |    113.58 ns |  1.01 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.14 μs |   1.796 μs |  0.098 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   515.86 μs | 302.935 μs | 16.605 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.06 μs |  17.190 μs |  0.942 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,662.00 μs | 116.768 μs |  6.400 μs |    1280 B |


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