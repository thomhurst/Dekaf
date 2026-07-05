---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 23:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,979.12 μs** |   **508.769 μs** |  **27.887 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,307.20 μs | 2,456.675 μs | 134.659 μs |  0.22 |    0.02 |       - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,182.41 μs** |   **974.359 μs** |  **53.408 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,301.39 μs |   986.417 μs |  54.069 μs |  0.32 |    0.01 |  7.8125 |       - |  339.62 KB |        0.32 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,435.42 μs** |   **538.269 μs** |  **29.504 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,115.57 μs |   301.296 μs |  16.515 μs |  0.17 |    0.00 |       - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,806.44 μs** |   **898.339 μs** |  **49.241 μs** |  **1.00** |    **0.01** | **78.1250** | **31.2500** |  **1937.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,590.31 μs |   900.790 μs |  49.375 μs |  0.52 |    0.01 |  7.8125 |       - |  361.69 KB |        0.19 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **113.17 μs** |    **31.921 μs** |   **1.750 μs** |  **1.00** |    **0.02** |  **1.3428** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    56.05 μs |    72.216 μs |   3.958 μs |  0.50 |    0.03 |       - |       - |    4.33 KB |        0.13 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,162.80 μs** |   **262.216 μs** |  **14.373 μs** |  **1.00** |    **0.02** | **13.6719** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   693.48 μs | 1,097.813 μs |  60.175 μs |  0.60 |    0.05 |       - |       - |   41.68 KB |        0.12 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **802.95 μs** | **1,917.568 μs** | **105.108 μs** |  **1.01** |    **0.16** |  **4.8828** |       **-** |  **122.01 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   180.54 μs |   108.453 μs |   5.945 μs |  0.23 |    0.02 |  0.4883 |       - |   99.08 KB |        0.81 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,476.92 μs** | **1,301.275 μs** |  **71.327 μs** |  **1.00** |    **0.01** | **48.8281** |       **-** | **1220.66 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 1,594.87 μs | 1,230.376 μs |  67.441 μs |  0.21 |    0.01 |       - |       - |  994.17 KB |        0.81 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,360.85 μs** |    **67.995 μs** |   **3.727 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,099.83 μs |    46.423 μs |   2.545 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,355.43 μs** |    **83.997 μs** |   **4.604 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,096.08 μs |     4.556 μs |   0.250 μs |  0.20 |    0.00 |       - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,362.71 μs** |    **83.127 μs** |   **4.556 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,098.36 μs |    63.514 μs |   3.481 μs |  0.20 |    0.00 |       - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,353.01 μs** |    **95.701 μs** |   **5.246 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,103.40 μs |   102.646 μs |   5.626 μs |  0.21 |    0.00 |       - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.023 ms** | **27.057 ms** | **1.4831 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.821 ms | 33.616 ms | 1.8426 ms | 0.006 |   603.5 KB |        8.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.610 ms** | **37.586 ms** | **2.0602 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.941 ms | 40.816 ms | 2.2373 ms | 0.004 |   794.3 KB |        3.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.495 ms** | **16.808 ms** | **0.9213 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.248 ms | 21.806 ms | 1.1952 ms | 0.005 |  1112.8 KB |        1.85 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.497 ms** | **25.569 ms** | **1.4015 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.753 ms | 19.644 ms | 1.0768 ms | 0.005 | 2768.73 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.867 ms** | **24.691 ms** | **1.3534 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.384 ms | 11.778 ms | 0.6456 ms | 0.002 |  184.73 KB |       76.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.931 ms** | **36.047 ms** | **1.9758 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.376 ms |  8.463 ms | 0.4639 ms | 0.002 |  196.85 KB |       47.27 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.821 ms** | **32.779 ms** | **1.7967 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.736 ms | 13.397 ms | 0.7343 ms | 0.002 |  295.33 KB |      122.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.995 ms** | **24.551 ms** | **1.3457 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.830 ms |  3.690 ms | 0.2022 ms | 0.002 |  189.57 KB |       45.36 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.722 μs |  8.1597 μs | 0.4473 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.123 μs |  0.6417 μs | 0.0352 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.822 μs | 21.8269 μs | 1.1964 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.758 μs |  2.0096 μs | 0.1102 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.993 μs |  1.0690 μs | 0.0586 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.485 μs |  1.2171 μs | 0.0667 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.074 μs | 11.1077 μs | 0.6089 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.298 μs |  4.4617 μs | 0.2446 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.565 μs |  2.9607 μs | 0.1623 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.493 μs |  2.5301 μs | 0.1387 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,227.2 ns |   637.3 ns |  34.93 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,413.0 ns | 2,241.8 ns | 122.88 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,526.7 ns | 5,979.1 ns | 327.74 ns |  0.36 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,948.7 ns | 8,178.2 ns | 448.27 ns |  0.70 |    0.09 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    801.3 ns | 1,749.0 ns |  95.87 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,161.7 ns | 2,828.3 ns | 155.03 ns |  9.55 |    0.15 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,207.7 ns | 1,313.1 ns |  71.97 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,017.7 ns |   846.0 ns |  46.37 ns |  0.96 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.441 μs |   2.700 μs |  0.1480 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   516.546 μs | 359.096 μs | 19.6833 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.857 μs |  10.072 μs |  0.5521 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,671.501 μs | 408.083 μs | 22.3684 μs |    1280 B |


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