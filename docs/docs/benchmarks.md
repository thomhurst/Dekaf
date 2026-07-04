---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 11:45 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,316.11 μs** |   **184.845 μs** |  **10.132 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,338.42 μs | 1,604.519 μs |  87.949 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,467.24 μs** |   **912.079 μs** |  **49.994 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,351.06 μs |   983.843 μs |  53.928 μs |  0.31 |    0.01 |  15.6250 |       - |  339.43 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,146.27 μs** |   **197.569 μs** |  **10.829 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,310.22 μs | 1,486.906 μs |  81.502 μs |  0.21 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,718.21 μs** | **2,832.286 μs** | **155.247 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,234.01 μs | 1,052.972 μs |  57.717 μs |  0.49 |    0.01 |  15.6250 |       - |  361.76 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **146.22 μs** |   **198.116 μs** |  **10.859 μs** |  **1.00** |    **0.09** |   **2.4414** |       **-** |   **43.16 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     56.05 μs |    92.194 μs |   5.053 μs |  0.38 |    0.04 |        - |       - |    9.95 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,411.89 μs** | **4,101.392 μs** | **224.811 μs** |  **1.02** |    **0.20** |  **25.3906** |       **-** |  **427.81 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    624.48 μs |   690.832 μs |  37.867 μs |  0.45 |    0.07 |        - |       - |   47.08 KB |        0.11 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    205.07 μs |    23.960 μs |   1.313 μs |     ? |       ? |   0.9766 |       - |  108.16 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,065.79 μs | 2,149.245 μs | 117.807 μs |     ? |       ? |   7.8125 |       - | 1023.97 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,487.59 μs** |   **267.976 μs** |  **14.689 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,121.40 μs |     8.079 μs |   0.443 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,491.65 μs** |   **156.981 μs** |   **8.605 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,420.44 μs |   318.496 μs |  17.458 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,496.04 μs** |   **399.478 μs** |  **21.897 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,361.85 μs |   490.704 μs |  26.897 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,503.35 μs** |   **300.877 μs** |  **16.492 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,121.93 μs |    21.453 μs |   1.176 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.389 ms** |  **7.037 ms** | **0.3857 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18.162 ms | 51.738 ms | 2.8359 ms | 0.006 |  596.59 KB |        8.00 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167.911 ms** | **20.663 ms** | **1.1326 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.566 ms | 25.190 ms | 1.3807 ms | 0.005 |  777.87 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,168.734 ms** | **18.625 ms** | **1.0209 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    24.673 ms | 46.056 ms | 2.5245 ms | 0.008 |  995.23 KB |        1.65 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,168.495 ms** | **10.488 ms** | **0.5749 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.672 ms | 13.982 ms | 0.7664 ms | 0.005 | 2764.88 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.701 ms** | **52.513 ms** | **2.8784 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.174 ms |  7.400 ms | 0.4056 ms | 0.002 |   198.4 KB |       82.45 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,158.002 ms** | **25.420 ms** | **1.3934 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.422 ms | 29.219 ms | 1.6016 ms | 0.002 |  186.38 KB |       44.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,158.292 ms** | **18.146 ms** | **0.9946 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     7.230 ms |  5.340 ms | 0.2927 ms | 0.002 |  184.76 KB |       76.78 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.472 ms** | **21.040 ms** | **1.1533 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.919 ms | 16.208 ms | 0.8884 ms | 0.002 |  187.09 KB |       44.76 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 14.847 μs |  1.558 μs | 0.0854 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.426 μs | 36.900 μs | 2.0226 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.096 μs |  1.961 μs | 0.1075 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.737 μs |  1.348 μs | 0.0739 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.024 μs |  3.702 μs | 0.2029 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.477 μs | 63.211 μs | 3.4648 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.334 μs |  4.296 μs | 0.2355 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.645 μs | 11.239 μs | 0.6161 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.616 μs |  2.440 μs | 0.1337 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.639 μs |  5.355 μs | 0.2935 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,479.5 ns |  4,568.5 ns |   250.41 ns |  1,422.5 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,462.3 ns |  4,290.6 ns |   235.18 ns |  1,473.0 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,405.3 ns |  1,417.1 ns |    77.67 ns |  1,382.0 ns |  0.32 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,695.0 ns |  4,457.6 ns |   244.34 ns |  2,585.0 ns |  0.60 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    791.2 ns |  1,146.6 ns |    62.85 ns |    771.5 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 42,210.0 ns |  4,075.3 ns |   223.38 ns | 42,140.0 ns |  9.47 |    0.51 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,467.0 ns |  5,247.9 ns |   287.66 ns |  4,354.0 ns |  1.00 |    0.08 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  5,620.7 ns | 47,172.5 ns | 2,585.68 ns |  4,168.0 ns |  1.26 |    0.51 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error      | StdDev    | Allocated |
|------------------------ |------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.08 μs |   2.076 μs |  0.114 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   534.98 μs | 367.458 μs | 20.142 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.11 μs |   2.914 μs |  0.160 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,648.70 μs |  74.757 μs |  4.098 μs |    1280 B |


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