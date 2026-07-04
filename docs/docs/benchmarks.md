---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 12:25 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,198.70 μs** |   **532.62 μs** |  **29.195 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,275.44 μs | 1,909.99 μs | 104.693 μs |  0.21 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,224.32 μs** | **1,110.91 μs** |  **60.893 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,365.04 μs | 2,547.19 μs | 139.620 μs |  0.33 |    0.02 |  15.6250 |       - |   339.5 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,654.16 μs** |   **342.14 μs** |  **18.754 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,184.05 μs |   726.84 μs |  39.840 μs |  0.18 |    0.01 |        - |       - |   36.29 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,506.47 μs** | **4,467.09 μs** | **244.856 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  5,627.06 μs |   718.44 μs |  39.380 μs |  0.49 |    0.01 |  15.6250 |       - |  361.62 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **150.40 μs** |   **530.04 μs** |  **29.053 μs** |  **1.02** |    **0.24** |   **2.4414** |       **-** |   **41.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     55.37 μs |   105.62 μs |   5.789 μs |  0.38 |    0.07 |   0.2441 |       - |    8.64 KB |        0.21 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    572.08 μs |   168.77 μs |   9.251 μs |     ? |       ? |        - |       - |   70.49 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    219.91 μs |   598.26 μs |  32.792 μs |     ? |       ? |   0.4883 |       - |   11.94 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  1,972.81 μs |   650.21 μs |  35.640 μs |     ? |       ? |   7.8125 |       - | 1037.42 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,397.51 μs** |   **184.03 μs** |  **10.087 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,310.08 μs |   387.62 μs |  21.247 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,396.74 μs** |   **115.58 μs** |   **6.335 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,318.89 μs |   328.90 μs |  18.028 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.68 μs** |    **47.04 μs** |   **2.578 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,339.65 μs |   620.60 μs |  34.017 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,395.68 μs** |    **67.81 μs** |   **3.717 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,327.69 μs |   367.83 μs |  20.162 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170.268 ms** |  **18.344 ms** |  **1.0055 ms** | **1.000** |  **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17.945 ms |  44.298 ms |  2.4281 ms | 0.006 | 601.98 KB |        8.07 |
|                      |            |              |             |              |            |            |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.497 ms** |  **25.949 ms** |  **1.4224 ms** | **1.000** |  **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.729 ms |  43.981 ms |  2.4107 ms | 0.005 | 775.63 KB |        3.10 |
|                      |            |              |             |              |            |            |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.900 ms** |  **14.753 ms** |  **0.8086 ms** | **1.000** | **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    16.151 ms |  42.552 ms |  2.3324 ms | 0.005 | 996.44 KB |        1.66 |
|                      |            |              |             |              |            |            |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167.295 ms** |  **31.257 ms** |  **1.7133 ms** | **1.000** | **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.854 ms |  29.867 ms |  1.6371 ms | 0.006 | 2762.2 KB |        1.17 |
|                      |            |              |             |              |            |            |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,157.186 ms** |  **14.279 ms** |  **0.7827 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.839 ms |  37.872 ms |  2.0759 ms | 0.002 | 184.38 KB |       76.62 |
|                      |            |              |             |              |            |            |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.964 ms** |  **13.630 ms** |  **0.7471 ms** | **1.000** |   **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.124 ms |   8.245 ms |  0.4519 ms | 0.002 |  195.5 KB |       46.95 |
|                      |            |              |             |              |            |            |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,141.470 ms** | **521.573 ms** | **28.5892 ms** | **1.000** |   **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.792 ms |   4.440 ms |  0.2433 ms | 0.002 | 231.12 KB |       96.05 |
|                      |            |              |             |              |            |            |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.399 ms** |  **16.716 ms** |  **0.9163 ms** | **1.000** |   **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.476 ms |   7.570 ms |  0.4149 ms | 0.002 | 258.96 KB |       61.96 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 11.931 μs |  1.245 μs | 0.0682 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  7.558 μs |  4.340 μs | 0.2379 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.630 μs |  3.945 μs | 0.2162 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.353 μs |  3.198 μs | 0.1753 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.223 μs | 44.041 μs | 2.4140 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 16.875 μs |  9.479 μs | 0.5195 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 13.877 μs | 17.052 μs | 0.9347 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 17.145 μs | 14.437 μs | 0.7913 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.649 μs | 10.722 μs | 0.5877 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.587 μs |  4.369 μs | 0.2395 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,062.0 ns |   2,650.1 ns |   145.26 ns |  1,052.0 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |    978.0 ns |   1,290.9 ns |    70.76 ns |    971.0 ns |  0.31 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,082.0 ns |   1,139.3 ns |    62.45 ns |  1,102.0 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  6,409.7 ns | 138,133.5 ns | 7,571.57 ns |  2,124.0 ns |  2.02 |    2.08 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    754.7 ns |   2,532.6 ns |   138.82 ns |    792.0 ns |  0.24 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 29,079.3 ns |   8,081.0 ns |   442.95 ns | 29,133.0 ns |  9.17 |    0.59 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,181.7 ns |   4,201.3 ns |   230.29 ns |  3,195.0 ns |  1.00 |    0.09 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,204.7 ns |   7,765.7 ns |   425.66 ns |  3,425.0 ns |  1.01 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.686 μs |   2.220 μs |  0.1217 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   437.662 μs | 561.570 μs | 30.7815 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     7.371 μs |   3.349 μs |  0.1836 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,159.203 μs | 124.432 μs |  6.8205 μs |    1280 B |


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