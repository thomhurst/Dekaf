---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 01:12 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,153.50 μs** |    **662.50 μs** |    **36.314 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,338.90 μs |    361.08 μs |    19.792 μs |  0.22 |    0.00 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,325.42 μs** |    **996.76 μs** |    **54.636 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,331.38 μs |    841.69 μs |    46.136 μs |  0.32 |    0.01 |  15.6250 |       - |  339.56 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,187.61 μs** |  **1,500.70 μs** |    **82.258 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,496.22 μs |  2,375.07 μs |   130.186 μs |  0.24 |    0.02 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,729.45 μs** |  **1,446.19 μs** |    **79.271 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,549.76 μs | 22,545.46 μs | 1,235.793 μs |  0.59 |    0.08 |  15.6250 |       - |  361.78 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.31 μs** |    **219.49 μs** |    **12.031 μs** |  **1.01** |    **0.11** |   **2.4414** |       **-** |   **41.51 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     55.54 μs |     27.52 μs |     1.509 μs |  0.43 |    0.03 |   0.2441 |       - |    9.36 KB |        0.23 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,411.50 μs** |    **531.45 μs** |    **29.131 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **422.68 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    616.86 μs |    464.21 μs |    25.445 μs |  0.44 |    0.02 |        - |       - |   73.37 KB |        0.17 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    203.58 μs |    189.53 μs |    10.389 μs |     ? |       ? |   0.9766 |       - |  100.91 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,032.32 μs |  2,097.00 μs |   114.944 μs |     ? |       ? |   7.8125 |       - |  986.94 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,429.17 μs** |     **56.62 μs** |     **3.104 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,118.48 μs |     13.81 μs |     0.757 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.34 μs** |    **131.52 μs** |     **7.209 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,119.88 μs |     62.23 μs |     3.411 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,431.29 μs** |     **65.99 μs** |     **3.617 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,118.15 μs |     46.94 μs |     2.573 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,439.59 μs** |     **74.56 μs** |     **4.087 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,163.83 μs |    950.85 μs |    52.119 μs |  0.21 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.776 ms** |  **14.381 ms** | **0.7883 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.433 ms |  22.538 ms | 1.2354 ms | 0.005 |  592.44 KB |        7.94 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.890 ms** |  **14.477 ms** | **0.7935 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.063 ms |  13.766 ms | 0.7546 ms | 0.004 |  783.84 KB |        3.13 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.074 ms** |  **17.911 ms** | **0.9818 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.524 ms | 137.769 ms | 7.5516 ms | 0.006 |  994.91 KB |        1.65 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.356 ms** |  **11.499 ms** | **0.6303 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.820 ms |  38.052 ms | 2.0858 ms | 0.005 | 2763.02 KB |        1.17 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.051 ms** |  **16.739 ms** | **0.9175 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.320 ms |   3.884 ms | 0.2129 ms | 0.002 |  193.37 KB |       80.36 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.606 ms** |   **7.603 ms** | **0.4168 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.145 ms |  25.533 ms | 1.3995 ms | 0.002 |  186.35 KB |       44.75 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.297 ms** |  **22.981 ms** | **1.2596 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.997 ms |  16.014 ms | 0.8778 ms | 0.002 |  184.77 KB |       76.79 |
|                      |            |              |             |              |            |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.775 ms** |  **19.867 ms** | **1.0890 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.794 ms |   7.094 ms | 0.3888 ms | 0.002 |  186.97 KB |       44.73 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.435 μs |   1.0048 μs |  0.0551 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  9.244 μs |   4.6730 μs |  0.2561 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.163 μs |   4.7057 μs |  0.2579 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.841 μs | 195.4939 μs | 10.7157 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.993 μs |   2.1242 μs |  0.1164 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.443 μs |   5.0438 μs |  0.2765 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.676 μs |  12.0602 μs |  0.6611 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.073 μs |  10.6920 μs |  0.5861 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.492 μs |   0.7426 μs |  0.0407 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.567 μs |   7.1541 μs |  0.3921 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,710.3 ns | 3,803.7 ns | 208.50 ns |  0.41 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,312.3 ns | 2,339.2 ns | 128.22 ns |  0.32 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,396.3 ns | 2,009.6 ns | 110.15 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,588.5 ns |   460.5 ns |  25.24 ns |  0.63 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    770.7 ns | 1,378.6 ns |  75.57 ns |  0.19 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,915.0 ns | 5,122.8 ns | 280.80 ns |  9.92 |    0.10 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,124.2 ns |   737.3 ns |  40.41 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,049.2 ns | 1,655.4 ns |  90.74 ns |  0.98 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.279 μs |   1.743 μs |  0.0956 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   567.351 μs | 538.644 μs | 29.5249 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.243 μs |   4.591 μs |  0.2517 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,645.057 μs | 377.547 μs | 20.6946 μs |    1280 B |


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