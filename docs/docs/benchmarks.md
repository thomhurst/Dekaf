---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-03 17:27 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,135.58 μs** |   **145.57 μs** |   **7.979 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,355.72 μs |   914.51 μs |  50.128 μs |  0.22 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,368.99 μs** |   **898.99 μs** |  **49.277 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,371.58 μs |   917.31 μs |  50.281 μs |  0.32 |    0.01 |  15.6250 |       - |  339.67 KB |        0.32 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,360.40 μs** |   **444.46 μs** |  **24.362 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,360.67 μs | 2,655.65 μs | 145.565 μs |  0.21 |    0.02 |        - |       - |    36.3 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,238.14 μs** | **2,088.09 μs** | **114.455 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,641.42 μs | 7,547.73 μs | 413.717 μs |  0.54 |    0.03 |  15.6250 |       - |  361.79 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.06 μs** |    **33.54 μs** |   **1.838 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.41 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     70.87 μs |   381.54 μs |  20.913 μs |  0.53 |    0.14 |        - |       - |    7.86 KB |        0.19 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,408.31 μs** | **1,057.59 μs** |  **57.970 μs** |  **1.00** |    **0.05** |  **23.4375** |       **-** |  **424.02 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    725.52 μs | 1,011.08 μs |  55.421 μs |  0.52 |    0.04 |        - |       - |   99.89 KB |        0.24 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    204.78 μs |   168.35 μs |   9.228 μs |     ? |       ? |   0.9766 |       - |  100.94 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |          **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,104.47 μs | 1,546.30 μs |  84.758 μs |     ? |       ? |   7.8125 |       - | 1014.15 KB |           ? |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,420.04 μs** |    **37.93 μs** |   **2.079 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,331.60 μs |    86.21 μs |   4.725 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,986.89 μs** | **8,895.96 μs** | **487.618 μs** |  **1.00** |    **0.10** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,123.87 μs |    83.94 μs |   4.601 μs |  0.19 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,421.55 μs** |    **73.88 μs** |   **4.050 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,117.56 μs |    69.14 μs |   3.790 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |             |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,422.58 μs** |   **120.37 μs** |   **6.598 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,399.61 μs |   578.64 μs |  31.717 μs |  0.26 |    0.01 |        - |       - |    1.14 KB |        0.56 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ATKAMJ(IterationCount=3, LaunchCount=1, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev     | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|-----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,150.269 ms** | **524.088 ms** | **28.7270 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.369 ms |  37.513 ms |  2.0562 ms | 0.005 |  409.22 KB |        5.48 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.647 ms** |   **2.543 ms** |  **0.1394 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    12.262 ms |  11.983 ms |  0.6568 ms | 0.004 |  601.66 KB |        2.40 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164.358 ms** |  **27.702 ms** |  **1.5184 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17.120 ms | 104.476 ms |  5.7267 ms | 0.005 |  884.52 KB |        1.47 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.362 ms** |   **1.881 ms** |  **0.1031 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    13.703 ms |  25.232 ms |  1.3831 ms | 0.004 | 2618.39 KB |        1.11 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.370 ms** |  **32.755 ms** |  **1.7954 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.822 ms |   9.745 ms |  0.5342 ms | 0.002 |  181.29 KB |       75.34 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.636 ms** |  **24.696 ms** |  **1.3537 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.154 ms |  28.999 ms |  1.5895 ms | 0.002 |   183.4 KB |       44.04 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,155.047 ms** |  **41.557 ms** |  **2.2779 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.642 ms |  15.299 ms |  0.8386 ms | 0.002 |  181.51 KB |       75.43 |
|                      |            |              |             |              |            |            |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,157.968 ms** |  **25.682 ms** |  **1.4077 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.475 ms |   4.052 ms |  0.2221 ms | 0.002 |  225.61 KB |       53.98 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 31.406 μs | 181.265 μs | 9.9357 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.059 μs |  43.352 μs | 2.3763 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 14.589 μs |  11.969 μs | 0.6561 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.608 μs |   3.774 μs | 0.2068 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 15.635 μs |  69.614 μs | 3.8158 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 15.452 μs |  13.675 μs | 0.7496 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.373 μs |  41.810 μs | 2.2917 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 19.690 μs |  31.285 μs | 1.7149 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.840 μs |  11.286 μs | 0.6186 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.979 μs |  11.101 μs | 0.6085 μs |         - |


## Serializer Benchmarks

| Method                               | Mean      | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1.424 μs |  1.9251 μs | 0.1055 μs |  0.36 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1.467 μs |  5.5080 μs | 0.3019 μs |  0.37 |    0.09 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1.411 μs |  6.8473 μs | 0.3753 μs |  0.35 |    0.10 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2.447 μs |  7.1839 μs | 0.3938 μs |  0.61 |    0.13 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1.013 μs |  0.8096 μs | 0.0444 μs |  0.25 |    0.04 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 48.558 μs | 38.4472 μs | 2.1074 μs | 12.10 |    2.04 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4.122 μs | 16.0019 μs | 0.8771 μs |  1.03 |    0.26 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4.014 μs | 16.4947 μs | 0.9041 μs |  1.00 |    0.26 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Allocated |
|------------------------ |------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.06 μs |  52.23 μs |  2.863 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   504.91 μs | 578.67 μs | 31.719 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    11.78 μs |  22.46 μs |  1.231 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,409.10 μs | 455.31 μs | 24.957 μs |    1280 B |


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