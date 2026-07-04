---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 13:28 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,133.10 μs** |   **517.469 μs** |  **28.364 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,335.56 μs | 1,676.154 μs |  91.876 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,358.75 μs** |   **756.498 μs** |  **41.466 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,354.30 μs |   721.147 μs |  39.529 μs |  0.32 |    0.00 |  15.6250 |       - |  339.61 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,270.89 μs** |   **924.063 μs** |  **50.651 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,335.33 μs | 1,682.779 μs |  92.239 μs |  0.21 |    0.01 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,913.47 μs** | **1,021.098 μs** |  **55.970 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,288.06 μs |   509.120 μs |  27.907 μs |  0.49 |    0.00 |  15.6250 |       - |  361.73 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **139.27 μs** |     **3.297 μs** |   **0.181 μs** |  **1.00** |    **0.00** |   **2.4414** |       **-** |    **41.9 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     56.25 μs |    60.116 μs |   3.295 μs |  0.40 |    0.02 |   0.2441 |       - |     9.4 KB |        0.22 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,374.53 μs** | **1,369.106 μs** |  **75.045 μs** |  **1.00** |    **0.07** |  **23.4375** |       **-** |  **424.72 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    687.01 μs |   733.842 μs |  40.224 μs |  0.50 |    0.03 |        - |       - |   67.45 KB |        0.16 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    215.64 μs |    44.402 μs |   2.434 μs |     ? |       ? |   0.9766 |       - |  103.76 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,128.12 μs | 2,891.911 μs | 158.515 μs |     ? |       ? |   7.8125 |       - | 1004.22 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,418.90 μs** |    **24.370 μs** |   **1.336 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,150.30 μs |   126.612 μs |   6.940 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,422.50 μs** |   **137.726 μs** |   **7.549 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,151.15 μs |   282.854 μs |  15.504 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,661.78 μs** | **7,569.732 μs** | **414.923 μs** |  **1.00** |    **0.09** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,138.91 μs |   144.600 μs |   7.926 μs |  0.20 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,423.17 μs** |    **84.725 μs** |   **4.644 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,117.27 μs |    40.354 μs |   2.212 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.730 ms** | **14.789 ms** | **0.8106 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.533 ms | 31.285 ms | 1.7148 ms | 0.005 |   593.1 KB |        7.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.235 ms** | **23.920 ms** | **1.3111 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.653 ms | 19.086 ms | 1.0462 ms | 0.004 |  776.27 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.546 ms** | **36.894 ms** | **2.0223 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.242 ms | 31.609 ms | 1.7326 ms | 0.005 |  994.88 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.528 ms** |  **8.612 ms** | **0.4721 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.281 ms | 25.169 ms | 1.3796 ms | 0.005 | 2762.44 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.092 ms** | **14.032 ms** | **0.7691 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.764 ms |  9.145 ms | 0.5013 ms | 0.002 |  184.47 KB |       76.66 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.833 ms** |  **7.956 ms** | **0.4361 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.789 ms | 20.163 ms | 1.1052 ms | 0.002 |  195.41 KB |       46.93 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.342 ms** |  **8.033 ms** | **0.4403 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.942 ms | 15.788 ms | 0.8654 ms | 0.002 |  184.56 KB |       76.70 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.871 ms** | **34.230 ms** | **1.8763 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.976 ms | 26.083 ms | 1.4297 ms | 0.002 |  187.04 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.848 μs |  3.1914 μs | 0.1749 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.161 μs |  2.9742 μs | 0.1630 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.270 μs |  3.0402 μs | 0.1666 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.778 μs | 59.0540 μs | 3.2370 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.958 μs |  0.5574 μs | 0.0306 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.266 μs |  2.2172 μs | 0.1215 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.835 μs |  6.5380 μs | 0.3584 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.394 μs |  4.0789 μs | 0.2236 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.559 μs |  1.5800 μs | 0.0866 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.691 μs |  1.6658 μs | 0.0913 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,393.0 ns |   2,573.6 ns |    141.07 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,470.0 ns |   1,851.3 ns |    101.47 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,518.7 ns |     690.7 ns |     37.86 ns |  0.35 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,619.8 ns |     547.4 ns |     30.01 ns |  0.61 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    925.2 ns |   2,587.2 ns |    141.81 ns |  0.22 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 48,334.0 ns | 239,639.4 ns | 13,135.44 ns | 11.28 |    2.66 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,284.3 ns |   1,401.4 ns |     76.81 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,008.0 ns |   2,211.9 ns |    121.24 ns |  0.94 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.46 μs |   2.114 μs |  0.116 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   530.25 μs | 233.144 μs | 12.779 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.29 μs |   8.521 μs |  0.467 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,664.62 μs | 306.009 μs | 16.773 μs |    1280 B |


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