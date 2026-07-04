---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 05:48 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,156.52 μs** |   **512.98 μs** |  **28.118 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,292.06 μs |   848.31 μs |  46.499 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,388.73 μs** |   **859.53 μs** |  **47.114 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,269.41 μs |   121.95 μs |   6.684 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.33 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,499.45 μs** | **8,882.03 μs** | **486.854 μs** |  **1.00** |    **0.09** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,262.43 μs |   932.62 μs |  51.120 μs |  0.19 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,759.19 μs** | **2,273.29 μs** | **124.607 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,431.75 μs | 4,254.84 μs | 233.222 μs |  0.50 |    0.02 |  15.6250 |       - |  361.79 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.29 μs** |   **101.49 μs** |   **5.563 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **41.13 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     72.34 μs |   276.25 μs |  15.142 μs |  0.51 |    0.09 |        - |       - |    9.33 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,418.74 μs** |   **104.08 μs** |   **5.705 μs** |  **1.00** |    **0.00** |  **25.3906** |       **-** |  **421.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    674.73 μs | 1,255.62 μs |  68.825 μs |  0.48 |    0.04 |        - |       - |  180.21 KB |        0.43 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    222.51 μs |    56.91 μs |   3.119 μs |     ? |       ? |   0.9766 |       - |  102.74 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,093.45 μs | 5,637.46 μs | 309.008 μs |     ? |       ? |   7.8125 |       - | 1034.57 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,422.14 μs** |    **33.22 μs** |   **1.821 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,114.83 μs |    27.07 μs |   1.484 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,440.93 μs** |   **116.74 μs** |   **6.399 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,393.56 μs |    85.95 μs |   4.711 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,436.01 μs** |   **103.56 μs** |   **5.676 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,395.10 μs |   186.44 μs |  10.219 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,436.15 μs** |   **312.73 μs** |  **17.142 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,354.14 μs |   573.97 μs |  31.461 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.634 ms** | **40.169 ms** | **2.2018 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.076 ms | 22.990 ms | 1.2602 ms | 0.005 |  594.73 KB |        7.97 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.528 ms** | **24.762 ms** | **1.3573 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.694 ms | 28.879 ms | 1.5829 ms | 0.005 |  786.75 KB |        3.14 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.465 ms** | **16.097 ms** | **0.8823 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.662 ms | 78.434 ms | 4.2992 ms | 0.006 | 1114.56 KB |        1.85 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.116 ms** | **24.212 ms** | **1.3271 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.120 ms | 56.287 ms | 3.0853 ms | 0.005 | 2760.81 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.548 ms** | **18.603 ms** | **1.0197 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.029 ms | 33.215 ms | 1.8206 ms | 0.002 |  186.72 KB |       77.60 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.921 ms** |  **5.816 ms** | **0.3188 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.855 ms | 17.567 ms | 0.9629 ms | 0.002 |  196.74 KB |       47.25 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.305 ms** | **28.619 ms** | **1.5687 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.674 ms |  8.574 ms | 0.4700 ms | 0.002 |  238.67 KB |       99.19 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.495 ms** | **55.943 ms** | **3.0664 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.841 ms | 20.725 ms | 1.1360 ms | 0.002 |   188.7 KB |       45.15 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.402 μs |  3.475 μs | 0.1905 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.076 μs |  5.465 μs | 0.2996 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.019 μs |  5.510 μs | 0.3020 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.806 μs |  2.450 μs | 0.1343 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.104 μs |  3.725 μs | 0.2042 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.946 μs |  5.332 μs | 0.2923 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.936 μs | 21.995 μs | 1.2056 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.835 μs | 11.005 μs | 0.6032 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.573 μs |  4.638 μs | 0.2542 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.301 μs | 12.672 μs | 0.6946 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,208.7 ns |  1,393.4 ns |    76.38 ns |  0.29 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,312.3 ns |    374.0 ns |    20.50 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,348.3 ns |  1,351.8 ns |    74.10 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,584.0 ns |  4,494.8 ns |   246.37 ns |  0.62 |    0.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    737.0 ns |  1,961.3 ns |   107.50 ns |  0.18 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,435.0 ns | 45,422.4 ns | 2,489.75 ns |  9.47 |    1.10 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,206.7 ns |  9,693.6 ns |   531.34 ns |  1.01 |    0.15 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,083.3 ns |  7,195.8 ns |   394.42 ns |  0.98 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.120 μs |   8.848 μs |  0.4850 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   520.869 μs | 288.174 μs | 15.7958 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.384 μs |   3.780 μs |  0.2072 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,572.531 μs | 373.592 μs | 20.4778 μs |    1280 B |


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