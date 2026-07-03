---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 23:48 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,187.75 μs** |   **441.20 μs** |  **24.184 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,361.68 μs | 3,532.37 μs | 193.621 μs |  0.22 |    0.03 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,447.23 μs** |   **232.45 μs** |  **12.741 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,334.25 μs |   540.73 μs |  29.639 μs |  0.31 |    0.00 |  15.6250 |       - |  339.66 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,189.81 μs** |   **689.41 μs** |  **37.789 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,257.59 μs | 2,066.24 μs | 113.258 μs |  0.20 |    0.02 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,523.44 μs** | **2,535.66 μs** | **138.988 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,644.83 μs | 9,836.31 μs | 539.162 μs |  0.53 |    0.04 |  15.6250 |       - |  361.65 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.69 μs** |    **77.51 μs** |   **4.249 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **42.48 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.56 μs |    83.20 μs |   4.560 μs |  0.49 |    0.03 |        - |       - |    6.48 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,458.36 μs** |   **152.83 μs** |   **8.377 μs** |  **1.00** |    **0.01** |  **23.4375** |       **-** |  **421.54 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    784.86 μs | 2,266.89 μs | 124.256 μs |  0.54 |    0.07 |        - |       - |   62.54 KB |        0.15 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    218.02 μs |   395.28 μs |  21.667 μs |     ? |       ? |   0.9766 |       - |  101.38 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,122.53 μs | 2,103.10 μs | 115.278 μs |     ? |       ? |   7.8125 |       - |  981.96 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,427.87 μs** |    **72.25 μs** |   **3.960 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,120.27 μs |    86.91 μs |   4.764 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,601.96 μs** | **5,154.09 μs** | **282.513 μs** |  **1.00** |    **0.06** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,122.74 μs |   163.49 μs |   8.961 μs |  0.20 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,422.43 μs** |    **33.32 μs** |   **1.826 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,116.82 μs |    27.23 μs |   1.493 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,424.60 μs** |    **42.57 μs** |   **2.333 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,120.15 μs |   110.82 μs |   6.075 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.538 ms** |   **4.926 ms** | **0.2700 ms** | **3,167.627 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.544 ms |  22.746 ms | 1.2468 ms |    14.713 ms | 0.005 |  599.19 KB |        8.03 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.419 ms** |  **35.918 ms** | **1.9688 ms** | **3,163.938 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.074 ms |  22.300 ms | 1.2223 ms |    13.972 ms | 0.004 |  777.34 KB |        3.10 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.725 ms** |   **3.967 ms** | **0.2174 ms** | **3,165.793 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.630 ms | 136.594 ms | 7.4872 ms |    13.680 ms | 0.006 | 1039.79 KB |        1.73 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.685 ms** |   **4.386 ms** | **0.2404 ms** | **3,165.727 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.427 ms |   9.034 ms | 0.4952 ms |    15.494 ms | 0.005 |  2762.1 KB |        1.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.075 ms** |  **14.545 ms** | **0.7972 ms** | **3,156.325 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.612 ms |   9.170 ms | 0.5027 ms |     5.575 ms | 0.002 |  197.95 KB |       82.26 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.339 ms** |  **35.730 ms** | **1.9585 ms** | **3,156.341 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.983 ms |  24.657 ms | 1.3516 ms |     5.438 ms | 0.002 |  187.77 KB |       45.09 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,154.832 ms** |  **31.803 ms** | **1.7432 ms** | **3,153.845 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.644 ms |  15.192 ms | 0.8327 ms |     6.229 ms | 0.002 |  229.71 KB |       95.46 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.060 ms** |  **22.210 ms** | **1.2174 ms** | **3,156.586 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.701 ms |   1.267 ms | 0.0694 ms |     5.720 ms | 0.002 |  187.04 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.854 μs |  2.5886 μs | 0.1419 μs | 14.827 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.664 μs |  3.6331 μs | 0.1991 μs |  9.597 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.316 μs |  2.6895 μs | 0.1474 μs | 10.369 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.850 μs |  4.5806 μs | 0.2511 μs | 26.869 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.874 μs |  2.2770 μs | 0.1248 μs |  8.807 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.399 μs |  0.2867 μs | 0.0157 μs | 19.396 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.522 μs | 50.7025 μs | 2.7792 μs | 18.233 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.242 μs |  9.0258 μs | 0.4947 μs | 20.449 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  6.347 μs | 55.0852 μs | 3.0194 μs |  4.635 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.070 μs | 24.4403 μs | 1.3397 μs | 12.224 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,348.3 ns |  3,110.0 ns | 170.47 ns |  0.27 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,235.5 ns |  1,075.8 ns |  58.97 ns |  0.25 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,386.7 ns |    382.8 ns |  20.98 ns |  0.28 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,474.0 ns |    657.8 ns |  36.06 ns |  0.50 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    781.7 ns |    968.9 ns |  53.11 ns |  0.16 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,825.3 ns | 14,398.9 ns | 789.25 ns |  8.20 |    1.19 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  5,062.3 ns | 13,990.5 ns | 766.87 ns |  1.02 |    0.20 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,459.8 ns | 10,735.6 ns | 588.46 ns |  0.90 |    0.17 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error        | StdDev      | Allocated |
|------------------------ |-------------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.104 μs |     6.540 μs |   0.3585 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   578.916 μs |   474.286 μs |  25.9972 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.424 μs |     2.356 μs |   0.1292 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,802.735 μs | 3,127.937 μs | 171.4528 μs |    1280 B |


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