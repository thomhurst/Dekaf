---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 01:33 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,221.43 μs** |   **891.21 μs** |  **48.850 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,375.29 μs | 2,742.21 μs | 150.310 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,514.48 μs** |   **400.59 μs** |  **21.958 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,268.86 μs |   321.78 μs |  17.638 μs |  0.30 |    0.00 |  19.5313 |  3.9063 |  339.49 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,131.06 μs** |   **420.88 μs** |  **23.070 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.06 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,411.54 μs | 1,505.82 μs |  82.539 μs |  0.23 |    0.01 |        - |       - |   36.28 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,749.94 μs** | **1,810.32 μs** |  **99.230 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,097.48 μs |   327.11 μs |  17.930 μs |  0.48 |    0.00 |  15.6250 |       - |  361.74 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.23 μs** |   **172.05 μs** |   **9.431 μs** |  **1.00** |    **0.08** |   **2.4414** |       **-** |   **41.79 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     88.69 μs |   352.91 μs |  19.344 μs |  0.62 |    0.12 |        - |       - |    7.55 KB |        0.18 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,414.79 μs** |   **280.53 μs** |  **15.377 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **436.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    634.72 μs |   266.49 μs |  14.607 μs |  0.45 |    0.01 |        - |       - |   70.95 KB |        0.16 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    224.34 μs |   358.49 μs |  19.650 μs |     ? |       ? |   0.9766 |       - |   102.9 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,101.91 μs | 2,936.21 μs | 160.943 μs |     ? |       ? |   7.8125 |       - |  969.54 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,447.24 μs** |   **212.98 μs** |  **11.674 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,389.67 μs |   208.03 μs |  11.403 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,451.23 μs** |    **77.90 μs** |   **4.270 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,393.99 μs |   429.52 μs |  23.544 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,456.89 μs** |    **70.32 μs** |   **3.854 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,387.96 μs |   466.55 μs |  25.573 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,426.51 μs** |    **32.18 μs** |   **1.764 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,393.67 μs |   125.07 μs |   6.855 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.220 ms** |  **21.118 ms** | **1.1575 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.909 ms |   8.781 ms | 0.4813 ms | 0.005 |  592.16 KB |        7.94 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.485 ms** |   **4.579 ms** | **0.2510 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.721 ms |  13.722 ms | 0.7521 ms | 0.004 |  777.59 KB |        3.11 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.546 ms** |  **15.169 ms** | **0.8315 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.464 ms | 111.467 ms | 6.1099 ms | 0.006 | 1067.23 KB |        1.77 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.804 ms** |  **16.552 ms** | **0.9073 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.880 ms |  18.686 ms | 1.0242 ms | 0.005 | 2763.12 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.377 ms** |  **13.868 ms** | **0.7601 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.134 ms |  21.067 ms | 1.1547 ms | 0.002 |  185.78 KB |       77.21 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.770 ms** |  **18.585 ms** | **1.0187 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.240 ms |  14.248 ms | 0.7810 ms | 0.002 |   185.2 KB |       44.47 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.275 ms** |  **29.992 ms** | **1.6439 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.773 ms |  14.742 ms | 0.8081 ms | 0.002 |  256.65 KB |      106.66 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.761 ms** |  **36.453 ms** | **1.9981 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.256 ms |   9.948 ms | 0.5453 ms | 0.002 |  189.43 KB |       45.32 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.594 μs | 25.356 μs | 1.3898 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.748 μs |  5.813 μs | 0.3186 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.280 μs |  4.324 μs | 0.2370 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.103 μs | 89.779 μs | 4.9211 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.017 μs |  3.237 μs | 0.1775 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.374 μs | 61.459 μs | 3.3688 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.547 μs | 24.031 μs | 1.3172 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.159 μs |  7.812 μs | 0.4282 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.461 μs |  3.033 μs | 0.1662 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.437 μs |  2.793 μs | 0.1531 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,442.0 ns |   1,493.3 ns |     81.85 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,365.3 ns |   2,429.5 ns |    133.17 ns |  0.28 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,305.5 ns |   1,355.1 ns |     74.28 ns |  0.27 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,818.0 ns |   4,385.6 ns |    240.39 ns |  0.58 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    780.7 ns |     665.4 ns |     36.47 ns |  0.16 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 47,063.7 ns | 208,692.7 ns | 11,439.15 ns |  9.72 |    2.06 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,845.8 ns |   2,903.6 ns |    159.16 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,640.2 ns |   8,880.5 ns |    486.77 ns |  0.96 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.854 μs |   8.615 μs | 0.4722 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   540.202 μs | 149.707 μs | 8.2060 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.869 μs |  21.624 μs | 1.1853 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,684.980 μs | 144.338 μs | 7.9116 μs |    1280 B |


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