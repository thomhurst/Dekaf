---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 14:58 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,101.28 μs** |    **690.84 μs** |  **37.867 μs** |  **1.00** |    **0.01** |        **-** |        **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,385.74 μs |  1,798.59 μs |  98.587 μs |  0.23 |    0.01 |        - |        - |   35.03 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,343.04 μs** |    **523.32 μs** |  **28.685 μs** |  **1.00** |    **0.00** |  **62.5000** |  **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,338.63 μs |    879.81 μs |  48.225 μs |  0.32 |    0.01 |  15.6250 |        - |  339.92 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,918.14 μs** | **11,386.80 μs** | **624.149 μs** |  **1.01** |    **0.11** |   **7.8125** |        **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,263.68 μs |  1,832.40 μs | 100.440 μs |  0.18 |    0.02 |        - |        - |   37.24 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,063.87 μs** |  **2,059.70 μs** | **112.899 μs** |  **1.00** |    **0.01** | **109.3750** |  **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,364.02 μs |  5,607.90 μs | 307.388 μs |  0.53 |    0.02 |  15.6250 |        - |  372.22 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.34 μs** |     **63.98 μs** |   **3.507 μs** |  **1.00** |    **0.03** |   **2.4414** |        **-** |   **42.13 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     69.46 μs |     56.12 μs |   3.076 μs |  0.52 |    0.02 |   0.9766 |   0.7324 |   22.86 KB |        0.54 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,521.16 μs** |  **1,101.96 μs** |  **60.402 μs** |  **1.00** |    **0.05** |  **23.4375** |        **-** |  **419.09 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    800.98 μs |    798.40 μs |  43.763 μs |  0.53 |    0.03 |   3.9063 |        - |  110.16 KB |        0.26 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    357.03 μs |    255.61 μs |  14.011 μs |     ? |       ? |  13.6719 |  12.6953 |  306.69 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |       **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,674.72 μs |  1,685.95 μs |  92.412 μs |     ? |       ? | 117.1875 | 109.3750 | 2475.54 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,437.61 μs** |    **333.55 μs** |  **18.283 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,220.55 μs |    298.83 μs |  16.380 μs |  0.22 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,400.84 μs** |    **160.17 μs** |   **8.779 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,116.66 μs |     90.95 μs |   4.985 μs |  0.21 |    0.00 |        - |        - |    1.26 KB |        1.07 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,405.04 μs** |     **49.20 μs** |   **2.697 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,110.61 μs |     32.78 μs |   1.797 μs |  0.21 |    0.00 |        - |        - |    1.26 KB |        0.61 |
|                         |               |             |           |              |              |            |       |         |          |          |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,406.02 μs** |    **112.66 μs** |   **6.175 μs** |  **1.00** |    **0.00** |        **-** |        **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,112.44 μs |     49.96 μs |   2.739 μs |  0.21 |    0.00 |        - |        - |    1.26 KB |        0.61 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.796 ms** |  **17.526 ms** | **0.9607 ms** | **3,168.236 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.273 ms |  37.836 ms | 2.0739 ms |    13.635 ms | 0.005 |  406.93 KB |        5.45 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.752 ms** |  **22.064 ms** | **1.2094 ms** | **3,164.278 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.886 ms |  53.923 ms | 2.9557 ms |    11.701 ms | 0.004 |  590.93 KB |        2.36 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.610 ms** |  **24.494 ms** | **1.3426 ms** | **3,165.095 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.821 ms | 171.830 ms | 9.4186 ms |    11.957 ms | 0.005 |  892.29 KB |        1.48 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.839 ms** |  **30.037 ms** | **1.6464 ms** | **3,165.614 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.459 ms |  40.178 ms | 2.2023 ms |    14.263 ms | 0.004 | 2653.92 KB |        1.12 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.024 ms** |   **9.148 ms** | **0.5015 ms** | **3,154.818 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.360 ms |  11.229 ms | 0.6155 ms |     6.560 ms | 0.002 |  182.66 KB |       75.91 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.600 ms** |   **8.824 ms** | **0.4837 ms** | **3,156.332 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.195 ms |   2.401 ms | 0.1316 ms |     5.240 ms | 0.002 |   192.3 KB |       46.18 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.932 ms** |  **24.450 ms** | **1.3402 ms** | **3,156.560 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.440 ms |   8.783 ms | 0.4814 ms |     6.658 ms | 0.002 |  181.49 KB |       75.43 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.810 ms** |  **14.024 ms** | **0.7687 ms** | **3,156.200 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.990 ms |   8.935 ms | 0.4898 ms |     6.060 ms | 0.002 |  260.47 KB |       62.32 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.766 μs |   3.097 μs |  0.1698 μs | 14.723 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 22.275 μs | 378.154 μs | 20.7279 μs | 11.011 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.303 μs |   2.125 μs |  0.1165 μs | 10.360 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.801 μs |  12.170 μs |  0.6671 μs | 34.845 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.372 μs |  10.763 μs |  0.5900 μs |  9.087 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.551 μs |   7.273 μs |  0.3987 μs | 19.322 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.516 μs |  11.086 μs |  0.6077 μs | 18.700 μs |    2400 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.267 μs |   7.780 μs |  0.4265 μs | 20.418 μs |    2440 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.656 μs |   2.014 μs |  0.1104 μs |  4.710 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.300 μs |  22.417 μs |  1.2287 μs | 12.533 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,476.3 ns | 6,915.4 ns | 379.06 ns |  0.37 |    0.08 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,235.2 ns | 1,378.6 ns |  75.57 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,679.0 ns | 5,067.9 ns | 277.79 ns |  0.42 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,432.0 ns | 1,187.5 ns |  65.09 ns |  0.62 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    812.0 ns | 1,015.8 ns |  55.68 ns |  0.21 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,464.3 ns | 4,211.5 ns | 230.85 ns | 10.49 |    0.07 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,951.7 ns |   355.8 ns |  19.50 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,904.3 ns |   832.1 ns |  45.61 ns |  0.99 |    0.01 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.794 μs |  33.398 μs |  1.8307 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   919.197 μs | 143.552 μs |  7.8686 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.257 μs |  13.643 μs |  0.7478 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,680.315 μs | 557.625 μs | 30.5653 μs |    1280 B |


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