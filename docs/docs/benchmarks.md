---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-07 13:16 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,013.37 μs** |    **66.575 μs** |  **44.035 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,113.94 μs |    10.478 μs |   5.480 μs |  0.19 |    0.00 |        - |       - |   37.66 KB |        0.35 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,285.31 μs** |   **100.451 μs** |  **66.442 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,460.54 μs |   237.021 μs | 141.048 μs |  0.34 |    0.02 |  23.4375 |  7.8125 |  480.54 KB |        0.45 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,547.54 μs** |    **41.516 μs** |  **27.460 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,147.55 μs |    10.756 μs |   7.115 μs |  0.18 |    0.00 |   1.9531 |       - |   44.11 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,144.26 μs** |   **154.988 μs** | **102.515 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,830.24 μs | 1,233.409 μs | 645.097 μs |  0.73 |    0.05 |  62.5000 | 46.8750 | 1212.69 KB |        0.63 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.65 μs** |     **3.156 μs** |   **1.878 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     85.73 μs |    29.880 μs |  19.763 μs |  0.64 |    0.14 |   0.2441 |       - |     4.9 KB |        0.12 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,359.61 μs** |    **21.876 μs** |  **14.469 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **419.72 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    723.79 μs |    80.290 μs |  53.107 μs |  0.53 |    0.04 |   7.8125 |  3.9063 |  170.61 KB |        0.41 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    810.86 μs |    63.809 μs |  42.206 μs |     ? |       ? |   0.4883 |       - |   13.13 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,843.46 μs |   216.923 μs | 113.455 μs |     ? |       ? |   7.8125 |       - |   132.1 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,400.11 μs** |     **6.185 μs** |   **4.091 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,361.31 μs |    27.020 μs |  17.872 μs |  0.25 |    0.00 |        - |       - |    1.56 KB |        1.31 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,400.16 μs** |     **4.026 μs** |   **2.663 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,362.52 μs |    20.045 μs |  13.259 μs |  0.25 |    0.00 |        - |       - |    1.59 KB |        1.34 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,404.35 μs** |     **5.175 μs** |   **3.079 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,100.25 μs |     0.804 μs |   0.532 μs |  0.20 |    0.00 |        - |       - |    1.55 KB |        0.75 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,409.57 μs** |     **6.146 μs** |   **3.657 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,099.87 μs |     0.875 μs |   0.521 μs |  0.20 |    0.00 |        - |       - |    1.55 KB |        0.75 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-HQYQUV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-HQYQUV(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.178 s** | **0.0045 s** | **0.0012 s** |  **1.00** |    **75.2 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0039 s | 0.0006 s |  0.95 |  112.45 KB |        1.50 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.175 s** | **0.0044 s** | **0.0011 s** |  **1.00** |  **250.98 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0049 s | 0.0013 s |  0.95 |  301.34 KB |        1.20 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.175 s** | **0.0024 s** | **0.0006 s** |  **1.00** |  **602.55 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0037 s | 0.0006 s |  0.95 |  362.03 KB |        0.60 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.176 s** | **0.0029 s** | **0.0008 s** |  **1.00** | **2368.19 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.016 s | 0.0014 s | 0.0004 s |  0.95 | 2132.58 KB |        0.90 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.179 s** | **0.0028 s** | **0.0007 s** |  **1.00** |   **16.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.014 s | 0.0036 s | 0.0009 s |  0.95 |  106.74 KB |        6.60 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.179 s** | **0.0031 s** | **0.0008 s** |  **1.00** |   **17.66 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.014 s | 0.0010 s | 0.0002 s |  0.95 |   170.3 KB |        9.65 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.180 s** | **0.0034 s** | **0.0009 s** |  **1.00** |   **16.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.014 s | 0.0028 s | 0.0007 s |  0.95 |  104.68 KB |        6.47 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.180 s** | **0.0032 s** | **0.0005 s** |  **1.00** |   **17.68 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.014 s | 0.0035 s | 0.0009 s |  0.95 |  108.11 KB |        6.11 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.498 μs |  8.9788 μs | 5.3431 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 10.294 μs |  0.1634 μs | 0.1081 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 10.850 μs |  0.1284 μs | 0.0849 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 41.083 μs |  8.6618 μs | 5.1545 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 22.622 μs | 11.6872 μs | 6.9549 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 26.929 μs | 10.2322 μs | 6.0890 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.243 μs |  0.4283 μs | 0.2549 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  5.215 μs |  0.5913 μs | 0.3911 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,303.8 ns |  68.89 ns |  36.03 ns |  0.34 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,677.2 ns |  83.55 ns |  55.26 ns |  0.44 |    0.01 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,657.7 ns | 300.27 ns | 198.61 ns |  0.43 |    0.05 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,939.2 ns | 996.10 ns | 592.76 ns |  1.02 |    0.15 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    737.8 ns | 190.15 ns | 113.15 ns |  0.19 |    0.03 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 34,344.3 ns | 641.43 ns | 381.71 ns |  8.93 |    0.13 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,844.8 ns |  74.34 ns |  38.88 ns |  1.00 |    0.01 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,762.2 ns | 788.08 ns | 468.97 ns |  0.98 |    0.12 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.833 μs |  2.7629 μs |  1.4451 μs |   77384 B |
| &#39;Snappy Compress 1MB&#39;   |   519.649 μs | 27.2354 μs | 14.2447 μs |   78392 B |
| &#39;Snappy Decompress 1KB&#39; |     7.818 μs |  0.2360 μs |  0.1404 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,970.176 μs | 49.5175 μs | 32.7528 μs |         - |


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