---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 17:44 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,245.80 μs** |   **632.38 μs** |  **34.663 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,388.92 μs | 1,650.13 μs |  90.449 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,427.92 μs** |   **514.58 μs** |  **28.206 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,348.22 μs | 1,013.85 μs |  55.572 μs |  0.32 |    0.01 |  15.6250 |       - |  339.42 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,231.39 μs** |   **438.73 μs** |  **24.048 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,210.83 μs | 1,160.33 μs |  63.602 μs |  0.19 |    0.01 |        - |       - |   36.32 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,878.52 μs** | **6,057.56 μs** | **332.035 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,610.28 μs | 6,943.31 μs | 380.586 μs |  0.51 |    0.03 |  15.6250 |       - |  361.84 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.62 μs** |    **11.33 μs** |   **0.621 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **41.87 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     62.01 μs |   107.68 μs |   5.902 μs |  0.43 |    0.04 |        - |       - |   10.41 KB |        0.25 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,406.00 μs** |   **347.42 μs** |  **19.044 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **421.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    698.81 μs | 1,114.66 μs |  61.098 μs |  0.50 |    0.04 |        - |       - |  100.52 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    219.38 μs |   334.49 μs |  18.334 μs |     ? |       ? |   0.9766 |       - |  100.61 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,987.96 μs | 2,770.58 μs | 151.865 μs |     ? |       ? |   7.8125 |       - | 1004.88 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,454.26 μs** |    **50.60 μs** |   **2.773 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,388.48 μs |   652.16 μs |  35.747 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,459.71 μs** |   **131.19 μs** |   **7.191 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,407.86 μs |   105.47 μs |   5.781 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,488.80 μs** |   **243.25 μs** |  **13.333 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,398.72 μs |   349.50 μs |  19.157 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,487.41 μs** |   **404.67 μs** |  **22.181 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,361.55 μs |   213.35 μs |  11.694 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.688 ms** | **25.677 ms** | **1.4075 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.800 ms |  7.846 ms | 0.4300 ms | 0.005 |  591.27 KB |        7.92 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.353 ms** | **21.010 ms** | **1.1516 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.993 ms | 54.346 ms | 2.9789 ms | 0.005 |  778.48 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.035 ms** |  **5.843 ms** | **0.3203 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20.254 ms | 93.143 ms | 5.1055 ms | 0.006 |  994.66 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.219 ms** | **34.950 ms** | **1.9157 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.380 ms | 54.023 ms | 2.9612 ms | 0.005 | 2759.77 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.579 ms** | **37.436 ms** | **2.0520 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.013 ms | 22.324 ms | 1.2236 ms | 0.002 |  184.38 KB |       76.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.892 ms** | **12.755 ms** | **0.6991 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.463 ms | 13.764 ms | 0.7545 ms | 0.002 |  188.66 KB |       45.31 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.724 ms** | **33.571 ms** | **1.8401 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.923 ms | 11.562 ms | 0.6337 ms | 0.002 |  184.59 KB |       76.71 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.584 ms** |  **3.043 ms** | **0.1668 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.680 ms | 13.364 ms | 0.7325 ms | 0.002 |  188.41 KB |       45.08 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.995 μs |   8.2915 μs |  0.4545 μs | 25.758 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.115 μs |   2.0606 μs |  0.1129 μs | 10.088 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.461 μs |   5.0600 μs |  0.2774 μs | 10.425 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.984 μs |   2.9607 μs |  0.1623 μs | 26.901 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.267 μs |  10.6728 μs |  0.5850 μs |  8.996 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.432 μs |   0.5673 μs |  0.0311 μs | 20.439 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 26.006 μs | 261.0452 μs | 14.3088 μs | 18.369 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.942 μs |  49.6433 μs |  2.7211 μs | 21.961 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.766 μs |   4.5135 μs |  0.2474 μs |  4.699 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.749 μs |   5.2268 μs |  0.2865 μs | 10.899 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,486.3 ns |  1,551.6 ns |  85.05 ns |  0.37 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,466.0 ns |  4,211.4 ns | 230.84 ns |  0.37 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,612.7 ns | 10,941.4 ns | 599.73 ns |  0.40 |    0.13 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,809.0 ns |  8,360.1 ns | 458.24 ns |  0.70 |    0.10 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    762.0 ns |  1,196.3 ns |  65.57 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,507.3 ns |  8,785.3 ns | 481.55 ns |  9.88 |    0.32 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,003.3 ns |  2,572.3 ns | 141.00 ns |  1.00 |    0.04 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,361.5 ns | 10,571.7 ns | 579.47 ns |  1.09 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev   | Allocated |
|------------------------ |-------------:|-----------:|---------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.041 μs |  25.528 μs | 1.399 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.468 μs |  32.740 μs | 1.795 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.732 μs |  22.897 μs | 1.255 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,657.398 μs | 136.131 μs | 7.462 μs |    1280 B |


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