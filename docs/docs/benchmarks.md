---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 17:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,129.85 μs** |   **760.19 μs** |  **41.669 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,380.53 μs | 3,251.72 μs | 178.238 μs |  0.23 |    0.03 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,365.93 μs** |   **736.36 μs** |  **40.363 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,341.44 μs |    76.59 μs |   4.198 μs |  0.32 |    0.00 |  15.6250 |       - |  339.66 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,285.06 μs** |   **632.61 μs** |  **34.676 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,357.41 μs | 3,082.11 μs | 168.941 μs |  0.22 |    0.02 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,430.71 μs** | **2,494.25 μs** | **136.718 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,320.77 μs | 5,792.58 μs | 317.511 μs |  0.51 |    0.02 |  15.6250 |       - |  361.75 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.20 μs** |    **16.61 μs** |   **0.910 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **42.01 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.08 μs |    84.65 μs |   4.640 μs |  0.48 |    0.03 |        - |       - |    6.95 KB |        0.17 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,408.19 μs** |   **692.42 μs** |  **37.954 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |  **420.37 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    712.08 μs | 1,176.10 μs |  64.466 μs |  0.51 |    0.04 |        - |       - |   80.75 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    212.91 μs |   607.93 μs |  33.323 μs |     ? |       ? |   0.9766 |       - |  100.31 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,183.92 μs | 2,906.40 μs | 159.310 μs |     ? |       ? |   7.8125 |       - |  988.72 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,418.82 μs** |   **122.82 μs** |   **6.732 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,116.11 μs |    25.21 μs |   1.382 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,417.88 μs** |    **60.47 μs** |   **3.315 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,133.62 μs |   176.44 μs |   9.671 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,438.82 μs** |   **588.28 μs** |  **32.246 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,168.61 μs |   129.32 μs |   7.088 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,420.56 μs** |    **56.86 μs** |   **3.116 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,177.73 μs |   192.37 μs |  10.545 μs |  0.22 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.604 ms** |  **9.872 ms** | **0.5411 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.076 ms | 13.710 ms | 0.7515 ms | 0.005 |  602.08 KB |        8.07 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.740 ms** |  **7.882 ms** | **0.4320 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.257 ms | 37.053 ms | 2.0310 ms | 0.005 |  774.88 KB |        3.09 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.430 ms** | **42.838 ms** | **2.3481 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.318 ms | 72.335 ms | 3.9649 ms | 0.006 | 1016.67 KB |        1.69 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.928 ms** |  **3.290 ms** | **0.1803 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.875 ms | 27.603 ms | 1.5130 ms | 0.005 | 2833.79 KB |        1.20 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.409 ms** | **40.390 ms** | **2.2139 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.236 ms | 10.349 ms | 0.5673 ms | 0.002 |  185.55 KB |       77.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,153.738 ms** | **69.856 ms** | **3.8291 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.947 ms |  7.275 ms | 0.3987 ms | 0.002 |  186.38 KB |       44.76 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,157.892 ms** | **19.713 ms** | **1.0806 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.529 ms |  6.607 ms | 0.3622 ms | 0.002 |  189.19 KB |       78.62 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.913 ms** | **16.920 ms** | **0.9275 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.898 ms | 11.462 ms | 0.6283 ms | 0.002 |  187.03 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.809 μs |   0.4827 μs |  0.0265 μs | 25.799 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 16.945 μs | 207.3163 μs | 11.3637 μs | 10.400 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.419 μs |   4.5900 μs |  0.2516 μs | 10.289 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.710 μs |   1.1963 μs |  0.0656 μs | 26.720 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 15.267 μs |   5.3860 μs |  0.2952 μs | 15.143 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.596 μs |  69.4398 μs |  3.8062 μs | 20.418 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 18.842 μs |  31.7517 μs |  1.7404 μs | 17.934 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.377 μs |  11.7177 μs |  0.6423 μs | 20.167 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.957 μs |   3.2122 μs |  0.1761 μs |  4.973 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.999 μs |   0.2787 μs |  0.0153 μs | 10.995 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,130.2 ns |  1,304.2 ns |    71.49 ns |  0.27 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,210.3 ns |    737.3 ns |    40.41 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,267.3 ns |    958.5 ns |    52.54 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,621.0 ns |    927.9 ns |    50.86 ns |  0.62 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    768.7 ns |    640.7 ns |    35.12 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,281.5 ns | 32,517.8 ns | 1,782.41 ns |  9.35 |    0.40 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,200.7 ns |  1,490.0 ns |    81.67 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,181.2 ns | 11,977.6 ns |   656.53 ns |  1.00 |    0.14 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev    | Allocated |
|------------------------ |-------------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.216 μs |  22.121 μs | 1.2125 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   542.521 μs | 155.852 μs | 8.5428 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.236 μs |   5.705 μs | 0.3127 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,633.079 μs | 140.978 μs | 7.7275 μs |    1280 B |


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