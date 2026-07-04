---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 05:05 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,209.31 μs** |   **894.95 μs** |  **49.055 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,467.38 μs | 1,666.89 μs |  91.368 μs |  0.24 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,403.55 μs** |   **531.26 μs** |  **29.120 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,345.83 μs |   524.13 μs |  28.729 μs |  0.32 |    0.00 |  15.6250 |       - |  339.47 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,129.83 μs** |   **773.21 μs** |  **42.382 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,449.10 μs | 1,803.14 μs |  98.836 μs |  0.24 |    0.01 |        - |       - |   36.34 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,931.76 μs** |   **848.38 μs** |  **46.502 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.94 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,739.27 μs | 4,309.72 μs | 236.230 μs |  0.52 |    0.02 |  15.6250 |       - |  362.19 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **147.31 μs** |    **24.41 μs** |   **1.338 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **41.92 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     53.14 μs |    81.88 μs |   4.488 μs |  0.36 |    0.03 |   0.2441 |  0.1221 |    9.94 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,579.25 μs** |   **995.45 μs** |  **54.564 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |  **419.38 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    625.11 μs |   829.36 μs |  45.460 μs |  0.40 |    0.03 |        - |       - |   72.56 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    211.76 μs |   244.66 μs |  13.411 μs |     ? |       ? |   0.9766 |       - |  101.92 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,215.64 μs** |   **364.91 μs** |  **20.002 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1225.88 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,122.99 μs | 7,206.87 μs | 395.033 μs |  0.96 |    0.15 |   7.8125 |       - |  969.53 KB |        0.79 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,448.54 μs** |   **127.22 μs** |   **6.973 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,317.05 μs |   510.07 μs |  27.959 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,446.10 μs** |   **144.74 μs** |   **7.934 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,230.44 μs |   208.02 μs |  11.402 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,464.49 μs** |   **181.23 μs** |   **9.934 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,138.11 μs |   238.39 μs |  13.067 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,454.38 μs** |    **80.77 μs** |   **4.427 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,119.64 μs |    48.05 μs |   2.634 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169.333 ms** |   **8.694 ms** | **0.4765 ms** | **3,169.313 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.613 ms |  71.825 ms | 3.9369 ms |    15.235 ms | 0.005 |  595.01 KB |        7.97 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.355 ms** |  **28.283 ms** | **1.5503 ms** | **3,166.613 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.698 ms |  13.991 ms | 0.7669 ms |    13.396 ms | 0.004 |   775.6 KB |        3.10 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.906 ms** |  **26.931 ms** | **1.4762 ms** | **3,166.442 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.250 ms | 142.063 ms | 7.7870 ms |    16.044 ms | 0.006 | 1066.54 KB |        1.77 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.893 ms** |  **20.849 ms** | **1.1428 ms** | **3,166.635 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.418 ms |  13.380 ms | 0.7334 ms |    16.344 ms | 0.005 | 2764.59 KB |        1.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.510 ms** |  **53.602 ms** | **2.9381 ms** | **3,158.426 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.707 ms |  30.619 ms | 1.6783 ms |     7.283 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.057 ms** |  **19.270 ms** | **1.0562 ms** | **3,155.678 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.742 ms |   7.740 ms | 0.4243 ms |     6.851 ms | 0.002 |  192.34 KB |       46.19 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.074 ms** |  **31.662 ms** | **1.7355 ms** | **3,156.859 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.577 ms |  20.984 ms | 1.1502 ms |     6.925 ms | 0.002 |   184.7 KB |       76.76 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.448 ms** |  **16.161 ms** | **0.8859 ms** | **3,158.328 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.764 ms |  13.812 ms | 0.7571 ms |     7.025 ms | 0.002 |     187 KB |       44.74 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 24.804 μs | 135.584 μs |  7.4318 μs | 25.398 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.174 μs |  29.796 μs |  1.6332 μs |  9.422 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.259 μs |   3.987 μs |  0.2185 μs | 10.159 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.858 μs |   2.187 μs |  0.1199 μs | 26.805 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.035 μs |   4.081 μs |  0.2237 μs |  8.931 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.830 μs |  64.496 μs |  3.5353 μs | 25.562 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.630 μs |   8.884 μs |  0.4870 μs | 17.433 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.975 μs |  23.323 μs |  1.2784 μs | 20.919 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 | 12.668 μs | 257.023 μs | 14.0883 μs |  4.639 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.626 μs |   6.909 μs |  0.3787 μs | 10.569 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,259.3 ns |   2,590.4 ns |    141.99 ns |  0.30 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,325.3 ns |   4,002.5 ns |    219.39 ns |  0.31 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,365.3 ns |   1,898.9 ns |    104.08 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,440.7 ns |   1,695.1 ns |     92.92 ns |  0.57 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    747.7 ns |   1,393.4 ns |     76.38 ns |  0.18 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 48,475.8 ns | 297,579.3 ns | 16,311.33 ns | 11.37 |    3.60 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,320.2 ns |  11,354.2 ns |    622.36 ns |  1.01 |    0.17 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,876.7 ns |   3,837.7 ns |    210.36 ns |  0.91 |    0.12 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.287 μs |   2.861 μs |  0.1568 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   537.980 μs |  88.352 μs |  4.8429 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.154 μs |  15.105 μs |  0.8280 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 2,050.906 μs | 205.189 μs | 11.2471 μs |    1280 B |


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