---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-03-13 21:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,989.1 μs** |  **82.79 μs** |  **54.76 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.95 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,183.3 μs |  23.02 μs |  13.70 μs |  0.20 |    0.00 |        - |       - |   52.18 KB |        0.49 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,315.2 μs** |  **88.30 μs** |  **52.55 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1063.36 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,357.9 μs | 136.15 μs |  81.02 μs |  0.32 |    0.01 |  31.2500 | 15.6250 |  604.38 KB |        0.57 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,437.8 μs** |  **94.74 μs** |  **56.38 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |   **194.5 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,658.5 μs |  12.85 μs |   6.72 μs |  0.26 |    0.00 |   3.9063 |       - |   84.05 KB |        0.43 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,478.9 μs** | **256.27 μs** | **169.51 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1938.77 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,864.1 μs | 114.56 μs |  68.17 μs |  0.55 |    0.01 |  93.7500 | 78.1250 | 1681.25 KB |        0.87 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **162.2 μs** |  **46.56 μs** |  **30.79 μs** |  **1.03** |    **0.25** |   **2.4414** |       **-** |   **42.17 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    122.2 μs |  19.57 μs |  12.94 μs |  0.78 |    0.15 |   0.9766 |  0.4883 |   17.61 KB |        0.42 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,413.6 μs** |  **55.20 μs** |  **32.85 μs** |  **1.00** |    **0.03** |  **25.3906** |       **-** |  **422.94 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,145.5 μs | 176.34 μs | 116.64 μs |  0.81 |    0.08 |   7.8125 |       - |  159.78 KB |        0.38 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |        **NA** |        **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    672.2 μs |  19.48 μs |  10.19 μs |     ? |       ? |   1.9531 |       - |   38.85 KB |           ? |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |        **NA** |        **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,393.2 μs | 102.99 μs |  68.12 μs |     ? |       ? |  23.4375 |       - |  397.46 KB |           ? |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,403.1 μs** |   **4.43 μs** |   **2.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |     **1.6 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,201.3 μs | 192.80 μs | 127.53 μs |  0.22 |    0.02 |        - |       - |    9.97 KB |        6.23 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,402.5 μs** |   **3.79 μs** |   **2.25 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.55 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,367.7 μs | 108.65 μs |  64.66 μs |  0.25 |    0.01 |        - |       - |    9.97 KB |        6.42 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,402.2 μs** |   **3.04 μs** |   **2.01 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.43 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,369.8 μs | 114.59 μs |  68.19 μs |  0.25 |    0.01 |        - |       - |    9.97 KB |        4.11 |
|                         |               |             |           |             |           |           |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,406.4 μs** |   **8.28 μs** |   **5.48 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.43 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,357.9 μs | 109.65 μs |  72.52 μs |  0.25 |    0.01 |        - |       - |    9.97 KB |        4.11 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-AFNPNR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-AFNPNR(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.167 s** | **0.0040 s** | **0.0010 s** |  **1.00** |   **76880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.012 s | 0.0051 s | 0.0013 s |  0.95 |  129248 B |        1.68 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.165 s** | **0.0025 s** | **0.0006 s** |  **1.00** |  **256880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.010 s | 0.0034 s | 0.0005 s |  0.95 |  388288 B |        1.51 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.165 s** | **0.0030 s** | **0.0008 s** |  **1.00** |  **616880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.010 s | 0.0022 s | 0.0006 s |  0.95 |  543928 B |        0.88 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.164 s** | **0.0026 s** | **0.0007 s** |  **1.00** | **2424896 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.011 s | 0.0031 s | 0.0008 s |  0.95 | 2285536 B |        0.94 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.157 s** | **0.0034 s** | **0.0009 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.006 s | 0.0031 s | 0.0008 s |  0.95 |   31528 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.156 s** | **0.0037 s** | **0.0010 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.007 s | 0.0023 s | 0.0006 s |  0.95 |   34320 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.157 s** | **0.0064 s** | **0.0017 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.006 s | 0.0009 s | 0.0001 s |  0.95 |   33008 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.156 s** | **0.0048 s** | **0.0012 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.007 s | 0.0045 s | 0.0012 s |  0.95 |   35656 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Median    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 30.951 μs | 10.6305 μs | 6.3260 μs | 32.821 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 11.080 μs |  1.0085 μs | 0.6670 μs | 10.809 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 11.278 μs |  0.7106 μs | 0.4229 μs | 11.101 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 34.205 μs | 12.2506 μs | 7.2902 μs | 36.684 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 16.560 μs |  8.9116 μs | 5.3031 μs | 20.308 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 25.127 μs |  9.2971 μs | 5.5325 μs | 26.911 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 19.968 μs |  2.0218 μs | 1.0574 μs | 20.227 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.273 μs |  0.0607 μs | 0.0317 μs |  4.268 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,382.0 ns |    44.98 ns |  23.53 ns |  0.36 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,998.9 ns |   113.93 ns |  75.36 ns |  0.51 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,534.2 ns |   136.85 ns |  81.44 ns |  0.39 |    0.02 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,770.2 ns |   184.64 ns |  96.57 ns |  0.71 |    0.03 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    677.1 ns |    33.00 ns |  19.63 ns |  0.17 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 32,423.2 ns | 1,538.75 ns | 804.80 ns |  8.35 |    0.27 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,886.6 ns |   174.95 ns |  91.50 ns |  1.00 |    0.03 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,419.8 ns |   131.36 ns |  86.89 ns |  0.88 |    0.03 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev     | Allocated |
|------------------------ |-------------:|----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.100 μs |  1.552 μs |  1.0269 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   928.266 μs | 27.148 μs | 17.9565 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     9.881 μs |  1.043 μs |  0.6205 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,619.929 μs | 35.544 μs | 23.5101 μs |         - |


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