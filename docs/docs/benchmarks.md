---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-03-14 10:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error      | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-----------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,043.47 μs** | **105.165 μs** |  **62.582 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.95 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,314.10 μs | 118.921 μs |  78.659 μs |  0.22 |    0.01 |       - |       - |   47.35 KB |        0.44 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,374.53 μs** |  **73.497 μs** |  **48.614 μs** |  **1.00** |    **0.01** | **62.5000** | **31.2500** | **1063.36 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,339.45 μs | 148.698 μs |  88.488 μs |  0.32 |    0.01 | 31.2500 | 15.6250 |  586.12 KB |        0.55 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,197.45 μs** |  **95.257 μs** |  **63.007 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |   **194.5 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,684.14 μs |  10.691 μs |   5.592 μs |  0.27 |    0.00 |       - |       - |   61.77 KB |        0.32 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,718.59 μs** | **107.614 μs** |  **64.039 μs** |  **1.00** |    **0.01** | **93.7500** | **31.2500** | **1938.78 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,032.69 μs | 208.173 μs | 123.880 μs |  0.55 |    0.01 | 78.1250 | 62.5000 | 1454.55 KB |        0.75 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **146.53 μs** |  **14.449 μs** |   **9.557 μs** |  **1.00** |    **0.09** |  **2.1973** |       **-** |   **38.17 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     71.72 μs |   9.185 μs |   6.075 μs |  0.49 |    0.05 |  0.7324 |  0.4883 |   14.48 KB |        0.38 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,445.83 μs** |  **55.857 μs** |  **33.240 μs** |  **1.00** |    **0.03** | **25.3906** |       **-** |  **421.14 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    761.95 μs |  35.894 μs |  21.360 μs |  0.53 |    0.02 |  3.9063 |       - |  126.33 KB |        0.30 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |         **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    649.18 μs |  29.393 μs |  17.492 μs |     ? |       ? |  0.9766 |       - |   22.16 KB |           ? |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |         **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,088.67 μs | 120.007 μs |  71.414 μs |     ? |       ? |  7.8125 |       - |  223.11 KB |           ? |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,427.72 μs** |   **2.697 μs** |   **1.411 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.57 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,109.92 μs |   1.472 μs |   0.974 μs |  0.20 |    0.00 |       - |       - |    5.03 KB |        3.20 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,432.73 μs** |   **5.412 μs** |   **3.580 μs** |  **1.00** |    **0.00** |       **-** |       **-** |     **1.6 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,397.77 μs |  61.330 μs |  40.566 μs |  0.26 |    0.01 |       - |       - |    5.03 KB |        3.15 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,438.29 μs** |   **3.245 μs** |   **2.146 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.43 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,344.85 μs |  55.353 μs |  36.613 μs |  0.25 |    0.01 |       - |       - |    5.03 KB |        2.07 |
|                         |               |             |           |              |            |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,433.16 μs** |   **5.846 μs** |   **3.867 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.43 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,118.30 μs |  18.033 μs |  10.731 μs |  0.21 |    0.00 |       - |       - |    5.03 KB |        2.07 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-RXGBCD(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-RXGBCD(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.169 s** | **0.0024 s** | **0.0006 s** |  **1.00** |   **76592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.012 s | 0.0031 s | 0.0008 s |  0.95 |  129184 B |        1.69 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.166 s** | **0.0029 s** | **0.0004 s** |  **1.00** |  **255920 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.011 s | 0.0023 s | 0.0006 s |  0.95 |  322816 B |        1.26 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.166 s** | **0.0038 s** | **0.0010 s** |  **1.00** |  **616880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.011 s | 0.0039 s | 0.0010 s |  0.95 |  536296 B |        0.87 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.167 s** | **0.0043 s** | **0.0011 s** |  **1.00** | **2424608 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.011 s | 0.0018 s | 0.0005 s |  0.95 | 2285520 B |        0.94 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.157 s** | **0.0082 s** | **0.0013 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.006 s | 0.0034 s | 0.0005 s |  0.95 |   31680 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.158 s** | **0.0026 s** | **0.0007 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.007 s | 0.0017 s | 0.0004 s |  0.95 |   34920 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.158 s** | **0.0046 s** | **0.0012 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.006 s | 0.0020 s | 0.0005 s |  0.95 |   32752 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.157 s** | **0.0053 s** | **0.0014 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.007 s | 0.0017 s | 0.0003 s |  0.95 |   36576 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev     | Allocated |
|--------------------------------- |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 31.097 μs |  9.9333 μs |  5.9111 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.526 μs |  0.8906 μs |  0.5891 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 10.960 μs |  0.1395 μs |  0.0830 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 34.475 μs | 12.0919 μs |  7.1957 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 19.255 μs | 22.6910 μs | 13.5030 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 26.225 μs | 11.7064 μs |  6.9663 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.980 μs |  0.3919 μs |  0.2050 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.418 μs |  0.0686 μs |  0.0359 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,338.8 ns |  46.81 ns |  27.86 ns |  0.31 |    0.02 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,735.0 ns | 292.09 ns | 193.20 ns |  0.40 |    0.05 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,910.4 ns | 297.92 ns | 197.06 ns |  0.44 |    0.05 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,842.1 ns | 406.24 ns | 268.70 ns |  0.65 |    0.07 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    998.4 ns | 349.51 ns | 231.18 ns |  0.23 |    0.05 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 31,746.7 ns | 568.92 ns | 376.31 ns |  7.29 |    0.50 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,373.4 ns | 454.80 ns | 300.82 ns |  1.00 |    0.09 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,716.3 ns | 226.08 ns | 149.54 ns |  0.85 |    0.07 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.883 μs |  0.1866 μs |  0.0976 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   936.355 μs | 30.7021 μs | 20.3076 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     7.882 μs |  0.2022 μs |  0.1203 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,651.158 μs | 24.6238 μs | 16.2871 μs |         - |


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