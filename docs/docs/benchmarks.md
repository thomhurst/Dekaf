---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 23:26 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,249.16 μs** |   **309.45 μs** |  **16.962 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,414.96 μs | 2,014.99 μs | 110.449 μs |  0.23 |    0.02 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,401.90 μs** |   **763.98 μs** |  **41.876 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,407.93 μs | 2,387.50 μs | 130.867 μs |  0.33 |    0.02 |  15.6250 |       - |  339.51 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,322.69 μs** |    **90.13 μs** |   **4.940 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,303.39 μs | 1,911.95 μs | 104.801 μs |  0.21 |    0.01 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,390.40 μs** |   **943.69 μs** |  **51.727 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,670.48 μs | 2,701.29 μs | 148.067 μs |  0.54 |    0.01 |  15.6250 |       - |  361.79 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **145.33 μs** |   **102.56 μs** |   **5.622 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **42.03 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     54.35 μs |    54.30 μs |   2.977 μs |  0.37 |    0.02 |        - |       - |    8.37 KB |        0.20 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,406.10 μs** |   **669.03 μs** |  **36.672 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |  **420.72 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    614.58 μs |   630.95 μs |  34.585 μs |  0.44 |    0.02 |        - |       - |    97.8 KB |        0.23 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    203.38 μs |   284.46 μs |  15.592 μs |     ? |       ? |   0.9766 |       - |  117.59 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,173.14 μs | 5,156.06 μs | 282.621 μs |     ? |       ? |   7.8125 |       - | 1010.21 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,430.13 μs** |    **59.20 μs** |   **3.245 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,314.24 μs |   690.62 μs |  37.855 μs |  0.24 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,438.53 μs** |    **20.35 μs** |   **1.116 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,328.85 μs |   463.20 μs |  25.389 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,449.07 μs** |   **298.26 μs** |  **16.349 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,329.23 μs |   145.32 μs |   7.966 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,439.06 μs** |    **55.57 μs** |   **3.046 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,318.98 μs |   336.48 μs |  18.444 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.665 ms** | **37.315 ms** | **2.0453 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.712 ms | 38.858 ms | 2.1299 ms | 0.005 |  592.71 KB |        7.94 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.893 ms** | **24.716 ms** | **1.3548 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.541 ms |  8.832 ms | 0.4841 ms | 0.005 |  775.74 KB |        3.10 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.815 ms** | **16.650 ms** | **0.9127 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19.098 ms | 87.087 ms | 4.7735 ms | 0.006 |  995.44 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.666 ms** | **17.775 ms** | **0.9743 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.762 ms | 36.597 ms | 2.0060 ms | 0.005 | 2761.48 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.299 ms** | **15.577 ms** | **0.8538 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.006 ms |  2.198 ms | 0.1205 ms | 0.002 |  184.34 KB |       76.61 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.586 ms** | **51.685 ms** | **2.8330 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.925 ms |  9.671 ms | 0.5301 ms | 0.002 |  188.59 KB |       45.29 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.272 ms** | **38.547 ms** | **2.1129 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.277 ms | 14.635 ms | 0.8022 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.893 ms** | **33.956 ms** | **1.8612 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.193 ms | 17.644 ms | 0.9671 ms | 0.002 |   187.1 KB |       44.76 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.679 μs |  3.336 μs | 0.1828 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.925 μs |  5.026 μs | 0.2755 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.909 μs |  8.242 μs | 0.4518 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.548 μs |  2.763 μs | 0.1515 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.150 μs |  6.597 μs | 0.3616 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.542 μs |  8.890 μs | 0.4873 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.809 μs | 23.046 μs | 1.2633 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.391 μs | 18.737 μs | 1.0270 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.524 μs | 10.903 μs | 0.5976 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.426 μs | 12.518 μs | 0.6862 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,292.3 ns |   2,564.5 ns |    140.57 ns |  1,232.0 ns |  0.30 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,342.3 ns |   3,806.8 ns |    208.66 ns |  1,232.0 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,335.2 ns |   3,084.8 ns |    169.09 ns |  1,292.5 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  3,525.3 ns |  26,578.2 ns |  1,456.84 ns |  2,844.0 ns |  0.81 |    0.29 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    728.8 ns |   1,241.8 ns |     68.07 ns |    705.5 ns |  0.17 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,767.7 ns |   6,669.6 ns |    365.58 ns | 35,614.0 ns |  8.24 |    0.39 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,350.7 ns |   4,134.1 ns |    226.61 ns |  4,411.0 ns |  1.00 |    0.06 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  9,992.3 ns | 185,371.4 ns | 10,160.83 ns |  4,317.0 ns |  2.30 |    2.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.898 μs |  14.207 μs |  0.7787 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   538.414 μs | 437.663 μs | 23.9898 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.889 μs |  14.654 μs |  0.8032 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,602.231 μs | 254.945 μs | 13.9744 μs |    1280 B |


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