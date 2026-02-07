---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-07 18:21 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,933.12 μs** |    **58.261 μs** |    **34.670 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,108.75 μs |     8.387 μs |     4.991 μs |  0.19 |    0.00 |   1.9531 |       - |   37.64 KB |        0.35 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,269.15 μs** |   **103.781 μs** |    **68.645 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,405.32 μs |   183.647 μs |   109.285 μs |  0.33 |    0.01 |  23.4375 |  7.8125 |   476.9 KB |        0.45 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,675.85 μs** |    **19.360 μs** |    **10.126 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.24 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,133.67 μs |    14.114 μs |     8.399 μs |  0.17 |    0.00 |   1.9531 |       - |      44 KB |        0.23 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,555.94 μs** |   **119.955 μs** |    **62.739 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,980.62 μs | 3,243.085 μs | 2,145.100 μs |  0.86 |    0.18 |  62.5000 | 46.8750 | 1208.89 KB |        0.62 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **130.42 μs** |     **2.699 μs** |     **1.785 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     87.86 μs |    28.528 μs |    18.870 μs |  0.67 |    0.14 |   0.2441 |       - |    4.88 KB |        0.12 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,323.58 μs** |    **25.525 μs** |    **16.883 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **421.77 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    702.58 μs |   108.297 μs |    64.446 μs |  0.53 |    0.05 |   7.8125 |  3.9063 |  166.13 KB |        0.39 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    771.59 μs |    21.555 μs |    11.274 μs |     ? |       ? |   0.4883 |       - |   13.17 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,630.30 μs |   179.046 μs |    93.645 μs |     ? |       ? |   7.8125 |       - |  130.81 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,383.24 μs** |    **21.556 μs** |    **11.274 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,343.33 μs |    30.825 μs |    20.389 μs |  0.25 |    0.00 |        - |       - |    1.55 KB |        1.30 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,371.57 μs** |     **4.337 μs** |     **2.868 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,333.73 μs |    14.945 μs |     8.893 μs |  0.25 |    0.00 |        - |       - |    1.55 KB |        1.31 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,373.94 μs** |     **3.418 μs** |     **2.261 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,305.94 μs |    24.253 μs |    16.042 μs |  0.24 |    0.00 |        - |       - |    1.55 KB |        0.75 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,373.92 μs** |     **6.754 μs** |     **4.467 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,250.13 μs |    30.502 μs |    20.175 μs |  0.23 |    0.00 |        - |       - |    1.55 KB |        0.75 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-AFFIZA(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-AFFIZA(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.177 s** | **0.0046 s** | **0.0012 s** |  **1.00** |   **77008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0040 s | 0.0010 s |  0.95 |  115112 B |        1.49 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.174 s** | **0.0015 s** | **0.0002 s** |  **1.00** |  **257008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0052 s | 0.0013 s |  0.95 |  373608 B |        1.45 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.174 s** | **0.0013 s** | **0.0002 s** |  **1.00** |  **617008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0021 s | 0.0003 s |  0.95 |  370088 B |        0.60 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.175 s** | **0.0042 s** | **0.0011 s** |  **1.00** | **2425024 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.016 s | 0.0030 s | 0.0008 s |  0.95 | 2232040 B |        0.92 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.164 s** | **0.0030 s** | **0.0008 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.013 s | 0.0036 s | 0.0006 s |  0.95 |   83368 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.164 s** | **0.0020 s** | **0.0003 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0025 s | 0.0006 s |  0.95 |   86296 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.165 s** | **0.0017 s** | **0.0004 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.012 s | 0.0016 s | 0.0004 s |  0.95 |   84368 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.165 s** | **0.0022 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.013 s | 0.0031 s | 0.0008 s |  0.95 |   87832 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.629 μs |  9.3569 μs | 5.5681 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 12.933 μs |  0.2719 μs | 0.1422 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 10.987 μs |  0.1458 μs | 0.0965 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 40.642 μs | 13.8138 μs | 7.2249 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 23.170 μs | 11.2080 μs | 6.6697 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 25.124 μs |  9.5080 μs | 5.6581 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.587 μs |  0.3428 μs | 0.2040 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  5.681 μs |  0.2996 μs | 0.1783 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,370.3 ns |    37.37 ns |    22.24 ns |  0.36 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,507.3 ns |    70.84 ns |    42.16 ns |  0.40 |    0.01 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  2,069.5 ns |   243.08 ns |   144.65 ns |  0.55 |    0.04 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,923.0 ns |    81.93 ns |    54.19 ns |  0.77 |    0.02 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    703.0 ns |    64.84 ns |    42.88 ns |  0.19 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 58,158.8 ns | 2,276.71 ns | 1,505.90 ns | 15.35 |    0.46 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,791.0 ns |   112.44 ns |    66.91 ns |  1.00 |    0.02 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,418.1 ns |   157.74 ns |    93.87 ns |  0.90 |    0.03 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.969 μs |  0.2109 μs |  0.1255 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   502.032 μs | 28.9404 μs | 19.1423 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     9.202 μs |  0.3509 μs |  0.2088 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,636.819 μs | 34.6379 μs | 22.9108 μs |         - |


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