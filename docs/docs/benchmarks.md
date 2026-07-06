---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 00:27 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,090.28 μs** |    **356.76 μs** |    **19.555 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,417.84 μs |  2,849.66 μs |   156.199 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,309.47 μs** |  **1,477.84 μs** |    **81.005 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,350.67 μs |  1,032.61 μs |    56.601 μs |  0.32 |    0.01 |  15.6250 |       - |  339.35 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,304.24 μs** |    **576.38 μs** |    **31.593 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,430.30 μs |  8,940.50 μs |   490.059 μs |  0.23 |    0.07 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,105.41 μs** |  **3,439.74 μs** |   **188.544 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,558.99 μs |  7,483.27 μs |   410.183 μs |  0.54 |    0.03 |  15.6250 |       - |  361.68 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.58 μs** |     **25.70 μs** |     **1.409 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     64.74 μs |     50.23 μs |     2.754 μs |  0.49 |    0.02 |        - |       - |     9.5 KB |        0.28 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,327.10 μs** |    **277.66 μs** |    **15.220 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    639.42 μs |  1,182.05 μs |    64.792 μs |  0.48 |    0.04 |        - |       - |  116.31 KB |        0.35 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **659.38 μs** |  **7,470.75 μs** |   **409.497 μs** |  **1.50** |    **1.48** |   **7.3242** |       **-** |  **122.48 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    220.03 μs |    460.02 μs |    25.215 μs |  0.50 |    0.37 |   0.4883 |       - |   12.25 KB |        0.10 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,347.28 μs** | **32,650.40 μs** | **1,789.679 μs** |  **1.03** |    **0.26** |  **74.2188** |       **-** | **1225.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,032.63 μs |  3,334.82 μs |   182.793 μs |  0.22 |    0.05 |   7.8125 |       - | 1025.77 KB |        0.84 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,438.92 μs** |    **292.43 μs** |    **16.029 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,289.48 μs |    378.87 μs |    20.767 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,404.19 μs** |    **278.45 μs** |    **15.263 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,109.13 μs |     10.43 μs |     0.572 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,422.97 μs** |    **273.02 μs** |    **14.965 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,114.27 μs |     73.40 μs |     4.023 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.60 μs** |    **238.79 μs** |    **13.089 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,366.53 μs |    560.66 μs |    30.732 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.444 ms** | **22.972 ms** | **1.2592 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.027 ms | 29.957 ms | 1.6421 ms | 0.005 |  602.03 KB |        8.07 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.002 ms** |  **4.192 ms** | **0.2298 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.139 ms | 11.711 ms | 0.6419 ms | 0.004 |  787.67 KB |        3.15 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.723 ms** | **58.535 ms** | **3.2085 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.951 ms | 76.085 ms | 4.1705 ms | 0.006 | 1003.63 KB |        1.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.130 ms** |  **9.738 ms** | **0.5338 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.731 ms | 39.596 ms | 2.1704 ms | 0.005 | 2773.55 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.219 ms** | **24.642 ms** | **1.3507 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.904 ms | 15.643 ms | 0.8574 ms | 0.002 |  184.56 KB |       76.70 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.176 ms** | **36.097 ms** | **1.9786 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.811 ms | 31.042 ms | 1.7015 ms | 0.002 |  186.45 KB |       44.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.087 ms** | **12.883 ms** | **0.7062 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.447 ms |  8.622 ms | 0.4726 ms | 0.002 |  184.78 KB |       76.79 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.349 ms** |  **9.926 ms** | **0.5441 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.553 ms |  5.458 ms | 0.2992 ms | 0.002 |  187.13 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.062 μs |   1.8274 μs | 0.1002 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.445 μs |   3.5894 μs | 0.1967 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.341 μs |   4.2759 μs | 0.2344 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.705 μs | 124.4266 μs | 6.8202 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.110 μs |   0.4495 μs | 0.0246 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 24.863 μs |  74.1849 μs | 4.0663 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.827 μs |   8.4127 μs | 0.4611 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.115 μs |   9.8704 μs | 0.5410 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.539 μs |   2.3371 μs | 0.1281 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.698 μs |   5.0072 μs | 0.2745 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,299.0 ns |   1,047.5 ns |     57.42 ns |  1,282.0 ns |  0.31 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,406.7 ns |   1,427.3 ns |     78.23 ns |  1,383.0 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,349.7 ns |   3,375.5 ns |    185.02 ns |  1,253.0 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,579.5 ns |   1,015.8 ns |     55.68 ns |  2,569.5 ns |  0.61 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    771.7 ns |   1,680.0 ns |     92.09 ns |    752.0 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,006.7 ns |  14,383.9 ns |    788.43 ns | 38,943.0 ns |  9.22 |    0.29 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,231.0 ns |   2,277.0 ns |    124.81 ns |  4,298.0 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | 12,586.0 ns | 195,214.8 ns | 10,700.38 ns |  8,064.0 ns |  2.98 |    2.19 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.235 μs |   3.445 μs |  0.1888 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   541.609 μs | 211.789 μs | 11.6089 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.970 μs |   1.845 μs |  0.1011 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,633.505 μs |  56.604 μs |  3.1026 μs |    1280 B |


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