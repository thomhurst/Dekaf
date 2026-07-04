---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 22:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,200.85 μs** |    **345.60 μs** |    **18.943 μs** |  **1.00** |    **0.00** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,292.27 μs |  1,880.43 μs |   103.073 μs |  0.21 |    0.01 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,377.14 μs** |    **556.39 μs** |    **30.498 μs** |  **1.00** |    **0.01** | **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,348.80 μs |    965.84 μs |    52.941 μs |  0.32 |    0.01 | 15.6250 |       - |  339.34 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,193.61 μs** |    **484.37 μs** |    **26.550 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,368.67 μs |  1,845.76 μs |   101.172 μs |  0.22 |    0.01 |       - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **15,047.25 μs** | **25,518.77 μs** | **1,398.770 μs** |  **1.01** |    **0.11** | **93.7500** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,589.74 μs |  4,935.82 μs |   270.549 μs |  0.44 |    0.04 | 15.6250 |       - |  361.67 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.56 μs** |     **49.53 μs** |     **2.715 μs** |  **1.00** |    **0.02** |  **2.4414** |       **-** |   **42.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.13 μs |     85.60 μs |     4.692 μs |  0.45 |    0.03 |       - |       - |    7.47 KB |        0.18 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,361.78 μs** |    **889.80 μs** |    **48.773 μs** |  **1.00** |    **0.04** | **23.4375** |       **-** |   **417.4 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    606.59 μs |    324.85 μs |    17.806 μs |  0.45 |    0.02 |       - |       - |   70.45 KB |        0.17 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    218.49 μs |    228.07 μs |    12.501 μs |     ? |       ? |  0.9766 |       - |   103.6 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,990.42 μs |  2,754.01 μs |   150.957 μs |     ? |       ? |  7.8125 |       - | 1000.51 KB |           ? |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,447.25 μs** |    **372.18 μs** |    **20.400 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,386.43 μs |    407.48 μs |    22.336 μs |  0.25 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,443.45 μs** |     **99.92 μs** |     **5.477 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,120.92 μs |     57.88 μs |     3.172 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,447.78 μs** |    **123.66 μs** |     **6.778 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,117.28 μs |     20.98 μs |     1.150 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,452.68 μs** |    **419.51 μs** |    **22.995 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,118.96 μs |     77.04 μs |     4.223 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.591 ms** | **41.476 ms** | **2.2735 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.520 ms | 22.341 ms | 1.2246 ms | 0.005 |  591.16 KB |        7.92 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.193 ms** | **13.588 ms** | **0.7448 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.579 ms | 34.387 ms | 1.8849 ms | 0.005 |  774.58 KB |        3.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.657 ms** | **18.126 ms** | **0.9935 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.426 ms | 14.962 ms | 0.8201 ms | 0.005 | 1066.39 KB |        1.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.434 ms** |  **9.284 ms** | **0.5089 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18.588 ms | 23.623 ms | 1.2949 ms | 0.006 | 2760.98 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.425 ms** |  **3.521 ms** | **0.1930 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.378 ms |  6.072 ms | 0.3328 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.260 ms** | **36.277 ms** | **1.9885 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.349 ms | 27.648 ms | 1.5155 ms | 0.002 |  186.29 KB |       44.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.249 ms** |  **2.530 ms** | **0.1387 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.747 ms |  8.926 ms | 0.4892 ms | 0.002 |  184.66 KB |       76.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.186 ms** | **32.644 ms** | **1.7893 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.198 ms | 32.782 ms | 1.7969 ms | 0.002 |  261.33 KB |       62.52 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 24.520 μs |  11.006 μs | 0.6033 μs | 24.274 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.738 μs |  20.595 μs | 1.1289 μs |  9.226 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 17.834 μs | 169.974 μs | 9.3168 μs | 12.544 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 30.699 μs | 152.056 μs | 8.3347 μs | 26.014 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.453 μs |  28.734 μs | 1.5750 μs | 12.913 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 14.310 μs |   8.739 μs | 0.4790 μs | 14.310 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.775 μs |  79.199 μs | 4.3412 μs | 17.479 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.357 μs |  87.588 μs | 4.8010 μs | 18.642 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.829 μs |  33.109 μs | 1.8148 μs |  4.266 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.096 μs |  46.928 μs | 2.5723 μs | 11.790 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,509.7 ns | 15,210.2 ns |   833.7 ns |  1,149.0 ns |  0.29 |    0.16 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,102.8 ns | 11,958.3 ns |   655.5 ns |    908.5 ns |  0.21 |    0.12 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  3,451.8 ns | 19,855.8 ns | 1,088.4 ns |  3,079.5 ns |  0.67 |    0.24 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,360.7 ns | 17,938.1 ns |   983.2 ns |  1,948.0 ns |  0.46 |    0.20 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    745.7 ns |  7,026.6 ns |   385.1 ns |    602.0 ns |  0.14 |    0.07 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 31,036.2 ns | 51,707.9 ns | 2,834.3 ns | 29,820.5 ns |  6.00 |    1.42 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,443.5 ns | 28,999.9 ns | 1,589.6 ns |  4,802.5 ns |  1.05 |    0.36 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,105.5 ns | 27,372.7 ns | 1,500.4 ns |  3,473.5 ns |  0.79 |    0.31 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Allocated |
|------------------------ |------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.99 μs |  35.55 μs |  1.949 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   457.63 μs | 563.94 μs | 30.911 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.50 μs |  48.66 μs |  2.667 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,227.52 μs | 105.49 μs |  5.782 μs |    1280 B |


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