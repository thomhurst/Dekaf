---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 13:29 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,113.23 μs** |   **897.597 μs** |  **49.200 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,293.63 μs | 1,469.871 μs |  80.569 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,273.80 μs** | **1,599.436 μs** |  **87.671 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,288.20 μs |   579.327 μs |  31.755 μs |  0.31 |    0.01 |  15.6250 |       - |   339.4 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,668.79 μs** |   **491.609 μs** |  **26.947 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,259.81 μs | 3,743.518 μs | 205.195 μs |  0.19 |    0.03 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,782.56 μs** | **8,734.373 μs** | **478.760 μs** |  **1.00** |    **0.05** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,631.53 μs | 1,512.505 μs |  82.906 μs |  0.48 |    0.02 |  15.6250 |       - |  361.84 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **139.91 μs** |    **40.178 μs** |   **2.202 μs** |  **1.00** |    **0.02** |   **2.5635** |       **-** |   **42.77 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.29 μs |     4.780 μs |   0.262 μs |  0.42 |    0.01 |        - |       - |    9.24 KB |        0.22 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,157.01 μs** |   **621.862 μs** |  **34.086 μs** |  **1.00** |    **0.04** |  **23.4375** |       **-** |  **407.29 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    649.72 μs | 1,402.438 μs |  76.872 μs |  0.56 |    0.06 |        - |       - |   72.58 KB |        0.18 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    196.18 μs |   149.036 μs |   8.169 μs |     ? |       ? |   0.4883 |       - |   15.25 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,906.94 μs | 1,032.682 μs |  56.605 μs |     ? |       ? |   7.8125 |       - |   952.9 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,398.55 μs** |    **16.794 μs** |   **0.921 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,339.51 μs |   222.176 μs |  12.178 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,406.67 μs** |   **346.170 μs** |  **18.975 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,112.43 μs |    98.382 μs |   5.393 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,409.06 μs** |   **129.643 μs** |   **7.106 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,104.45 μs |    33.521 μs |   1.837 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,404.62 μs** |    **41.948 μs** |   **2.299 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,352.21 μs |   343.603 μs |  18.834 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,171.192 ms** | **42.305 ms** | **2.3189 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.652 ms | 36.139 ms | 1.9809 ms | 0.005 |  593.52 KB |        7.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.617 ms** |  **5.445 ms** | **0.2984 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    17.357 ms | 53.226 ms | 2.9175 ms | 0.005 |  782.26 KB |        3.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.931 ms** | **14.212 ms** | **0.7790 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.894 ms |  9.625 ms | 0.5276 ms | 0.006 | 1040.99 KB |        1.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.317 ms** | **12.529 ms** | **0.6867 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.787 ms | 36.458 ms | 1.9984 ms | 0.005 | 2763.09 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.038 ms** | **28.402 ms** | **1.5568 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.644 ms | 11.182 ms | 0.6129 ms | 0.002 |  188.96 KB |       78.53 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.419 ms** | **40.001 ms** | **2.1926 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.263 ms |  5.619 ms | 0.3080 ms | 0.002 |  188.62 KB |       45.30 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.941 ms** | **58.098 ms** | **3.1846 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.891 ms |  6.375 ms | 0.3494 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.661 ms** |  **7.940 ms** | **0.4352 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.826 ms | 20.497 ms | 1.1235 ms | 0.002 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.884 μs |  3.156 μs | 0.1730 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.468 μs |  4.135 μs | 0.2266 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.218 μs |  4.549 μs | 0.2493 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.743 μs |  2.119 μs | 0.1161 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.870 μs |  1.782 μs | 0.0977 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.413 μs |  4.286 μs | 0.2349 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.698 μs |  1.750 μs | 0.0959 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.657 μs | 54.790 μs | 3.0032 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.536 μs |  4.170 μs | 0.2285 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.236 μs |  3.075 μs | 0.1686 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,302.0 ns |   2,849.8 ns |   156.20 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,794.0 ns |   1,277.1 ns |    70.00 ns |  0.44 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,343.0 ns |     482.7 ns |    26.46 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,569.7 ns |     738.9 ns |    40.50 ns |  0.63 |    0.03 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    705.0 ns |     537.2 ns |    29.44 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 45,561.0 ns | 167,004.4 ns | 9,154.08 ns | 11.15 |    2.00 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,092.5 ns |   3,680.6 ns |   201.74 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,058.0 ns |  14,031.6 ns |   769.12 ns |  0.99 |    0.17 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.983 μs |   5.028 μs |  0.2756 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   513.331 μs | 376.928 μs | 20.6607 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.788 μs |  10.722 μs |  0.5877 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,677.695 μs | 362.101 μs | 19.8480 μs |    1280 B |


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