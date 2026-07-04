---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 18:27 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,183.62 μs** |   **618.677 μs** |  **33.912 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,244.87 μs |   563.774 μs |  30.902 μs |  0.20 |    0.00 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,377.19 μs** |   **725.892 μs** |  **39.789 μs** |  **1.00** |    **0.01** | **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,273.20 μs |    87.364 μs |   4.789 μs |  0.31 |    0.00 | 19.5313 |  3.9063 |  339.39 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,095.93 μs** |   **109.105 μs** |   **5.980 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,354.63 μs | 2,497.965 μs | 136.922 μs |  0.22 |    0.02 |       - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,934.21 μs** | **4,391.269 μs** | **240.700 μs** |  **1.00** |    **0.02** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,459.84 μs | 1,494.068 μs |  81.895 μs |  0.50 |    0.01 | 15.6250 |       - |  361.73 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.92 μs** |    **11.846 μs** |   **0.649 μs** |  **1.00** |    **0.01** |  **2.4414** |       **-** |   **42.44 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.42 μs |    82.507 μs |   4.522 μs |  0.45 |    0.03 |       - |       - |    9.24 KB |        0.22 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,464.07 μs** |   **524.318 μs** |  **28.740 μs** |  **1.00** |    **0.02** | **23.4375** |       **-** |  **424.09 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    641.91 μs |   978.260 μs |  53.622 μs |  0.44 |    0.03 |       - |       - |   72.01 KB |        0.17 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    229.14 μs |   308.027 μs |  16.884 μs |     ? |       ? |  0.9766 |       - |  103.29 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,293.96 μs | 9,995.014 μs | 547.860 μs |     ? |       ? |  7.8125 |       - |  995.45 KB |           ? |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,466.51 μs** |   **775.479 μs** |  **42.507 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,381.28 μs |   374.482 μs |  20.527 μs |  0.25 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,426.09 μs** |     **8.899 μs** |   **0.488 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,338.99 μs |   554.397 μs |  30.388 μs |  0.25 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,451.93 μs** |   **189.008 μs** |  **10.360 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,168.79 μs |   129.662 μs |   7.107 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,440.73 μs** |    **47.699 μs** |   **2.615 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,117.58 μs |    28.015 μs |   1.536 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.431 ms** | **27.227 ms** | **1.4924 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.690 ms | 13.821 ms | 0.7576 ms | 0.005 |  591.67 KB |        7.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.229 ms** | **22.582 ms** | **1.2378 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.898 ms |  6.220 ms | 0.3409 ms | 0.005 |  778.18 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.393 ms** | **37.427 ms** | **2.0515 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.391 ms | 50.755 ms | 2.7820 ms | 0.005 |  993.91 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.449 ms** | **13.365 ms** | **0.7326 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.774 ms | 47.213 ms | 2.5879 ms | 0.005 | 2850.17 KB |        1.20 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.845 ms** | **53.593 ms** | **2.9376 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.720 ms | 32.178 ms | 1.7638 ms | 0.002 |  184.48 KB |       76.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.359 ms** | **51.895 ms** | **2.8446 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.023 ms | 10.688 ms | 0.5859 ms | 0.002 |  188.63 KB |       45.30 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.802 ms** | **18.794 ms** | **1.0302 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.856 ms | 21.241 ms | 1.1643 ms | 0.002 |  186.01 KB |       77.30 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.554 ms** | **29.208 ms** | **1.6010 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.491 ms | 31.949 ms | 1.7512 ms | 0.002 |  205.17 KB |       49.09 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 36.445 μs | 258.834 μs | 14.1876 μs | 30.837 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.700 μs |  23.781 μs |  1.3035 μs | 11.066 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.368 μs |   1.187 μs |  0.0650 μs | 10.366 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 36.462 μs | 307.828 μs | 16.8731 μs | 26.750 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.035 μs |   1.475 μs |  0.0808 μs |  8.988 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.138 μs |  79.878 μs |  4.3784 μs | 26.871 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.907 μs |   2.610 μs |  0.1431 μs | 17.975 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.339 μs |  10.638 μs |  0.5831 μs | 20.048 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.656 μs |   1.393 μs |  0.0764 μs |  4.639 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 17.206 μs | 203.830 μs | 11.1726 μs | 10.760 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,200.5 ns |  1,355.1 ns |  74.28 ns |  0.24 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,336.3 ns |    210.7 ns |  11.55 ns |  0.27 |    0.00 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,436.7 ns |  1,377.4 ns |  75.50 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,585.7 ns |  1,017.5 ns |  55.77 ns |  0.52 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    764.3 ns |  1,214.7 ns |  66.58 ns |  0.15 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,925.0 ns | 10,426.5 ns | 571.51 ns |  8.65 |    0.10 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,962.3 ns |    379.8 ns |  20.82 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,910.3 ns |  1,417.1 ns |  77.67 ns |  0.79 |    0.01 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.38 μs |   3.346 μs |  0.183 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   531.41 μs | 329.038 μs | 18.036 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.09 μs |   4.304 μs |  0.236 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,640.76 μs | 118.205 μs |  6.479 μs |    1280 B |


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