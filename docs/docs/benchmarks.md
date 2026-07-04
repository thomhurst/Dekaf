---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 16:40 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,098.68 μs** |   **631.73 μs** |  **34.627 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,360.38 μs |   249.26 μs |  13.663 μs |  0.22 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,358.23 μs** |   **291.79 μs** |  **15.994 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,306.37 μs |   222.37 μs |  12.189 μs |  0.31 |    0.00 |  15.6250 |       - |  339.41 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,564.00 μs** | **1,419.69 μs** |  **77.818 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,213.20 μs | 1,039.95 μs |  57.003 μs |  0.18 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,159.77 μs** | **2,426.87 μs** | **133.025 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,887.34 μs | 1,435.64 μs |  78.692 μs |  0.48 |    0.01 |  15.6250 |       - |  361.79 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **139.69 μs** |   **139.56 μs** |   **7.650 μs** |  **1.00** |    **0.07** |   **2.4414** |       **-** |    **42.1 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.70 μs |    45.31 μs |   2.484 μs |  0.46 |    0.03 |        - |       - |    7.03 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,359.03 μs** |   **888.02 μs** |  **48.675 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **415.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    682.75 μs | 1,015.35 μs |  55.655 μs |  0.50 |    0.04 |        - |       - |  104.05 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    187.66 μs |   220.66 μs |  12.095 μs |     ? |       ? |   0.9766 |       - |  102.96 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,140.14 μs | 6,638.73 μs | 363.891 μs |     ? |       ? |   7.8125 |       - | 1086.57 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,407.26 μs** |   **208.62 μs** |  **11.435 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,387.61 μs |   492.92 μs |  27.019 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,400.13 μs** |    **38.32 μs** |   **2.100 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,371.30 μs |   474.43 μs |  26.005 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,406.37 μs** |    **96.71 μs** |   **5.301 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,369.35 μs |   650.18 μs |  35.639 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,400.19 μs** |    **58.94 μs** |   **3.231 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,356.77 μs |   496.84 μs |  27.233 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error        | StdDev      | Median       | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-------------:|------------:|-------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.215 ms** |     **5.302 ms** |   **0.2906 ms** | **3,166.276 ms** | **1.000** |    **0.00** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.514 ms |    38.876 ms |   2.1309 ms |    13.431 ms | 0.005 |    0.00 |  591.08 KB |        7.92 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.045 ms** |    **11.066 ms** |   **0.6065 ms** | **3,164.794 ms** | **1.000** |    **0.00** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.935 ms |    40.242 ms |   2.2058 ms |    13.681 ms | 0.004 |    0.00 |  773.21 KB |        3.09 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.621 ms** |    **18.802 ms** |   **1.0306 ms** | **3,165.126 ms** | **1.000** |    **0.00** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.880 ms |    12.271 ms |   0.6726 ms |    15.519 ms | 0.005 |    0.00 |  993.94 KB |        1.65 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.025 ms** |    **11.938 ms** |   **0.6544 ms** | **3,164.659 ms** |  **1.00** |    **0.00** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    82.867 ms | 2,126.161 ms | 116.5421 ms |    16.400 ms |  0.03 |    0.03 | 2758.39 KB |        1.17 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.947 ms** |    **23.022 ms** |   **1.2619 ms** | **3,154.398 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.416 ms |     6.510 ms |   0.3568 ms |     5.599 ms | 0.002 |    0.00 |  184.41 KB |       76.64 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.166 ms** |    **16.861 ms** |   **0.9242 ms** | **3,156.240 ms** | **1.000** |    **0.00** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.236 ms |    34.119 ms |   1.8702 ms |     5.157 ms | 0.002 |    0.00 |  186.42 KB |       44.77 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.693 ms** |    **19.263 ms** |   **1.0558 ms** | **3,156.181 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.895 ms |    15.533 ms |   0.8514 ms |     7.017 ms | 0.002 |    0.00 |     186 KB |       77.30 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.649 ms** |    **13.544 ms** |   **0.7424 ms** | **3,156.820 ms** | **1.000** |    **0.00** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.630 ms |     7.109 ms |   0.3896 ms |     5.512 ms | 0.002 |    0.00 |  261.36 KB |       62.53 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.387 μs |  4.664 μs | 0.2557 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.833 μs |  5.727 μs | 0.3139 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.363 μs |  5.489 μs | 0.3009 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.069 μs |  7.259 μs | 0.3979 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.306 μs |  7.149 μs | 0.3919 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.344 μs |  4.890 μs | 0.2681 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.962 μs | 29.386 μs | 1.6108 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.751 μs | 14.647 μs | 0.8028 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.961 μs |  4.667 μs | 0.2558 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.907 μs |  9.877 μs | 0.5414 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,233.8 ns |   2,084.9 ns |    114.28 ns |  0.30 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,375.3 ns |   1,187.0 ns |     65.06 ns |  0.33 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,285.7 ns |   3,551.7 ns |    194.68 ns |  0.31 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,471.8 ns |   3,520.3 ns |    192.96 ns |  0.60 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    601.0 ns |   2,031.5 ns |    111.36 ns |  0.15 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 51,973.0 ns | 287,017.4 ns | 15,732.39 ns | 12.64 |    3.54 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,149.3 ns |   8,986.1 ns |    492.56 ns |  1.01 |    0.14 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,866.3 ns |   7,817.0 ns |    428.47 ns |  0.94 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.075 μs |  20.017 μs |  1.0972 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   525.037 μs | 333.729 μs | 18.2928 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.931 μs |   4.363 μs |  0.2392 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,584.350 μs | 366.655 μs | 20.0976 μs |    1280 B |


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