---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 11:02 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,027.03 μs** |   **554.18 μs** |  **30.377 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,550.92 μs | 2,101.71 μs | 115.202 μs |  0.26 |    0.02 |       - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,184.40 μs** |   **118.53 μs** |   **6.497 μs** |  **1.00** |    **0.00** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,217.69 μs |   104.24 μs |   5.714 μs |  0.31 |    0.00 | 11.7188 |       - |  339.89 KB |        0.32 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,575.95 μs** |   **451.53 μs** |  **24.750 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,154.49 μs | 1,856.34 μs | 101.752 μs |  0.18 |    0.01 |       - |       - |   36.95 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,956.64 μs** | **1,444.32 μs** |  **79.168 μs** |  **1.00** |    **0.01** | **78.1250** | **31.2500** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,674.83 μs |   291.94 μs |  16.002 μs |  0.52 |    0.00 |  7.8125 |       - |  369.44 KB |        0.19 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **127.58 μs** |    **46.26 μs** |   **2.536 μs** |  **1.00** |    **0.02** |  **1.7090** |       **-** |   **42.08 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    65.39 μs |    36.52 μs |   2.002 μs |  0.51 |    0.02 |  0.4883 |  0.2441 |   23.61 KB |        0.56 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,267.51 μs** |   **680.28 μs** |  **37.289 μs** |  **1.00** |    **0.04** | **15.6250** |       **-** |   **421.6 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   633.51 μs |   413.00 μs |  22.638 μs |  0.50 |    0.02 |       - |       - |  128.13 KB |        0.30 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   279.40 μs |   220.12 μs |  12.066 μs |     ? |       ? |  3.9063 |  2.9297 |  188.29 KB |           ? |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |          **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,927.75 μs | 7,753.45 μs | 424.992 μs |     ? |       ? | 39.0625 | 31.2500 | 1937.77 KB |           ? |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,404.60 μs** |   **144.69 μs** |   **7.931 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,161.25 μs | 1,491.11 μs |  81.733 μs |  0.21 |    0.01 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,462.69 μs** |   **261.89 μs** |  **14.355 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,336.79 μs |   239.89 μs |  13.149 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,404.48 μs** |   **309.77 μs** |  **16.980 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,346.44 μs |   309.32 μs |  16.955 μs |  0.25 |    0.00 |       - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |             |             |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,462.31 μs** |   **135.43 μs** |   **7.423 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,292.07 μs |   406.16 μs |  22.263 μs |  0.24 |    0.00 |       - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.022 ms** |   **8.502 ms** | **0.4660 ms** | **3,169.132 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.943 ms |  56.243 ms | 3.0829 ms |    14.586 ms | 0.005 |  410.95 KB |        5.51 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.480 ms** |  **10.658 ms** | **0.5842 ms** | **3,166.565 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.106 ms |  33.304 ms | 1.8255 ms |    12.687 ms | 0.004 |  590.86 KB |        2.36 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.607 ms** |   **9.922 ms** | **0.5439 ms** | **3,166.730 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.995 ms |  59.701 ms | 3.2724 ms |    17.029 ms | 0.005 |  828.15 KB |        1.38 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.707 ms** |  **14.775 ms** | **0.8099 ms** | **3,166.320 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.812 ms |   9.805 ms | 0.5375 ms |    16.041 ms | 0.005 | 2575.89 KB |        1.09 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.470 ms** |  **16.771 ms** | **0.9193 ms** | **3,157.803 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.755 ms |  21.193 ms | 1.1617 ms |     7.188 ms | 0.002 |  183.52 KB |       76.27 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.847 ms** |   **4.623 ms** | **0.2534 ms** | **3,155.988 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     9.710 ms | 120.727 ms | 6.6174 ms |     6.269 ms | 0.003 |  187.77 KB |       45.09 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.405 ms** |   **4.354 ms** | **0.2386 ms** | **3,156.407 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.480 ms |   1.274 ms | 0.0698 ms |     6.513 ms | 0.002 |  310.09 KB |      128.87 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.959 ms** |   **8.366 ms** | **0.4586 ms** | **3,157.214 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.070 ms |  26.880 ms | 1.4734 ms |     5.222 ms | 0.002 |  187.45 KB |       44.85 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 15.092 μs |  2.382 μs | 0.1306 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.095 μs |  6.113 μs | 0.3351 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 11.146 μs |  3.196 μs | 0.1752 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.941 μs |  4.160 μs | 0.2280 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.752 μs |  4.862 μs | 0.2665 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 22.340 μs |  8.437 μs | 0.4625 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.338 μs | 21.055 μs | 1.1541 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.654 μs | 11.841 μs | 0.6490 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.589 μs |  7.192 μs | 0.3942 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,359.0 ns |  5,005.5 ns |   274.4 ns |  0.31 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,534.3 ns |  5,271.8 ns |   289.0 ns |  0.35 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,292.0 ns |  8,324.4 ns |   456.3 ns |  0.30 |    0.09 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,943.7 ns |  6,852.5 ns |   375.6 ns |  0.68 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    755.0 ns |  2,968.0 ns |   162.7 ns |  0.17 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 44,570.0 ns | 96,487.5 ns | 5,288.8 ns | 10.26 |    1.13 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,348.3 ns |  3,576.0 ns |   196.0 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,951.3 ns |  6,333.0 ns |   347.1 ns |  0.91 |    0.08 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.184 μs |   6.081 μs | 0.3333 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   516.860 μs | 118.858 μs | 6.5150 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.010 μs |  14.106 μs | 0.7732 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,570.899 μs |  89.458 μs | 4.9035 μs |    1280 B |


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