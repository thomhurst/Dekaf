---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-07 16:39 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,928.27 μs** |    **73.734 μs** |  **48.770 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,224.71 μs |    20.789 μs |  13.751 μs |  0.21 |    0.00 |   1.9531 |       - |   37.64 KB |        0.35 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,267.58 μs** |    **97.283 μs** |  **64.347 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,441.16 μs |   177.030 μs | 117.094 μs |  0.34 |    0.02 |  23.4375 |       - |     474 KB |        0.45 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,680.11 μs** |    **12.391 μs** |   **8.196 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,133.40 μs |    15.321 μs |  10.134 μs |  0.17 |    0.00 |   1.9531 |       - |   43.99 KB |        0.23 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,426.95 μs** |   **282.099 μs** | **167.873 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,514.60 μs | 1,072.570 μs | 560.975 μs |  0.75 |    0.05 |  62.5000 | 46.8750 |  1204.7 KB |        0.62 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.62 μs** |     **3.124 μs** |   **2.066 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     84.04 μs |    30.351 μs |  20.076 μs |  0.64 |    0.15 |   0.2441 |       - |    4.92 KB |        0.12 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,307.78 μs** |    **36.650 μs** |  **24.242 μs** |  **1.00** |    **0.02** |  **25.3906** |       **-** |  **421.93 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    678.39 μs |   124.016 μs |  82.029 μs |  0.52 |    0.06 |   7.8125 |  3.9063 |  140.91 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    766.25 μs |    33.158 μs |  19.732 μs |     ? |       ? |   0.4883 |       - |   13.19 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |         **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,586.34 μs |   273.669 μs | 143.134 μs |     ? |       ? |   7.8125 |       - |  131.28 KB |           ? |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,374.83 μs** |     **3.958 μs** |   **2.618 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,327.10 μs |    19.131 μs |  12.654 μs |  0.25 |    0.00 |        - |       - |    1.55 KB |        1.31 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,371.77 μs** |     **2.267 μs** |   **1.500 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,314.95 μs |    21.524 μs |  14.237 μs |  0.24 |    0.00 |        - |       - |    1.56 KB |        1.31 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,375.68 μs** |     **3.384 μs** |   **2.238 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,304.73 μs |    35.212 μs |  23.290 μs |  0.24 |    0.00 |        - |       - |    1.55 KB |        0.75 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,372.08 μs** |     **3.940 μs** |   **2.606 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,284.37 μs |    24.688 μs |  16.329 μs |  0.24 |    0.00 |        - |       - |    1.55 KB |        0.75 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-MAKAHD(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-MAKAHD(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.176 s** | **0.0044 s** | **0.0007 s** |  **1.00** |    **75.2 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0022 s | 0.0006 s |  0.95 |  114.72 KB |        1.53 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.174 s** | **0.0025 s** | **0.0006 s** |  **1.00** |  **250.98 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0030 s | 0.0008 s |  0.95 |  301.28 KB |        1.20 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.175 s** | **0.0031 s** | **0.0005 s** |  **1.00** |  **602.55 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0017 s | 0.0004 s |  0.95 |   361.7 KB |        0.60 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.175 s** | **0.0030 s** | **0.0008 s** |  **1.00** | **2368.19 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.015 s | 0.0059 s | 0.0009 s |  0.95 | 2128.86 KB |        0.90 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.178 s** | **0.0011 s** | **0.0002 s** |  **1.00** |   **16.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.014 s | 0.0053 s | 0.0014 s |  0.95 |  103.45 KB |        6.39 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.178 s** | **0.0037 s** | **0.0010 s** |  **1.00** |   **17.94 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0043 s | 0.0011 s |  0.95 |  111.95 KB |        6.24 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.178 s** | **0.0040 s** | **0.0006 s** |  **1.00** |   **16.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.013 s | 0.0016 s | 0.0002 s |  0.95 |  103.91 KB |        6.42 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.178 s** | **0.0033 s** | **0.0009 s** |  **1.00** |   **17.96 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.013 s | 0.0016 s | 0.0004 s |  0.95 |  107.84 KB |        6.00 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev     | Allocated |
|--------------------------------- |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 21.854 μs |  7.5118 μs |  4.4701 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 10.191 μs |  1.0981 μs |  0.5743 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 15.277 μs |  0.5079 μs |  0.3359 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 28.865 μs |  4.9711 μs |  2.9582 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 23.484 μs |  6.1864 μs |  3.6814 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 26.056 μs | 18.8970 μs | 11.2453 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 14.225 μs |  0.9146 μs |  0.5443 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  3.873 μs |  0.7857 μs |  0.4676 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,514.4 ns | 1,279.4 ns | 846.2 ns |  0.53 |    0.28 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,887.4 ns |   270.8 ns | 179.1 ns |  0.66 |    0.07 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,650.9 ns |   292.7 ns | 174.2 ns |  0.58 |    0.06 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,924.6 ns |   285.0 ns | 149.1 ns |  1.02 |    0.07 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    776.2 ns |   260.1 ns | 172.0 ns |  0.27 |    0.06 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 29,662.4 ns |   939.5 ns | 559.1 ns | 10.37 |    0.50 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  2,864.8 ns |   264.1 ns | 138.1 ns |  1.00 |    0.06 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,751.6 ns |   812.5 ns | 537.4 ns |  1.31 |    0.19 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Median      | Allocated |
|------------------------ |-------------:|------------:|------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    15.294 μs |   0.8914 μs |   0.4662 μs |    15.13 μs |   77672 B |
| &#39;Snappy Compress 1MB&#39;   |   463.354 μs |  32.6216 μs |  19.4126 μs |   461.10 μs |   78392 B |
| &#39;Snappy Decompress 1KB&#39; |     9.917 μs |   1.1463 μs |   0.6821 μs |    10.00 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,724.869 μs | 740.4256 μs | 489.7457 μs | 1,359.24 μs |         - |


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