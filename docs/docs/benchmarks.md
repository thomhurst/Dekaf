---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 03:40 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,133.76 μs** |   **424.36 μs** |  **23.260 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,384.24 μs | 2,144.78 μs | 117.562 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,344.94 μs** | **1,345.24 μs** |  **73.737 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1063.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,349.72 μs |   490.16 μs |  26.867 μs |  0.32 |    0.00 |  15.6250 |       - |  339.31 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,503.25 μs** |   **448.74 μs** |  **24.597 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,225.59 μs | 1,147.18 μs |  62.881 μs |  0.19 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,380.83 μs** | **5,963.44 μs** | **326.876 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,090.49 μs | 2,538.67 μs | 139.153 μs |  0.49 |    0.01 |  15.6250 |       - |  361.62 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.78 μs** |    **43.39 μs** |   **2.379 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **41.13 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.51 μs |    78.74 μs |   4.316 μs |  0.49 |    0.03 |        - |       - |    9.44 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,430.16 μs** | **1,265.48 μs** |  **69.365 μs** |  **1.00** |    **0.06** |  **25.3906** |       **-** |  **421.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    678.56 μs |   233.05 μs |  12.774 μs |  0.48 |    0.02 |        - |       - |   70.74 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    199.01 μs |   187.70 μs |  10.289 μs |     ? |       ? |   0.9766 |       - |   100.7 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,936.20 μs | 3,535.92 μs | 193.816 μs |     ? |       ? |   7.8125 |       - |  999.45 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,395.98 μs** |    **35.77 μs** |   **1.961 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,329.71 μs |   488.48 μs |  26.775 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,401.24 μs** |    **71.39 μs** |   **3.913 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,340.55 μs |    69.51 μs |   3.810 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,399.64 μs** |    **41.66 μs** |   **2.284 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,358.66 μs |   224.14 μs |  12.286 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,402.34 μs** |    **97.68 μs** |   **5.354 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,379.14 μs |   309.81 μs |  16.982 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.522 ms** | **40.155 ms** | **2.2010 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.714 ms | 67.972 ms | 3.7258 ms | 0.005 |   595.8 KB |        7.98 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.928 ms** | **38.203 ms** | **2.0940 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.794 ms | 37.039 ms | 2.0302 ms | 0.004 |  776.03 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.424 ms** | **37.744 ms** | **2.0689 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.564 ms | 21.389 ms | 1.1724 ms | 0.006 |  994.75 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.396 ms** | **20.914 ms** | **1.1464 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.377 ms | 10.243 ms | 0.5614 ms | 0.005 | 2761.19 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.601 ms** | **29.959 ms** | **1.6422 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.320 ms | 11.330 ms | 0.6210 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.046 ms** | **36.526 ms** | **2.0021 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.375 ms | 17.734 ms | 0.9721 ms | 0.002 |  187.73 KB |       45.08 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.856 ms** | **36.638 ms** | **2.0082 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.584 ms | 18.230 ms | 0.9992 ms | 0.002 |  185.16 KB |       76.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.188 ms** | **25.454 ms** | **1.3952 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.290 ms | 19.808 ms | 1.0857 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.494 μs |   1.772 μs |  0.0971 μs | 15.518 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.011 μs |   9.733 μs |  0.5335 μs |  9.814 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.117 μs |   4.026 μs |  0.2207 μs | 11.097 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.491 μs |   2.281 μs |  0.1250 μs | 32.487 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.033 μs |   4.834 μs |  0.2650 μs |  8.934 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.703 μs |  16.698 μs |  0.9153 μs | 22.008 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.850 μs |  31.716 μs |  1.7385 μs | 17.086 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.884 μs |  23.054 μs |  1.2637 μs | 19.284 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 | 12.128 μs | 253.011 μs | 13.8684 μs |  4.186 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.454 μs |   5.978 μs |  0.3277 μs | 11.498 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,076.5 ns |  4,207.9 ns | 230.65 ns |  0.26 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,536.0 ns |  1,391.4 ns |  76.27 ns |  0.37 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,294.7 ns |  1,526.4 ns |  83.67 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,253.7 ns |  6,211.5 ns | 340.47 ns |  0.55 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    697.2 ns |  1,069.0 ns |  58.59 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,696.3 ns | 12,794.0 ns | 701.28 ns |  8.65 |    0.71 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,149.5 ns |  7,231.2 ns | 396.37 ns |  1.01 |    0.12 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,967.5 ns |  6,073.2 ns | 332.89 ns |  0.96 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Median       | Allocated |
|------------------------ |------------:|----------:|----------:|-------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.84 μs |  10.51 μs |  0.576 μs |    10.751 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   524.35 μs | 243.28 μs | 13.335 μs |   528.995 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    17.15 μs | 242.48 μs | 13.291 μs |     9.785 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,600.31 μs | 292.53 μs | 16.035 μs | 1,607.616 μs |    1280 B |


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