---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 10:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,116.43 μs** |   **600.39 μs** |  **32.909 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,429.36 μs | 2,208.37 μs | 121.048 μs |  0.23 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,354.69 μs** | **1,229.73 μs** |  **67.406 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,326.31 μs |   176.96 μs |   9.700 μs |  0.32 |    0.00 |  15.6250 |       - |  339.59 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,316.03 μs** |   **827.06 μs** |  **45.334 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,293.75 μs | 1,599.64 μs |  87.682 μs |  0.20 |    0.01 |        - |       - |   36.39 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,585.75 μs** | **3,563.01 μs** | **195.301 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,594.59 μs | 5,426.29 μs | 297.433 μs |  0.52 |    0.02 |  15.6250 |       - |  361.87 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.39 μs** |   **103.66 μs** |   **5.682 μs** |  **1.00** |    **0.05** |   **2.4414** |       **-** |   **42.34 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     71.63 μs |   100.58 μs |   5.513 μs |  0.50 |    0.04 |        - |       - |    9.14 KB |        0.22 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,382.44 μs** |   **469.19 μs** |  **25.718 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **427.95 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    631.57 μs |   615.25 μs |  33.724 μs |  0.46 |    0.02 |        - |       - |  139.45 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    196.73 μs |   327.00 μs |  17.924 μs |     ? |       ? |   0.9766 |       - |  103.97 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,077.19 μs | 2,675.57 μs | 146.657 μs |     ? |       ? |   7.8125 |       - | 1003.47 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,424.50 μs** |    **34.99 μs** |   **1.918 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,363.26 μs |   417.62 μs |  22.891 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,425.55 μs** |    **90.95 μs** |   **4.985 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,347.70 μs |   404.78 μs |  22.187 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.98 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,424.34 μs** |    **71.66 μs** |   **3.928 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,329.85 μs |   650.14 μs |  35.636 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,444.76 μs** |   **112.88 μs** |   **6.187 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,315.46 μs |   346.24 μs |  18.978 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.632 ms** |  **24.801 ms** | **1.3594 ms** | **3,169.114 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.555 ms |  39.974 ms | 2.1911 ms |    15.611 ms | 0.005 |  592.23 KB |        7.94 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.332 ms** |  **12.436 ms** | **0.6816 ms** | **3,166.098 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.580 ms |  35.178 ms | 1.9282 ms |    15.026 ms | 0.005 |  780.78 KB |        3.12 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.909 ms** |  **20.072 ms** | **1.1002 ms** | **3,166.201 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    21.098 ms | 165.489 ms | 9.0710 ms |    16.061 ms | 0.007 |  994.48 KB |        1.65 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.339 ms** |   **6.330 ms** | **0.3469 ms** | **3,164.212 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17.525 ms |  20.723 ms | 1.1359 ms |    17.058 ms | 0.006 | 2759.27 KB |        1.17 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,152.984 ms** |  **28.543 ms** | **1.5645 ms** | **3,152.737 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.292 ms |   5.221 ms | 0.2862 ms |     5.216 ms | 0.002 |  186.65 KB |       77.57 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.347 ms** |  **29.595 ms** | **1.6222 ms** | **3,155.791 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.242 ms |  26.363 ms | 1.4450 ms |     5.897 ms | 0.002 |  199.86 KB |       48.00 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.605 ms** |  **28.736 ms** | **1.5751 ms** | **3,156.507 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.499 ms |   7.866 ms | 0.4312 ms |     6.702 ms | 0.002 |  184.63 KB |       76.73 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.784 ms** |  **15.819 ms** | **0.8671 ms** | **3,155.695 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.156 ms |   8.342 ms | 0.4572 ms |     6.297 ms | 0.002 |  187.03 KB |       44.75 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 15.321 μs |  1.724 μs | 0.0945 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.594 μs | 22.252 μs | 1.2197 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.130 μs |  3.793 μs | 0.2079 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.471 μs |  4.893 μs | 0.2682 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.272 μs |  4.588 μs | 0.2515 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.515 μs |  2.177 μs | 0.1193 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.207 μs | 21.737 μs | 1.1915 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.505 μs | 59.508 μs | 3.2619 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.483 μs |  5.827 μs | 0.3194 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.442 μs | 13.037 μs | 0.7146 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,343.5 ns |  2,672.4 ns |   146.5 ns |  0.30 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,302.3 ns |  4,392.5 ns |   240.8 ns |  0.29 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,864.7 ns | 12,261.6 ns |   672.1 ns |  0.41 |    0.13 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,487.3 ns |  3,740.3 ns |   205.0 ns |  0.55 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    669.2 ns |  2,360.5 ns |   129.4 ns |  0.15 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 44,895.8 ns | 94,613.2 ns | 5,186.1 ns |  9.93 |    1.31 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,550.7 ns |  8,664.7 ns |   474.9 ns |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,176.7 ns | 12,260.3 ns |   672.0 ns |  0.92 |    0.15 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Median       | Allocated |
|------------------------ |-------------:|-----------:|-----------:|-------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    18.343 μs | 185.034 μs | 10.1424 μs |    12.745 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   530.477 μs | 245.476 μs | 13.4554 μs |   531.322 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.628 μs |   4.669 μs |  0.2559 μs |     9.645 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,551.838 μs | 360.256 μs | 19.7468 μs | 1,550.650 μs |    1280 B |


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