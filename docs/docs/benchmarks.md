---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 01:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,207.13 μs** |   **623.85 μs** |  **34.195 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,296.04 μs | 1,255.95 μs |  68.843 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,395.26 μs** | **1,383.67 μs** |  **75.844 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,372.19 μs |   438.56 μs |  24.039 μs |  0.32 |    0.00 |  15.6250 |       - |  339.52 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,203.62 μs** |   **264.50 μs** |  **14.498 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,331.69 μs | 2,102.64 μs | 115.253 μs |  0.21 |    0.02 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,741.99 μs** | **4,634.28 μs** | **254.021 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,151.74 μs | 2,580.02 μs | 141.420 μs |  0.48 |    0.01 |  15.6250 |       - |  361.54 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.91 μs** |    **95.53 μs** |   **5.236 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.32 μs |    19.89 μs |   1.090 μs |  0.47 |    0.02 |        - |       - |    7.92 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,364.29 μs** |   **195.06 μs** |  **10.692 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    580.89 μs |   267.67 μs |  14.672 μs |  0.43 |    0.01 |        - |       - |   67.83 KB |        0.20 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,092.95 μs** |    **59.93 μs** |   **3.285 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.57 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    207.23 μs |   158.22 μs |   8.673 μs |  0.19 |    0.01 |   0.9766 |       - |  103.08 KB |        0.84 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,384.73 μs** |   **785.41 μs** |  **43.051 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** | **1210.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,117.45 μs | 1,161.68 μs |  63.675 μs |  0.89 |    0.03 |   7.8125 |       - | 1002.14 KB |        0.83 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,451.49 μs** |   **197.80 μs** |  **10.842 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,219.26 μs |   131.84 μs |   7.227 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,450.77 μs** |   **113.31 μs** |   **6.211 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,126.11 μs |    48.78 μs |   2.674 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,460.00 μs** |   **141.90 μs** |   **7.778 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,119.68 μs |    39.88 μs |   2.186 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,454.07 μs** |   **293.81 μs** |  **16.105 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.87 μs |    42.81 μs |   2.346 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.797 ms** |  **13.523 ms** | **0.7413 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.965 ms |  17.159 ms | 0.9406 ms | 0.006 |  601.01 KB |        8.05 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,169.470 ms** | **103.485 ms** | **5.6724 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.114 ms |  54.263 ms | 2.9744 ms | 0.005 |     786 KB |        3.14 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.948 ms** |   **8.766 ms** | **0.4805 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.724 ms |  73.331 ms | 4.0195 ms | 0.006 | 1002.23 KB |        1.66 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.425 ms** |  **15.594 ms** | **0.8548 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.564 ms |  13.215 ms | 0.7244 ms | 0.005 | 2769.48 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.164 ms** |   **8.258 ms** | **0.4526 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.523 ms |  13.928 ms | 0.7634 ms | 0.002 |  184.47 KB |       76.66 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.292 ms** |  **23.048 ms** | **1.2633 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.549 ms |   3.444 ms | 0.1888 ms | 0.002 |  187.66 KB |       45.07 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,158.006 ms** |  **22.071 ms** | **1.2098 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.904 ms |  23.607 ms | 1.2940 ms | 0.003 |  184.84 KB |       76.82 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.664 ms** |  **24.039 ms** | **1.3176 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     7.495 ms |  36.385 ms | 1.9944 ms | 0.002 |  261.49 KB |       62.56 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.166 μs |   2.3883 μs |  0.1309 μs | 26.209 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.855 μs |   0.6578 μs |  0.0361 μs | 10.845 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.502 μs |   1.1692 μs |  0.0641 μs | 10.476 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.811 μs |   1.7968 μs |  0.0985 μs | 26.781 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.940 μs |   1.3693 μs |  0.0751 μs |  8.937 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.133 μs | 185.7881 μs | 10.1837 μs | 20.369 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.760 μs |   5.6902 μs |  0.3119 μs | 17.733 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.581 μs |   7.8624 μs |  0.4310 μs | 24.451 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.571 μs |   1.2943 μs |  0.0709 μs |  4.558 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.442 μs |  10.5675 μs |  0.5792 μs | 11.371 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,356.3 ns |  4,484.9 ns | 245.83 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,352.2 ns |  2,082.5 ns | 114.15 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,754.8 ns |  9,881.3 ns | 541.63 ns |  0.43 |    0.12 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,773.7 ns |  6,017.9 ns | 329.86 ns |  0.67 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    757.7 ns |  1,753.0 ns |  96.09 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,764.5 ns | 12,699.9 ns | 696.12 ns |  9.92 |    0.38 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,115.8 ns |  3,050.8 ns | 167.23 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,617.2 ns | 17,561.8 ns | 962.62 ns |  1.12 |    0.21 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.117 μs |   2.140 μs |  0.1173 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   563.130 μs | 448.690 μs | 24.5942 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.325 μs |  12.535 μs |  0.6871 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,687.144 μs | 255.225 μs | 13.9897 μs |    1280 B |


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