---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 02:25 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,210.73 μs** |    **449.40 μs** |    **24.633 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,294.33 μs |  2,034.96 μs |   111.543 μs |  0.21 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,381.35 μs** |    **508.24 μs** |    **27.858 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,348.07 μs |    762.63 μs |    41.802 μs |  0.32 |    0.01 |  15.6250 |       - |  339.46 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,308.12 μs** |    **361.95 μs** |    **19.840 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,318.25 μs |  1,706.04 μs |    93.514 μs |  0.21 |    0.01 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,583.54 μs** |  **4,672.94 μs** |   **256.140 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,457.36 μs |  8,292.39 μs |   454.534 μs |  0.51 |    0.03 |  15.6250 |       - |  361.58 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.62 μs** |     **50.39 μs** |     **2.762 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.67 μs |    162.37 μs |     8.900 μs |  0.51 |    0.06 |        - |       - |    8.45 KB |        0.25 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,340.33 μs** |    **303.02 μs** |    **16.609 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    662.91 μs |  1,812.66 μs |    99.358 μs |  0.49 |    0.06 |        - |       - |  116.81 KB |        0.35 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,048.50 μs** |    **190.88 μs** |    **10.463 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **122.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    203.05 μs |    255.69 μs |    14.015 μs |  0.19 |    0.01 |   0.9766 |       - |  104.17 KB |        0.85 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,711.41 μs** | **26,033.39 μs** | **1,426.978 μs** |  **1.02** |    **0.19** |  **74.2188** |       **-** | **1225.54 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,834.10 μs |  1,258.78 μs |    68.998 μs |  0.19 |    0.03 |   7.8125 |       - | 1009.01 KB |        0.82 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,454.61 μs** |    **273.67 μs** |    **15.001 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,308.73 μs |    291.83 μs |    15.996 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,471.64 μs** |    **139.81 μs** |     **7.664 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,141.68 μs |    343.89 μs |    18.850 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,470.47 μs** |    **291.21 μs** |    **15.962 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,111.86 μs |     23.27 μs |     1.275 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,469.70 μs** |    **158.03 μs** |     **8.662 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,114.11 μs |     59.30 μs |     3.250 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.346 ms** | **32.704 ms** | **1.7926 ms** | **3,168.446 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    19.293 ms | 57.569 ms | 3.1555 ms |    18.614 ms | 0.006 |  610.63 KB |        8.18 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.564 ms** |  **3.994 ms** | **0.2189 ms** | **3,165.670 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.893 ms | 32.880 ms | 1.8023 ms |    15.258 ms | 0.005 |  784.15 KB |        3.13 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.217 ms** | **17.420 ms** | **0.9548 ms** | **3,166.707 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.509 ms | 79.346 ms | 4.3492 ms |    16.490 ms | 0.006 | 1005.23 KB |        1.67 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.538 ms** | **12.082 ms** | **0.6622 ms** | **3,166.258 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.041 ms | 21.410 ms | 1.1736 ms |    16.826 ms | 0.005 | 2773.57 KB |        1.17 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,158.939 ms** | **45.856 ms** | **2.5135 ms** | **3,157.537 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.744 ms |  5.957 ms | 0.3265 ms |     5.611 ms | 0.002 |  186.77 KB |       77.62 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.076 ms** | **27.375 ms** | **1.5005 ms** | **3,157.024 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.629 ms | 61.313 ms | 3.3608 ms |     5.716 ms | 0.002 |  186.45 KB |       44.77 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.427 ms** | **48.772 ms** | **2.6734 ms** | **3,157.631 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.706 ms | 41.738 ms | 2.2878 ms |     6.534 ms | 0.002 |  184.79 KB |       76.80 |
|                      |            |              |             |              |           |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.414 ms** | **50.240 ms** | **2.7538 ms** | **3,157.895 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.122 ms |  2.711 ms | 0.1486 ms |     6.205 ms | 0.002 |  187.13 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.219 μs |  1.664 μs | 0.0912 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.577 μs | 15.427 μs | 0.8456 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.289 μs |  3.205 μs | 0.1757 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.114 μs |  1.120 μs | 0.0614 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.877 μs |  3.656 μs | 0.2004 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.826 μs | 71.708 μs | 3.9306 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.544 μs |  9.396 μs | 0.5150 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.455 μs | 14.330 μs | 0.7855 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.628 μs |  2.339 μs | 0.1282 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.742 μs |  4.479 μs | 0.2455 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,555.7 ns |  6,438.3 ns |   352.90 ns |  0.37 |    0.07 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,301.7 ns |  1,146.6 ns |    62.85 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,346.3 ns |  1,069.0 ns |    58.59 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,481.7 ns |  1,369.3 ns |    75.06 ns |  0.59 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    805.3 ns |  2,009.6 ns |   110.15 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,128.3 ns | 32,661.1 ns | 1,790.27 ns | 10.06 |    0.38 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,187.0 ns |    729.7 ns |    40.00 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,137.3 ns |    173.4 ns |     9.50 ns |  0.99 |    0.01 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev   | Allocated |
|------------------------ |------------:|-----------:|---------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.09 μs |   4.323 μs | 0.237 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   540.03 μs | 181.650 μs | 9.957 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.17 μs |   2.650 μs | 0.145 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,650.67 μs | 102.719 μs | 5.630 μs |    1280 B |


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