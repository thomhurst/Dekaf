---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 12:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,137.43 μs** |   **705.82 μs** |  **38.688 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,334.71 μs |   937.61 μs |  51.394 μs |  0.22 |    0.01 |        - |       - |   34.91 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,386.85 μs** |   **365.73 μs** |  **20.047 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,372.52 μs |   836.73 μs |  45.864 μs |  0.32 |    0.01 |  15.6250 |       - |  339.78 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,508.50 μs** |   **405.83 μs** |  **22.245 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,143.20 μs |   399.28 μs |  21.886 μs |  0.18 |    0.00 |        - |       - |   36.91 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,575.24 μs** | **2,982.69 μs** | **163.492 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,493.00 μs | 6,049.81 μs | 331.611 μs |  0.52 |    0.02 |  15.6250 |       - |  369.42 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.27 μs** |   **178.49 μs** |   **9.784 μs** |  **1.00** |    **0.09** |   **2.4414** |       **-** |   **41.59 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     63.31 μs |   126.16 μs |   6.915 μs |  0.48 |    0.06 |   0.4883 |       - |   15.73 KB |        0.38 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,388.54 μs** | **1,034.54 μs** |  **56.706 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |  **429.65 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    706.81 μs |   871.41 μs |  47.765 μs |  0.51 |    0.03 |   3.9063 |       - |  137.14 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    319.90 μs |   383.80 μs |  21.037 μs |     ? |       ? |   6.8359 |  5.8594 |  200.95 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,144.89 μs | 3,769.97 μs | 206.645 μs |     ? |       ? |  70.3125 | 62.5000 | 1949.88 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,417.32 μs** |    **76.88 μs** |   **4.214 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,354.05 μs |   424.64 μs |  23.276 μs |  0.25 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,421.39 μs** |   **305.52 μs** |  **16.747 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,323.90 μs |   380.93 μs |  20.880 μs |  0.24 |    0.00 |        - |       - |    1.22 KB |        1.04 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,422.70 μs** |    **22.15 μs** |   **1.214 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,266.04 μs |   190.81 μs |  10.459 μs |  0.23 |    0.00 |        - |       - |    1.22 KB |        0.59 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,436.20 μs** |   **302.95 μs** |  **16.606 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,116.96 μs |   101.02 μs |   5.537 μs |  0.21 |    0.00 |        - |       - |    1.22 KB |        0.59 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.760 ms** | **35.788 ms** | **1.9617 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    12.787 ms | 19.782 ms | 1.0843 ms | 0.004 |   410.8 KB |        5.51 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.499 ms** | **19.334 ms** | **1.0598 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.193 ms | 23.515 ms | 1.2889 ms | 0.004 |  591.01 KB |        2.36 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.913 ms** | **14.608 ms** | **0.8007 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.183 ms | 20.918 ms | 1.1466 ms | 0.006 |  809.99 KB |        1.35 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166.883 ms** |  **4.632 ms** | **0.2539 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    14.867 ms | 17.745 ms | 0.9727 ms | 0.005 | 2616.39 KB |        1.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.386 ms** | **19.274 ms** | **1.0565 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.661 ms |  9.983 ms | 0.5472 ms | 0.002 |  182.55 KB |       75.87 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.752 ms** | **27.018 ms** | **1.4809 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.277 ms | 25.130 ms | 1.3774 ms | 0.002 |  183.29 KB |       44.02 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,153.213 ms** | **12.768 ms** | **0.6999 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.606 ms |  4.795 ms | 0.2628 ms | 0.002 |  217.48 KB |       90.38 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.597 ms** |  **4.514 ms** | **0.2474 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.655 ms |  8.224 ms | 0.4508 ms | 0.002 |  224.48 KB |       53.71 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 15.449 μs |  1.695 μs | 0.0929 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.062 μs |  2.966 μs | 0.1626 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.953 μs |  6.834 μs | 0.3746 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 32.735 μs |  6.131 μs | 0.3361 μs |         - |
| &#39;Read 1000 Int32s&#39;                        |  8.938 μs |  1.110 μs | 0.0608 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 21.567 μs |  8.571 μs | 0.4698 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.330 μs | 16.435 μs | 0.9008 μs |    2400 B |
| &#39;Read RecordBatch (10 records)&#39;           |  4.637 μs |  9.368 μs | 0.5135 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.757 μs | 14.771 μs | 0.8096 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  7,455.0 ns | 190,309.2 ns | 10,431.5 ns |  1,583.0 ns |  1.83 |    2.27 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,699.3 ns |   7,682.8 ns |    421.1 ns |  1,673.0 ns |  0.42 |    0.11 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,226.5 ns |   3,112.1 ns |    170.6 ns |  1,176.5 ns |  0.30 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,574.3 ns |   7,006.9 ns |    384.1 ns |  2,514.0 ns |  0.63 |    0.13 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    905.3 ns |   4,248.6 ns |    232.9 ns |    802.0 ns |  0.22 |    0.06 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,652.8 ns |   9,692.7 ns |    531.3 ns | 35,562.5 ns |  8.76 |    1.45 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,175.7 ns |  15,453.2 ns |    847.0 ns |  3,935.0 ns |  1.03 |    0.25 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,077.8 ns |   6,598.3 ns |    361.7 ns |  4,111.5 ns |  1.00 |    0.18 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.267 μs |   5.745 μs |  0.3149 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   527.766 μs | 414.718 μs | 22.7321 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.120 μs |  16.531 μs |  0.9061 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,571.305 μs | 432.626 μs | 23.7137 μs |    1280 B |


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