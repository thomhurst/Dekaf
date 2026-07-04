---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 00:30 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,168.93 μs** |   **755.196 μs** |  **41.395 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,356.75 μs | 2,582.012 μs | 141.529 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,460.54 μs** |   **848.589 μs** |  **46.514 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,334.61 μs | 1,018.921 μs |  55.850 μs |  0.31 |    0.01 |  15.6250 |       - |  339.34 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,127.36 μs** |   **446.153 μs** |  **24.455 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,360.88 μs | 1,790.259 μs |  98.130 μs |  0.22 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,081.04 μs** | **2,428.436 μs** | **133.111 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,704.78 μs | 4,098.047 μs | 224.628 μs |  0.51 |    0.02 |  15.6250 |       - |  361.79 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **145.48 μs** |    **81.800 μs** |   **4.484 μs** |  **1.00** |    **0.04** |   **2.1973** |       **-** |   **38.81 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     60.52 μs |   148.310 μs |   8.129 μs |  0.42 |    0.05 |        - |       - |    8.14 KB |        0.21 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,465.47 μs** |   **378.708 μs** |  **20.758 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **420.42 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    647.89 μs |   392.549 μs |  21.517 μs |  0.44 |    0.01 |        - |       - |  105.07 KB |        0.25 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    206.29 μs |   251.383 μs |  13.779 μs |     ? |       ? |   0.9766 |       - |  102.48 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **2,342.09 μs** |   **821.070 μs** |  **45.006 μs** |  **1.00** |    **0.02** |  **70.3125** |       **-** |  **1226.8 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,072.68 μs | 2,312.074 μs | 126.733 μs |  0.89 |    0.05 |   7.8125 |       - | 1025.91 KB |        0.84 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,439.58 μs** |    **99.311 μs** |   **5.444 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,120.08 μs |    76.795 μs |   4.209 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,435.24 μs** |    **30.901 μs** |   **1.694 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,116.45 μs |     4.960 μs |   0.272 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,438.86 μs** |    **31.571 μs** |   **1.730 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,117.71 μs |    65.217 μs |   3.575 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,440.69 μs** |    **68.161 μs** |   **3.736 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.22 μs |   126.052 μs |   6.909 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.378 ms** |  **18.623 ms** |  **1.0208 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.717 ms |  54.086 ms |  2.9646 ms | 0.005 |  605.55 KB |        8.12 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.529 ms** |  **10.367 ms** |  **0.5683 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.999 ms |  26.007 ms |  1.4255 ms | 0.005 |  777.11 KB |        3.10 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.763 ms** |  **15.528 ms** |  **0.8512 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    21.688 ms | 150.317 ms |  8.2394 ms | 0.007 |  995.23 KB |        1.65 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.420 ms** |   **8.619 ms** |  **0.4725 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.847 ms |  24.481 ms |  1.3419 ms | 0.005 | 2774.83 KB |        1.17 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,158.188 ms** |  **64.617 ms** |  **3.5419 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.182 ms |  13.600 ms |  0.7455 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,141.163 ms** | **523.801 ms** | **28.7113 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.449 ms |   7.034 ms |  0.3856 ms | 0.002 |  186.35 KB |       44.75 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.023 ms** |   **5.490 ms** |  **0.3009 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.452 ms |   8.219 ms |  0.4505 ms | 0.002 |  256.65 KB |      106.66 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,140.857 ms** | **545.600 ms** | **29.9062 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.988 ms |  22.469 ms |  1.2316 ms | 0.002 |  200.64 KB |       48.00 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.811 μs |  3.146 μs | 0.1724 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.467 μs | 26.349 μs | 1.4443 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.591 μs |  6.393 μs | 0.3504 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.061 μs | 11.294 μs | 0.6191 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.893 μs |  2.208 μs | 0.1210 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.159 μs |  2.608 μs | 0.1429 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.377 μs | 10.300 μs | 0.5646 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.111 μs |  8.020 μs | 0.4396 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.104 μs |  7.287 μs | 0.3995 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.469 μs |  1.580 μs | 0.0866 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,297.2 ns |     173.4 ns |      9.50 ns |  1,297.5 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,339.0 ns |   1,047.5 ns |     57.42 ns |  1,322.0 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,495.8 ns |   1,530.0 ns |     83.86 ns |  1,452.5 ns |  0.38 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,557.7 ns |   2,058.4 ns |    112.83 ns |  2,584.0 ns |  0.64 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    739.2 ns |     270.8 ns |     14.84 ns |    735.5 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 49,804.0 ns | 322,953.9 ns | 17,702.19 ns | 39,815.0 ns | 12.53 |    3.88 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,980.0 ns |   2,947.8 ns |    161.58 ns |  4,006.0 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,955.8 ns |   1,967.7 ns |    107.86 ns |  4,002.5 ns |  1.00 |    0.04 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.86 μs |   4.087 μs |  0.224 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   561.31 μs | 229.335 μs | 12.571 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.15 μs |   1.906 μs |  0.104 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,654.44 μs | 121.243 μs |  6.646 μs |    1280 B |


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