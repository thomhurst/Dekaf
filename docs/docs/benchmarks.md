---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-04 21:18 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,340.33 μs** |  **9,698.30 μs** |   **531.596 μs** | **6,064.42 μs** |  **1.00** |    **0.10** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,213.86 μs |  1,673.48 μs |    91.729 μs | 1,176.13 μs |  0.19 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,577.23 μs** | **12,555.02 μs** |   **688.183 μs** | **7,211.99 μs** |  **1.01** |    **0.11** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,164.61 μs |     56.59 μs |     3.102 μs | 2,163.98 μs |  0.29 |    0.02 |  19.5313 |  3.9063 |  339.47 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,565.29 μs** |    **686.92 μs** |    **37.652 μs** | **6,567.70 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,093.62 μs |     24.50 μs |     1.343 μs | 1,093.54 μs |  0.17 |    0.00 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,068.30 μs** |  **4,981.24 μs** |   **273.039 μs** | **8,950.90 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 4,806.56 μs |  7,642.03 μs |   418.885 μs | 4,723.91 μs |  0.53 |    0.04 |  15.6250 |       - |  361.81 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    38.80 μs |     37.76 μs |     2.070 μs |    37.99 μs |     ? |       ? |   0.2441 |       - |   17.52 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,025.52 μs** |  **3,059.12 μs** |   **167.680 μs** |   **962.53 μs** |  **1.02** |    **0.20** |  **25.3906** |       **-** |  **424.71 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   498.64 μs |     72.28 μs |     3.962 μs |   496.93 μs |  0.49 |    0.07 |   1.9531 |       - |  102.77 KB |        0.24 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   299.21 μs |  4,885.85 μs |   267.810 μs |   146.54 μs |     ? |       ? |   0.4883 |       - |    11.9 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 4,289.45 μs | 45,603.54 μs | 2,499.684 μs | 5,340.36 μs |     ? |       ? |   3.9063 |       - |  105.25 KB |           ? |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,394.42 μs** |  **1,709.38 μs** |    **93.697 μs** | **5,343.94 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,370.94 μs |  1,745.33 μs |    95.667 μs | 1,338.84 μs |  0.25 |    0.02 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,690.29 μs** | **11,045.83 μs** |   **605.459 μs** | **5,347.14 μs** |  **1.01** |    **0.13** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,266.48 μs |    103.94 μs |     5.697 μs | 1,266.54 μs |  0.22 |    0.02 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,414.04 μs** |    **765.78 μs** |    **41.975 μs** | **5,405.04 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,114.19 μs |    117.34 μs |     6.432 μs | 1,117.61 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |             |              |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,382.55 μs** |     **92.61 μs** |     **5.076 μs** | **5,383.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,319.73 μs |    195.45 μs |    10.714 μs | 1,319.33 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,166.566 ms** | **25.070 ms** | **1.3741 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.431 ms | 22.065 ms | 1.2095 ms | 0.005 |  596.46 KB |        7.99 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,164.320 ms** |  **9.443 ms** | **0.5176 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    11.799 ms | 25.637 ms | 1.4053 ms | 0.004 |  779.41 KB |        3.11 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.978 ms** |  **6.166 ms** | **0.3380 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    13.927 ms | 32.283 ms | 1.7695 ms | 0.004 | 1030.87 KB |        1.71 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,163.534 ms** |  **9.308 ms** | **0.5102 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.339 ms | 30.897 ms | 1.6936 ms | 0.004 | 2759.48 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.135 ms** | **33.891 ms** | **1.8577 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     4.820 ms |  8.241 ms | 0.4517 ms | 0.002 |  197.95 KB |       82.26 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.390 ms** | **17.020 ms** | **0.9329 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     4.987 ms |  1.900 ms | 0.1042 ms | 0.002 |  186.32 KB |       44.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.552 ms** | **22.837 ms** | **1.2518 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.189 ms |  9.528 ms | 0.5223 ms | 0.002 |  184.66 KB |       76.74 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.593 ms** | **20.574 ms** | **1.1277 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.790 ms | 21.617 ms | 1.1849 ms | 0.002 |  189.27 KB |       45.28 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 34.156 μs | 269.6304 μs | 14.7794 μs | 25.663 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.220 μs |   4.3827 μs |  0.2402 μs | 10.354 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.453 μs |   4.5840 μs |  0.2513 μs | 10.440 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.947 μs |   0.9362 μs |  0.0513 μs | 26.960 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.926 μs |   1.5905 μs |  0.0872 μs |  8.886 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.719 μs |  72.7026 μs |  3.9851 μs | 20.459 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.829 μs |   4.5904 μs |  0.2516 μs | 17.893 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.219 μs |   6.4174 μs |  0.3518 μs | 20.192 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.505 μs |   2.1380 μs |  0.1172 μs |  4.458 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 17.796 μs | 222.8383 μs | 12.2145 μs | 10.920 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,503.0 ns |  5,414.5 ns | 296.78 ns |  0.37 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,376.3 ns |    899.9 ns |  49.33 ns |  0.33 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,453.0 ns |  5,085.4 ns | 278.75 ns |  0.35 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,624.8 ns |    805.7 ns |  44.16 ns |  0.64 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    737.7 ns |  1,004.8 ns |  55.08 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,762.0 ns | 11,388.4 ns | 624.23 ns | 10.15 |    0.17 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,114.3 ns |    919.5 ns |  50.40 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,933.8 ns |  1,654.1 ns |  90.67 ns |  0.96 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.108 μs |   4.196 μs |  0.2300 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   928.311 μs | 392.635 μs | 21.5216 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.250 μs |  14.314 μs |  0.7846 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,703.992 μs | 250.483 μs | 13.7298 μs |    1280 B |


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