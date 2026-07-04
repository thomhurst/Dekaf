---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 02:15 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Median       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,063.63 μs** |   **139.618 μs** |   **7.653 μs** |  **6,059.46 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,274.76 μs |   896.624 μs |  49.147 μs |  1,279.17 μs |  0.21 |    0.01 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,198.53 μs** | **1,337.308 μs** |  **73.302 μs** |  **7,169.49 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,335.78 μs |   434.004 μs |  23.789 μs |  2,338.10 μs |  0.32 |    0.00 |  15.6250 |       - |  339.78 KB |        0.32 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,620.40 μs** | **1,005.512 μs** |  **55.116 μs** |  **6,611.82 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,166.83 μs |   980.025 μs |  53.718 μs |  1,141.37 μs |  0.18 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,372.83 μs** | **2,851.363 μs** | **156.293 μs** | **11,360.68 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,540.33 μs |   984.945 μs |  53.988 μs |  5,558.71 μs |  0.49 |    0.01 |  15.6250 |       - |   361.8 KB |        0.19 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.19 μs** |   **155.331 μs** |   **8.514 μs** |    **132.75 μs** |  **1.00** |    **0.08** |   **2.6855** |       **-** |   **43.88 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     51.29 μs |    95.410 μs |   5.230 μs |     50.15 μs |  0.39 |    0.04 |   0.2441 |       - |    9.48 KB |        0.22 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,291.28 μs** | **2,566.557 μs** | **140.682 μs** |  **1,226.77 μs** |  **1.01** |    **0.13** |  **25.3906** |       **-** |  **445.67 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    667.77 μs |   836.817 μs |  45.869 μs |    671.81 μs |  0.52 |    0.06 |        - |       - |   59.92 KB |        0.13 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    253.04 μs | 2,621.129 μs | 143.673 μs |    178.72 μs |     ? |       ? |   0.4883 |       - |   11.97 KB |           ? |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,907.60 μs | 4,598.690 μs | 252.070 μs |  1,845.12 μs |     ? |       ? |   7.8125 |       - | 1009.98 KB |           ? |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,392.26 μs** |    **40.020 μs** |   **2.194 μs** |  **5,392.30 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,353.44 μs |   300.298 μs |  16.460 μs |  1,355.59 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,392.63 μs** |    **93.572 μs** |   **5.129 μs** |  **5,393.95 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,319.81 μs |     6.099 μs |   0.334 μs |  1,319.90 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,397.64 μs** |    **54.704 μs** |   **2.998 μs** |  **5,397.84 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,284.20 μs |   174.673 μs |   9.574 μs |  1,283.48 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,411.20 μs** |   **278.646 μs** |  **15.274 μs** |  **5,406.16 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,104.15 μs |    34.799 μs |   1.907 μs |  1,103.71 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.502 ms** | **12.166 ms** | **0.6669 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.941 ms | 39.802 ms | 2.1817 ms | 0.005 |   593.8 KB |        7.96 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.693 ms** |  **3.005 ms** | **0.1647 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.643 ms | 25.620 ms | 1.4043 ms | 0.004 |  781.44 KB |        3.12 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.760 ms** | **31.736 ms** | **1.7396 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.652 ms | 16.599 ms | 0.9099 ms | 0.005 |  995.41 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.216 ms** | **17.589 ms** | **0.9641 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.083 ms | 54.511 ms | 2.9879 ms | 0.005 | 2761.17 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.429 ms** | **13.331 ms** | **0.7307 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.228 ms | 27.568 ms | 1.5111 ms | 0.002 |  186.83 KB |       77.64 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.728 ms** | **15.118 ms** | **0.8287 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.812 ms | 11.910 ms | 0.6528 ms | 0.002 |  188.66 KB |       45.31 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.866 ms** | **31.750 ms** | **1.7403 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.939 ms | 12.331 ms | 0.6759 ms | 0.002 |  184.66 KB |       76.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.810 ms** | **13.008 ms** | **0.7130 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.708 ms | 11.377 ms | 0.6236 ms | 0.002 |  188.95 KB |       45.21 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 23.566 μs |  8.401 μs | 0.4605 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.982 μs | 19.990 μs | 1.0957 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.097 μs |  5.594 μs | 0.3066 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.604 μs |  4.251 μs | 0.2330 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.037 μs |  1.916 μs | 0.1050 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 24.670 μs | 70.148 μs | 3.8451 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.224 μs | 26.254 μs | 1.4391 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.491 μs | 17.466 μs | 0.9574 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.554 μs |  5.122 μs | 0.2807 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.746 μs |  9.108 μs | 0.4992 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,516.3 ns |  4,458.8 ns | 244.40 ns |  0.37 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,236.5 ns |  3,959.4 ns | 217.03 ns |  0.30 |    0.06 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,386.0 ns |  2,241.6 ns | 122.87 ns |  0.34 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,337.7 ns |  5,979.1 ns | 327.74 ns |  0.58 |    0.09 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    974.3 ns |  1,319.8 ns |  72.34 ns |  0.24 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,761.8 ns | 13,612.4 ns | 746.14 ns |  8.81 |    0.91 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,097.7 ns |  8,948.8 ns | 490.51 ns |  1.01 |    0.15 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,204.5 ns |  5,762.7 ns | 315.87 ns |  1.04 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.94 μs |  11.815 μs |  0.648 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   524.45 μs | 342.745 μs | 18.787 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.92 μs |   5.225 μs |  0.286 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,566.36 μs | 406.616 μs | 22.288 μs |    1280 B |


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