---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 17:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,091.26 μs** |   **491.00 μs** |  **26.913 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,279.66 μs | 1,123.82 μs |  61.600 μs |  0.21 |    0.01 |        - |       - |    34.7 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,321.47 μs** | **1,945.07 μs** | **106.616 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,335.47 μs |    11.32 μs |   0.621 μs |  0.32 |    0.00 |  15.6250 |       - |  339.39 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,557.42 μs** |   **285.18 μs** |  **15.632 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,188.52 μs |   729.01 μs |  39.960 μs |  0.18 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,921.40 μs** | **2,924.10 μs** | **160.280 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,018.99 μs |   554.41 μs |  30.389 μs |  0.50 |    0.01 |  15.6250 |       - |  361.73 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.77 μs** |    **73.53 μs** |   **4.030 μs** |  **1.00** |    **0.04** |   **2.4414** |       **-** |   **42.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.37 μs |    14.91 μs |   0.817 μs |  0.41 |    0.01 |   0.2441 |       - |    8.79 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,300.43 μs** |   **442.43 μs** |  **24.251 μs** |  **1.00** |    **0.02** |  **23.4375** |       **-** |   **408.7 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    666.92 μs |   461.59 μs |  25.301 μs |  0.51 |    0.02 |        - |       - |   78.66 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    204.64 μs |   374.95 μs |  20.552 μs |     ? |       ? |   0.9766 |       - |  103.78 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,165.59 μs | 3,379.92 μs | 185.265 μs |     ? |       ? |   7.8125 |       - | 1005.93 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,416.00 μs** |    **83.52 μs** |   **4.578 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,380.40 μs |   474.19 μs |  25.992 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,417.84 μs** |   **236.27 μs** |  **12.951 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,362.64 μs |   680.32 μs |  37.290 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.66 μs** |   **135.25 μs** |   **7.413 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,376.51 μs |   198.10 μs |  10.859 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,402.63 μs** |   **133.99 μs** |   **7.344 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,384.16 μs |   671.11 μs |  36.786 μs |  0.26 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.161 ms** | **11.403 ms** | **0.6250 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.530 ms | 46.449 ms | 2.5460 ms | 0.005 |  591.01 KB |        7.92 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,168.466 ms** | **90.002 ms** | **4.9333 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.569 ms | 16.051 ms | 0.8798 ms | 0.004 |  777.52 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.343 ms** | **32.330 ms** | **1.7721 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.305 ms | 74.814 ms | 4.1008 ms | 0.006 |     994 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.867 ms** |  **7.060 ms** | **0.3870 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.875 ms | 22.107 ms | 1.2118 ms | 0.005 | 2787.38 KB |        1.18 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.103 ms** | **37.550 ms** | **2.0582 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.389 ms |  9.403 ms | 0.5154 ms | 0.002 |  184.98 KB |       76.87 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.482 ms** | **44.309 ms** | **2.4287 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.912 ms | 43.032 ms | 2.3587 ms | 0.002 |  188.56 KB |       45.28 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.467 ms** | **12.102 ms** | **0.6634 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.606 ms | 13.020 ms | 0.7137 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.560 ms** | **12.391 ms** | **0.6792 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.199 ms | 12.059 ms | 0.6610 ms | 0.002 |  214.09 KB |       51.22 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.422 μs |   9.408 μs | 0.5157 μs | 32.438 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.751 μs |   1.308 μs | 0.0717 μs | 10.771 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.178 μs |   4.591 μs | 0.2516 μs | 11.150 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.686 μs |   9.779 μs | 0.5360 μs | 32.689 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 15.262 μs | 165.033 μs | 9.0460 μs | 10.074 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.130 μs |   6.503 μs | 0.3565 μs | 20.160 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.686 μs |  16.613 μs | 0.9106 μs | 17.175 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.766 μs |  29.628 μs | 1.6240 μs | 18.988 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.671 μs |   9.046 μs | 0.4958 μs |  4.787 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.381 μs |  21.890 μs | 1.1998 μs | 12.768 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,237.0 ns |  2,867.2 ns | 157.16 ns |  0.31 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,292.0 ns |  2,031.5 ns | 111.36 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,226.8 ns |  6,411.1 ns | 351.41 ns |  0.31 |    0.08 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,507.3 ns |  5,180.5 ns | 283.96 ns |  0.63 |    0.09 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    701.3 ns |  1,274.5 ns |  69.86 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,734.2 ns |  9,125.6 ns | 500.20 ns |  8.98 |    0.87 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,013.0 ns |  8,653.3 ns | 474.32 ns |  1.01 |    0.14 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,135.3 ns | 16,551.1 ns | 907.22 ns |  1.04 |    0.22 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.05 μs |   6.216 μs |  0.341 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   514.35 μs | 137.105 μs |  7.515 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.01 μs |   4.132 μs |  0.226 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,558.46 μs | 212.920 μs | 11.671 μs |    1280 B |


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