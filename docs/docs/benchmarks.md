---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-04-05 19:47 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,162.38 μs** |    **91.753 μs** |    **60.689 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,366.53 μs |    99.267 μs |    65.659 μs |  0.22 |    0.01 |        - |       - |   35.15 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,382.89 μs** |    **62.533 μs** |    **41.362 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** | **1062.82 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,307.30 μs |    74.079 μs |    48.999 μs |  0.31 |    0.01 |  15.6250 |       - |   340.7 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,550.67 μs** |    **90.339 μs** |    **59.754 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,157.30 μs |    79.051 μs |    47.042 μs |  0.18 |    0.01 |        - |       - |   37.51 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,211.94 μs** |   **195.447 μs** |   **129.276 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,116.14 μs |   215.680 μs |   142.659 μs |  0.50 |    0.01 |  15.6250 |       - |   374.8 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     59.98 μs |     7.019 μs |     4.643 μs |     ? |       ? |   0.2441 |       - |    9.07 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,441.07 μs** |    **52.461 μs** |    **31.219 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |  **418.59 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    735.89 μs |   192.421 μs |   127.274 μs |  0.51 |    0.08 |   3.9063 |       - |  160.78 KB |        0.38 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,046.03 μs |   168.234 μs |   111.276 μs |     ? |       ? |   0.9766 |       - |   17.93 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,651.09 μs | 1,760.760 μs | 1,164.633 μs |     ? |       ? |   7.8125 |       - |  193.86 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,422.55 μs** |    **12.735 μs** |     **7.578 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,380.67 μs |    22.802 μs |    15.082 μs |  0.25 |    0.00 |        - |       - |    1.34 KB |        1.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,418.91 μs** |    **10.331 μs** |     **5.403 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,365.47 μs |    24.952 μs |    16.504 μs |  0.25 |    0.00 |        - |       - |    1.34 KB |        1.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.03 μs** |     **3.264 μs** |     **2.159 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,338.74 μs |    26.136 μs |    17.287 μs |  0.25 |    0.00 |        - |       - |    1.34 KB |        0.65 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,420.98 μs** |     **4.125 μs** |     **2.728 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,245.64 μs |    19.476 μs |    12.882 μs |  0.23 |    0.00 |        - |       - |    1.34 KB |        0.65 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-FMPLRC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-FMPLRC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-FMPLRC(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.170 s** | **0.0037 s** | **0.0010 s** |  **1.00** |   **76880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.017 s | 0.0058 s | 0.0015 s |  0.95 |  513976 B |        6.69 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.166 s** | **0.0031 s** | **0.0008 s** |  **1.00** |  **256880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0059 s | 0.0015 s |  0.95 | 1190272 B |        4.63 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.166 s** | **0.0034 s** | **0.0005 s** |  **1.00** |  **616592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.016 s | 0.0042 s | 0.0011 s |  0.95 | 1478040 B |        2.40 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.166 s** | **0.0018 s** | **0.0005 s** |  **1.00** | **2424608 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.016 s | 0.0034 s | 0.0009 s |  0.95 | 7027096 B |        2.90 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.157 s** | **0.0041 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.007 s | 0.0020 s | 0.0005 s |  0.95 |  253976 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.147 s** | **0.0871 s** | **0.0226 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.008 s | 0.0035 s | 0.0009 s |  0.96 | 1099840 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.158 s** | **0.0020 s** | **0.0005 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.008 s | 0.0007 s | 0.0001 s |  0.95 | 1303096 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.156 s** | **0.0027 s** | **0.0007 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.008 s | 0.0031 s | 0.0005 s |  0.95 | 3396424 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 23.967 μs |  8.7331 μs | 5.1969 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 13.903 μs |  2.8122 μs | 1.4708 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.811 μs |  0.0907 μs | 0.0474 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 35.746 μs | 11.4571 μs | 6.8180 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 18.977 μs |  8.0078 μs | 4.7653 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 25.282 μs |  9.1886 μs | 5.4680 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 18.262 μs |  0.7028 μs | 0.3676 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;           |  5.706 μs |  0.2337 μs | 0.1390 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.252 μs |  0.7079 μs | 0.4213 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,374.2 ns |   142.44 ns |  74.50 ns |  0.35 |    0.06 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,843.1 ns |   290.59 ns | 192.21 ns |  0.47 |    0.08 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,474.6 ns |   200.32 ns | 104.77 ns |  0.38 |    0.06 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,287.2 ns |   259.01 ns | 154.13 ns |  0.58 |    0.09 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    734.1 ns |    78.27 ns |  46.58 ns |  0.19 |    0.03 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 34,752.5 ns |   737.22 ns | 438.71 ns |  8.84 |    1.33 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,030.1 ns | 1,076.99 ns | 712.36 ns |  1.03 |    0.23 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  2,989.7 ns |   101.18 ns |  60.21 ns |  0.76 |    0.11 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Allocated |
|------------------------ |-------------:|------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.152 μs |   1.6941 μs |   1.0081 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   855.298 μs | 227.5904 μs | 150.5369 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     7.976 μs |   0.7233 μs |   0.4304 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,599.035 μs |  38.0498 μs |  25.1676 μs |         - |


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