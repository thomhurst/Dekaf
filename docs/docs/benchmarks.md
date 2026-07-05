---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-05 15:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,112.79 μs** |    **248.37 μs** |  **13.614 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,427.22 μs |  1,852.88 μs | 101.563 μs |  0.23 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,381.36 μs** |  **1,805.77 μs** |  **98.980 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,254.18 μs |    282.37 μs |  15.478 μs |  0.31 |    0.00 |  19.5313 |  3.9063 |  339.32 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,958.14 μs** | **12,303.60 μs** | **674.402 μs** |  **1.01** |    **0.12** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,199.77 μs |  1,080.74 μs |  59.239 μs |  0.17 |    0.02 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,253.27 μs** |  **3,040.41 μs** | **166.655 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,883.69 μs |    872.98 μs |  47.851 μs |  0.48 |    0.01 |  15.6250 |       - |  361.73 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **144.90 μs** |    **126.33 μs** |   **6.925 μs** |  **1.00** |    **0.06** |   **2.4414** |       **-** |   **42.01 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.17 μs |     75.39 μs |   4.132 μs |  0.43 |    0.03 |        - |       - |    8.44 KB |        0.20 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,516.32 μs** |  **2,817.20 μs** | **154.420 μs** |  **1.01** |    **0.13** |  **25.3906** |       **-** |  **427.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    682.67 μs |  1,972.34 μs | 108.111 μs |  0.45 |    0.07 |        - |       - |   62.36 KB |        0.15 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    193.01 μs |     68.93 μs |   3.778 μs |     ? |       ? |   0.9766 |       - |   98.44 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,042.55 μs |  4,315.23 μs | 236.532 μs |     ? |       ? |   7.8125 |       - |  983.71 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,418.92 μs** |    **630.97 μs** |  **34.586 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,294.02 μs |    260.18 μs |  14.262 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,399.72 μs** |     **22.32 μs** |   **1.224 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,114.16 μs |     75.62 μs |   4.145 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.06 μs** |     **40.06 μs** |   **2.196 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,110.86 μs |     10.90 μs |   0.597 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,401.44 μs** |     **25.95 μs** |   **1.422 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,111.56 μs |     22.55 μs |   1.236 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.490 ms** |  **35.652 ms** | **1.9542 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.856 ms |  62.921 ms | 3.4489 ms | 0.005 |  604.69 KB |        8.10 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.990 ms** |   **8.192 ms** | **0.4490 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.527 ms |  49.240 ms | 2.6990 ms | 0.005 |  800.27 KB |        3.20 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.354 ms** |   **3.094 ms** | **0.1696 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.960 ms | 122.849 ms | 6.7338 ms | 0.006 | 1006.41 KB |        1.67 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.475 ms** |  **20.789 ms** | **1.1395 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.626 ms |  26.021 ms | 1.4263 ms | 0.005 | 2788.57 KB |        1.18 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,154.298 ms** |  **26.120 ms** | **1.4317 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.037 ms |  19.302 ms | 1.0580 ms | 0.002 |   188.9 KB |       78.50 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.149 ms** |  **24.607 ms** | **1.3488 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.897 ms |  26.069 ms | 1.4289 ms | 0.002 |  188.82 KB |       45.35 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.491 ms** |  **27.143 ms** | **1.4878 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.446 ms |   9.987 ms | 0.5474 ms | 0.002 |  211.67 KB |       87.97 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.189 ms** |  **46.167 ms** | **2.5306 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.945 ms |  16.777 ms | 0.9196 ms | 0.002 |  209.61 KB |       50.15 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.862 μs |  0.9362 μs | 0.0513 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.223 μs |  1.6040 μs | 0.0879 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.402 μs |  2.4295 μs | 0.1332 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.606 μs |  1.3198 μs | 0.0723 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.947 μs |  1.6820 μs | 0.0922 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.757 μs | 77.6724 μs | 4.2575 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.768 μs |  2.0201 μs | 0.1107 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.175 μs |  4.8789 μs | 0.2674 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.882 μs |  7.0815 μs | 0.3882 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.621 μs |  3.6579 μs | 0.2005 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,233.8 ns |    752.3 ns |  41.24 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,346.0 ns |    649.4 ns |  35.59 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,428.3 ns |  1,795.9 ns |  98.44 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,981.7 ns |  8,220.9 ns | 450.62 ns |  0.71 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    719.8 ns |    384.6 ns |  21.08 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,650.0 ns | 14,194.8 ns | 778.07 ns |  9.74 |    0.36 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,176.8 ns |  2,971.7 ns | 162.89 ns |  1.00 |    0.05 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,452.0 ns |  5,737.0 ns | 314.46 ns |  1.07 |    0.07 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.180 μs |  29.211 μs |  1.6012 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   553.414 μs | 544.572 μs | 29.8498 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.238 μs |   3.992 μs |  0.2188 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,675.332 μs | 439.644 μs | 24.0984 μs |    1280 B |


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