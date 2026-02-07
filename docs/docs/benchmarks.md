---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-07 15:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,953.88 μs** |    **71.256 μs** |  **47.131 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,105.34 μs |     3.423 μs |   2.037 μs |  0.19 |    0.00 |   1.9531 |       - |   37.64 KB |        0.35 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,236.56 μs** |    **50.916 μs** |  **30.299 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,373.78 μs |   119.117 μs |  62.301 μs |  0.33 |    0.01 |  23.4375 |  7.8125 |  478.15 KB |        0.45 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,672.73 μs** |    **34.045 μs** |  **22.519 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,123.48 μs |     4.524 μs |   2.366 μs |  0.17 |    0.00 |   1.9531 |       - |   43.99 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,369.41 μs** |   **212.399 μs** | **140.489 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,622.34 μs | 1,157.486 μs | 605.387 μs |  0.76 |    0.05 |  62.5000 | 46.8750 | 1201.71 KB |        0.62 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.26 μs** |     **2.545 μs** |   **1.683 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     85.98 μs |    35.506 μs |  23.485 μs |  0.66 |    0.17 |   0.2441 |       - |    4.89 KB |        0.12 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,326.48 μs** |    **17.282 μs** |  **11.431 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **420.99 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    691.10 μs |    86.758 μs |  57.385 μs |  0.52 |    0.04 |        - |       - |   50.09 KB |        0.12 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    772.66 μs |    33.075 μs |  19.682 μs |     ? |       ? |   0.4883 |       - |    13.2 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,735.73 μs |   258.425 μs | 153.784 μs |     ? |       ? |   7.8125 |       - |  131.53 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,378.34 μs** |     **3.692 μs** |   **2.197 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,109.45 μs |    19.636 μs |  12.988 μs |  0.21 |    0.00 |        - |       - |    1.55 KB |        1.30 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,376.63 μs** |     **4.926 μs** |   **2.931 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,257.71 μs |    35.075 μs |  23.200 μs |  0.23 |    0.00 |        - |       - |    1.55 KB |        1.31 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,383.55 μs** |     **4.673 μs** |   **3.091 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,311.09 μs |    28.719 μs |  18.996 μs |  0.24 |    0.00 |        - |       - |    1.55 KB |        0.75 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,381.59 μs** |     **2.597 μs** |   **1.546 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,323.21 μs |    22.042 μs |  14.579 μs |  0.25 |    0.00 |        - |       - |    1.55 KB |        0.75 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-ZXBEUU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-ZXBEUU(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.178 s** | **0.0038 s** | **0.0010 s** |  **1.00** |    **75.2 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0038 s | 0.0010 s |  0.95 |  125.88 KB |        1.67 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.174 s** | **0.0027 s** | **0.0007 s** |  **1.00** |  **250.98 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0019 s | 0.0003 s |  0.95 |  295.27 KB |        1.18 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.174 s** | **0.0017 s** | **0.0003 s** |  **1.00** |  **602.55 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0031 s | 0.0008 s |  0.95 |  425.91 KB |        0.71 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.173 s** | **0.0060 s** | **0.0009 s** |  **1.00** | **2367.91 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.015 s | 0.0012 s | 0.0003 s |  0.95 | 2127.55 KB |        0.90 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.177 s** | **0.0038 s** | **0.0010 s** |  **1.00** |    **15.9 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.013 s | 0.0006 s | 0.0001 s |  0.95 |  104.09 KB |        6.55 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.178 s** | **0.0025 s** | **0.0006 s** |  **1.00** |   **17.94 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0029 s | 0.0008 s |  0.95 |  106.55 KB |        5.94 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.179 s** | **0.0043 s** | **0.0011 s** |  **1.00** |   **16.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.014 s | 0.0020 s | 0.0005 s |  0.95 |  110.22 KB |        6.81 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.179 s** | **0.0035 s** | **0.0009 s** |  **1.00** |   **17.96 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.014 s | 0.0030 s | 0.0008 s |  0.95 |  113.32 KB |        6.31 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev     | Allocated |
|--------------------------------- |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.327 μs |  9.0893 μs |  5.4089 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 10.127 μs |  0.1190 μs |  0.0787 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 13.611 μs |  0.1127 μs |  0.0746 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 36.742 μs | 19.6548 μs | 11.6963 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 22.322 μs | 10.9351 μs |  6.5073 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 30.899 μs | 12.0305 μs |  7.1592 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.403 μs |  0.3386 μs |  0.2239 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.382 μs |  0.0903 μs |  0.0597 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,299.7 ns |    38.70 ns |  23.03 ns |  0.34 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,859.0 ns |    74.40 ns |  38.91 ns |  0.49 |    0.01 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,518.0 ns |   156.27 ns |  81.73 ns |  0.40 |    0.02 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,407.1 ns |   223.46 ns | 147.81 ns |  0.89 |    0.04 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    619.7 ns |    36.92 ns |  24.42 ns |  0.16 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,643.6 ns | 1,424.85 ns | 847.90 ns |  9.35 |    0.27 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,811.6 ns |   139.78 ns |  73.11 ns |  1.00 |    0.03 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,406.1 ns |   126.02 ns |  75.00 ns |  0.89 |    0.02 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Allocated |
|------------------------ |-------------:|------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.322 μs |   0.7589 μs |   0.3969 μs |   77672 B |
| &#39;Snappy Compress 1MB&#39;   |   731.858 μs | 306.4234 μs | 202.6801 μs |   78104 B |
| &#39;Snappy Decompress 1KB&#39; |     9.158 μs |   1.3430 μs |   0.8883 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,931.858 μs |  31.5077 μs |  20.8404 μs |         - |


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