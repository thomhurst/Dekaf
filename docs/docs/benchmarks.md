---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-08 02:44 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,080.1 μs** |   **123.12 μs** |    **81.43 μs** |  **6,063.7 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,280.1 μs |   180.82 μs |   119.60 μs |  1,242.4 μs |  0.21 |    0.02 |        - |       - |    37.8 KB |        0.35 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,399.5 μs** |    **64.56 μs** |    **38.42 μs** |  **7,407.4 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  5,521.0 μs | 3,784.04 μs | 2,251.82 μs |  4,285.1 μs |  0.75 |    0.29 |  15.6250 |       - |   476.6 KB |        0.45 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,637.0 μs** |    **53.62 μs** |    **35.46 μs** |  **6,636.1 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |          NA |          NA |          NA |          NA |     ? |       ? |       NA |      NA |         NA |           ? |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,839.8 μs** |   **294.77 μs** |   **175.42 μs** | **11,905.6 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |          NA |          NA |          NA |          NA |     ? |       ? |       NA |      NA |         NA |           ? |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.2 μs** |     **1.91 μs** |     **1.27 μs** |    **135.8 μs** |  **1.00** |    **0.01** |   **2.4414** |       **-** |   **42.23 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    115.9 μs |    56.13 μs |    37.13 μs |    134.0 μs |  0.85 |    0.26 |   0.2441 |       - |    5.11 KB |        0.12 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,370.0 μs** |   **156.45 μs** |    **93.10 μs** |  **1,356.1 μs** |  **1.00** |    **0.09** |  **23.4375** |       **-** |  **421.82 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    848.8 μs |    84.31 μs |    55.76 μs |    846.5 μs |  0.62 |    0.06 |        - |       - |   52.54 KB |        0.12 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |          **NA** |          **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,112.7 μs |    36.59 μs |    19.14 μs |  1,120.2 μs |     ? |       ? |        - |       - |   14.81 KB |           ? |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |          **NA** |          **NA** |          **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 11,510.4 μs |   805.96 μs |   421.53 μs | 11,464.4 μs |     ? |       ? |   7.8125 |       - |  149.27 KB |           ? |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,519.6 μs** |    **74.92 μs** |    **49.56 μs** |  **5,532.7 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,359.2 μs |    18.87 μs |    11.23 μs |  1,356.1 μs |  0.25 |    0.00 |        - |       - |     1.6 KB |        1.36 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,492.2 μs** |    **45.20 μs** |    **29.90 μs** |  **5,480.9 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,364.9 μs |    23.07 μs |    15.26 μs |  1,361.2 μs |  0.25 |    0.00 |        - |       - |     1.6 KB |        1.34 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,431.9 μs** |    **39.26 μs** |    **25.97 μs** |  **5,421.7 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,356.8 μs |    26.82 μs |    15.96 μs |  1,361.3 μs |  0.25 |    0.00 |        - |       - |    1.65 KB |        0.80 |
|                         |               |             |           |             |             |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,492.9 μs** |    **88.34 μs** |    **58.43 μs** |  **5,500.8 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,322.5 μs |   115.91 μs |    76.67 μs |  1,351.8 μs |  0.24 |    0.01 |        - |       - |     1.6 KB |        0.78 |

Benchmarks with issues:
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-VKCXRQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Dekaf_ProduceBatch: Job-VKCXRQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.Confluent_FireAndForget: Job-VKCXRQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-VKCXRQ(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.180 s** | **0.0016 s** | **0.0004 s** |  **1.00** |   **77008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0054 s | 0.0014 s |  0.95 |  115008 B |        1.49 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.175 s** | **0.0043 s** | **0.0007 s** |  **1.00** |  **257008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.016 s | 0.0020 s | 0.0005 s |  0.95 |  302712 B |        1.18 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.176 s** | **0.0014 s** | **0.0002 s** |  **1.00** |  **617008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0048 s | 0.0013 s |  0.95 |  370432 B |        0.60 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.175 s** | **0.0018 s** | **0.0003 s** |  **1.00** | **2424688 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.015 s | 0.0026 s | 0.0004 s |  0.95 | 2179704 B |        0.90 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.166 s** | **0.0010 s** | **0.0003 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.013 s | 0.0020 s | 0.0005 s |  0.95 |   84232 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.164 s** | **0.0020 s** | **0.0005 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0032 s | 0.0005 s |  0.95 |   86016 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.165 s** | **0.0082 s** | **0.0021 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.014 s | 0.0011 s | 0.0003 s |  0.95 |   84352 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.165 s** | **0.0039 s** | **0.0010 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.014 s | 0.0045 s | 0.0012 s |  0.95 |   87520 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.326 μs |  8.9263 μs | 5.3119 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 14.540 μs |  0.3610 μs | 0.2148 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 11.119 μs |  0.5193 μs | 0.2716 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 40.572 μs |  9.1106 μs | 5.4216 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 14.861 μs |  9.7656 μs | 5.8114 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 32.624 μs | 11.6328 μs | 6.9225 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 17.706 μs |  3.1794 μs | 1.8920 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  5.035 μs |  0.4018 μs | 0.2657 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,455.4 ns |    73.60 ns |    48.68 ns |  0.39 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,534.6 ns |    56.12 ns |    33.40 ns |  0.41 |    0.01 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,822.0 ns |   101.43 ns |    67.09 ns |  0.48 |    0.02 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,664.3 ns |   133.96 ns |    88.61 ns |  0.97 |    0.03 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    645.3 ns |    43.14 ns |    25.67 ns |  0.17 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 45,968.9 ns | 7,336.34 ns | 4,365.74 ns | 12.17 |    1.11 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,776.9 ns |    91.54 ns |    54.48 ns |  1.00 |    0.02 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,457.0 ns |    73.53 ns |    48.64 ns |  0.92 |    0.02 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev     | Allocated |
|------------------------ |-------------:|----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.145 μs |  1.042 μs |  0.6893 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   495.714 μs | 20.110 μs | 13.3013 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     9.143 μs |  1.710 μs |  1.1309 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,665.386 μs | 31.939 μs | 21.1254 μs |         - |


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