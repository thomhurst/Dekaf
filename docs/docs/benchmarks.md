---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 19:15 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,311.40 μs** |   **405.09 μs** |  **22.205 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,388.48 μs | 2,414.87 μs | 132.367 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,484.03 μs** | **1,703.88 μs** |  **93.395 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,385.58 μs |    80.01 μs |   4.386 μs |  0.32 |    0.00 |  15.6250 |       - |  339.54 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,136.85 μs** |   **545.08 μs** |  **29.878 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.07 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,331.63 μs | 1,987.21 μs | 108.926 μs |  0.22 |    0.02 |        - |       - |   36.33 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,122.30 μs** | **2,243.48 μs** | **122.973 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,818.05 μs | 6,171.94 μs | 338.305 μs |  0.52 |    0.02 |  15.6250 |       - |  361.64 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **148.40 μs** |    **71.97 μs** |   **3.945 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.67 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     67.66 μs |   106.96 μs |   5.863 μs |  0.46 |    0.04 |        - |       - |    9.38 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,498.03 μs** |   **634.05 μs** |  **34.754 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |  **419.73 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    689.75 μs |   982.87 μs |  53.875 μs |  0.46 |    0.03 |        - |       - |   94.36 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    197.43 μs |   259.48 μs |  14.223 μs |     ? |       ? |   0.9766 |       - |  105.11 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,876.44 μs | 2,606.33 μs | 142.862 μs |     ? |       ? |   7.8125 |       - | 1000.56 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,481.46 μs** |    **88.77 μs** |   **4.866 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,402.99 μs |   252.01 μs |  13.813 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,479.95 μs** |   **450.15 μs** |  **24.674 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,394.44 μs |   583.19 μs |  31.966 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,734.02 μs** | **8,100.75 μs** | **444.030 μs** |  **1.00** |    **0.09** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,387.75 μs |   381.87 μs |  20.931 μs |  0.24 |    0.02 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,489.39 μs** |   **387.53 μs** |  **21.242 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,400.26 μs |   295.67 μs |  16.207 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.584 ms** | **40.5416 ms** | **2.2222 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.372 ms | 44.6617 ms | 2.4481 ms | 0.005 |  593.08 KB |        7.95 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.068 ms** | **20.4732 ms** | **1.1222 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.536 ms | 58.3358 ms | 3.1976 ms | 0.005 |  775.49 KB |        3.10 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167.776 ms** | **16.3176 ms** | **0.8944 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.605 ms | 43.6787 ms | 2.3942 ms | 0.005 |  995.75 KB |        1.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.144 ms** |  **9.0178 ms** | **0.4943 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.760 ms | 53.2063 ms | 2.9164 ms | 0.006 | 2761.39 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.966 ms** | **33.1540 ms** | **1.8173 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.372 ms |  0.3042 ms | 0.0167 ms | 0.002 |  193.34 KB |       80.35 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,157.513 ms** |  **4.6437 ms** | **0.2545 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.637 ms |  5.5499 ms | 0.3042 ms | 0.002 |   187.7 KB |       45.08 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.325 ms** | **39.7479 ms** | **2.1787 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.870 ms | 36.8754 ms | 2.0213 ms | 0.002 |  184.57 KB |       76.70 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.037 ms** | **28.3788 ms** | **1.5555 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.457 ms |  7.3376 ms | 0.4022 ms | 0.002 |  186.91 KB |       44.72 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.586 μs |  6.331 μs | 0.3470 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.072 μs |  4.645 μs | 0.2546 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.070 μs |  3.755 μs | 0.2058 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.458 μs |  8.701 μs | 0.4769 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.013 μs |  1.756 μs | 0.0962 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.219 μs |  1.724 μs | 0.0945 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.975 μs | 19.087 μs | 1.0462 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.725 μs | 16.768 μs | 0.9191 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.570 μs | 10.084 μs | 0.5528 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.479 μs | 10.464 μs | 0.5735 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,137.3 ns |  2,456.5 ns | 134.6 ns |  0.27 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,388.2 ns |  3,021.7 ns | 165.6 ns |  0.32 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,253.5 ns |  6,184.2 ns | 339.0 ns |  0.29 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,499.8 ns |  3,030.3 ns | 166.1 ns |  0.59 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    723.3 ns |  2,016.8 ns | 110.5 ns |  0.17 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,983.3 ns | 10,692.1 ns | 586.1 ns |  8.42 |    0.59 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,291.8 ns |  6,504.3 ns | 356.5 ns |  1.00 |    0.10 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,144.5 ns |  5,327.3 ns | 292.0 ns |  0.97 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.674 μs |  11.087 μs |  0.6077 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   539.330 μs | 907.931 μs | 49.7668 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.446 μs |   6.333 μs |  0.3472 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,640.111 μs | 245.210 μs | 13.4408 μs |    1280 B |


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