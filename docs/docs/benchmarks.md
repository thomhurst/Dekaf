---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 14:52 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,149.92 μs** |   **779.179 μs** |  **42.709 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,362.74 μs | 1,338.165 μs |  73.349 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,399.81 μs** | **2,417.778 μs** | **132.527 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,366.83 μs |   569.618 μs |  31.223 μs |  0.32 |    0.01 |  15.6250 |       - |  339.43 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,431.24 μs** | **7,679.409 μs** | **420.934 μs** |  **1.00** |    **0.08** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,272.78 μs | 1,470.766 μs |  80.618 μs |  0.20 |    0.02 |        - |       - |   36.33 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,803.35 μs** | **1,929.679 μs** | **105.772 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,595.69 μs | 4,832.980 μs | 264.912 μs |  0.52 |    0.02 |  15.6250 |       - |  361.97 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.52 μs** |   **259.947 μs** |  **14.249 μs** |  **1.01** |    **0.13** |   **2.4414** |       **-** |    **41.5 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     65.78 μs |     6.396 μs |   0.351 μs |  0.49 |    0.05 |        - |       - |    7.79 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,470.10 μs** |   **244.978 μs** |  **13.428 μs** |  **1.00** |    **0.01** |  **23.4375** |       **-** |   **423.9 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    605.23 μs |   510.777 μs |  27.997 μs |  0.41 |    0.02 |        - |       - |   68.54 KB |        0.16 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    203.67 μs |   258.951 μs |  14.194 μs |     ? |       ? |   0.9766 |       - |  102.21 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,097.32 μs | 2,602.757 μs | 142.666 μs |     ? |       ? |   7.8125 |       - |  969.72 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,423.35 μs** |    **39.578 μs** |   **2.169 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,353.43 μs |   578.003 μs |  31.682 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,423.36 μs** |    **67.277 μs** |   **3.688 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,272.79 μs |   197.342 μs |  10.817 μs |  0.23 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,423.11 μs** |    **28.231 μs** |   **1.547 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,120.30 μs |    75.154 μs |   4.119 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,431.15 μs** |    **69.482 μs** |   **3.809 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,119.43 μs |    67.803 μs |   3.716 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.121 ms** |  **41.357 ms** | **2.2669 ms** | **3,167.940 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.846 ms |  14.775 ms | 0.8099 ms |    15.024 ms | 0.005 |  600.16 KB |        8.04 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.355 ms** |  **25.345 ms** | **1.3893 ms** | **3,166.080 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.024 ms |  79.253 ms | 4.3441 ms |    14.569 ms | 0.005 |  792.04 KB |        3.16 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.000 ms** |  **10.605 ms** | **0.5813 ms** | **3,165.335 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.517 ms | 159.692 ms | 8.7533 ms |    13.713 ms | 0.006 |  997.26 KB |        1.66 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.440 ms** |  **28.285 ms** | **1.5504 ms** | **3,164.350 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.293 ms |  26.341 ms | 1.4439 ms |    16.947 ms | 0.005 | 2764.88 KB |        1.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.548 ms** |  **16.760 ms** | **0.9187 ms** | **3,155.739 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.349 ms |   9.309 ms | 0.5103 ms |     6.416 ms | 0.002 |  198.13 KB |       82.34 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.408 ms** |  **26.107 ms** | **1.4310 ms** | **3,157.250 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.733 ms |   7.185 ms | 0.3939 ms |     5.887 ms | 0.002 |  199.92 KB |       48.01 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.050 ms** |  **66.635 ms** | **3.6525 ms** | **3,156.166 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.507 ms |   9.125 ms | 0.5002 ms |     6.229 ms | 0.002 |     186 KB |       77.30 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.979 ms** |  **36.468 ms** | **1.9989 ms** | **3,157.237 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.234 ms |  13.988 ms | 0.7667 ms |     5.849 ms | 0.002 |  187.03 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.071 μs |   0.8426 μs |  0.0462 μs | 26.098 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.066 μs |   3.2513 μs |  0.1782 μs |  9.990 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.750 μs |   3.6542 μs |  0.2003 μs | 10.700 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 38.750 μs | 226.6005 μs | 12.4207 μs | 37.500 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.867 μs |   0.7952 μs |  0.0436 μs |  8.847 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.388 μs |   3.0402 μs |  0.1666 μs | 20.317 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.104 μs |   0.8341 μs |  0.0457 μs | 18.095 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 26.776 μs | 195.7190 μs | 10.7280 μs | 20.718 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.537 μs |   1.3198 μs |  0.0723 μs |  4.574 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.445 μs |  21.8997 μs |  1.2004 μs | 10.871 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,368.3 ns |    697.2 ns |  38.21 ns |  0.34 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,428.7 ns |  1,004.8 ns |  55.08 ns |  0.35 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,409.3 ns |    596.9 ns |  32.72 ns |  0.35 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,464.0 ns |    795.2 ns |  43.59 ns |  0.61 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    668.7 ns |    759.5 ns |  41.63 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,814.8 ns | 10,294.4 ns | 564.27 ns | 10.64 |    0.13 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,024.7 ns |    379.8 ns |  20.82 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,952.5 ns |    657.8 ns |  36.06 ns |  0.98 |    0.01 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.01 μs |   2.484 μs |  0.136 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   515.85 μs | 315.048 μs | 17.269 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.37 μs |   7.196 μs |  0.394 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,718.99 μs | 260.698 μs | 14.290 μs |    1280 B |


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