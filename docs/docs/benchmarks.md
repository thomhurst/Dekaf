---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-03-14 01:34 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,043.13 μs** |    **86.371 μs** |    **57.129 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.98 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,155.82 μs |    77.866 μs |    51.503 μs |  0.19 |    0.01 |        - |       - |   47.27 KB |        0.44 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,321.56 μs** |    **58.855 μs** |    **35.023 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1063.39 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,312.66 μs |   109.887 μs |    57.473 μs |  0.32 |    0.01 |  31.2500 | 15.6250 |  582.91 KB |        0.55 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,450.22 μs** |    **71.590 μs** |    **47.352 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.47 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,746.12 μs |    19.961 μs |    10.440 μs |  0.27 |    0.00 |        - |       - |   61.78 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,244.63 μs** | **3,324.127 μs** | **1,978.135 μs** |  **1.02** |    **0.19** | **109.3750** | **46.8750** | **1938.73 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,039.94 μs |   383.205 μs |   253.466 μs |  0.54 |    0.07 |  78.1250 | 62.5000 | 1449.24 KB |        0.75 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **159.82 μs** |    **52.197 μs** |    **34.525 μs** |  **1.04** |    **0.28** |   **2.4414** |       **-** |   **42.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     69.37 μs |     5.009 μs |     3.313 μs |  0.45 |    0.08 |   0.7324 |  0.4883 |   13.82 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,405.46 μs** |    **80.740 μs** |    **48.047 μs** |  **1.00** |    **0.05** |  **25.3906** |       **-** |   **420.5 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    757.81 μs |    98.754 μs |    58.767 μs |  0.54 |    0.04 |   7.8125 |  3.9063 |  136.59 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    640.08 μs |    27.153 μs |    14.201 μs |     ? |       ? |   0.9766 |       - |   22.22 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,135.50 μs |   115.909 μs |    76.667 μs |     ? |       ? |   7.8125 |       - |  219.37 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,410.31 μs** |     **5.706 μs** |     **3.774 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.55 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,367.49 μs |    57.401 μs |    37.967 μs |  0.25 |    0.01 |        - |       - |    4.96 KB |        3.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,409.05 μs** |     **3.520 μs** |     **2.095 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.58 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,295.09 μs |    49.550 μs |    32.774 μs |  0.24 |    0.01 |        - |       - |    4.96 KB |        3.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,404.38 μs** |     **4.116 μs** |     **2.450 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.44 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,106.11 μs |     0.822 μs |     0.430 μs |  0.20 |    0.00 |        - |       - |    4.96 KB |        2.03 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,403.92 μs** |     **3.562 μs** |     **2.120 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.46 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,392.22 μs |    50.868 μs |    33.646 μs |  0.26 |    0.01 |        - |       - |    4.96 KB |        2.02 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ORTUTI(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ORTUTI(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.169 s** | **0.0009 s** | **0.0001 s** |  **1.00** |   **76880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.012 s | 0.0040 s | 0.0010 s |  0.95 |  130904 B |        1.70 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.165 s** | **0.0032 s** | **0.0008 s** |  **1.00** |  **256592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.011 s | 0.0022 s | 0.0006 s |  0.95 |  322816 B |        1.26 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.165 s** | **0.0014 s** | **0.0004 s** |  **1.00** |  **616592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.011 s | 0.0011 s | 0.0003 s |  0.95 |  471368 B |        0.76 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.165 s** | **0.0014 s** | **0.0004 s** |  **1.00** | **2424896 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.012 s | 0.0026 s | 0.0007 s |  0.95 | 2285536 B |        0.94 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.157 s** | **0.0027 s** | **0.0007 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.006 s | 0.0024 s | 0.0006 s |  0.95 |   31816 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.157 s** | **0.0023 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.006 s | 0.0023 s | 0.0006 s |  0.95 |   42776 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.156 s** | **0.0045 s** | **0.0012 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.005 s | 0.0011 s | 0.0002 s |  0.95 |   32544 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.157 s** | **0.0056 s** | **0.0015 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.007 s | 0.0032 s | 0.0008 s |  0.95 |   36080 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 32.303 μs | 12.1119 μs | 7.2076 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 10.429 μs |  0.3138 μs | 0.2075 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 10.981 μs |  0.1299 μs | 0.0773 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 35.894 μs | 11.6300 μs | 6.9208 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 22.172 μs | 13.6031 μs | 8.0950 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 25.478 μs |  9.8297 μs | 5.8495 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 20.420 μs |  0.4912 μs | 0.2923 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.583 μs |  0.4055 μs | 0.2413 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error    | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|---------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,466.4 ns | 133.0 ns |  79.16 ns |  0.36 |    0.02 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,659.2 ns | 173.3 ns | 103.15 ns |  0.41 |    0.03 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,756.1 ns | 285.8 ns | 170.06 ns |  0.43 |    0.04 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,939.7 ns | 189.1 ns | 112.51 ns |  0.72 |    0.03 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    767.9 ns | 116.2 ns |  76.86 ns |  0.19 |    0.02 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 31,952.1 ns | 547.9 ns | 286.57 ns |  7.83 |    0.26 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,086.9 ns | 210.5 ns | 139.21 ns |  1.00 |    0.05 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,537.2 ns | 130.3 ns |  77.55 ns |  0.87 |    0.03 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    12.632 μs |  3.2123 μs |  2.1247 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   536.931 μs | 22.9139 μs | 15.1561 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     8.065 μs |  0.4429 μs |  0.2930 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,625.070 μs | 52.5123 μs | 31.2492 μs |         - |


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