---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-03-14 11:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,931.01 μs** |    **71.565 μs** |    **47.336 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.94 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,362.99 μs |    67.892 μs |    40.401 μs |  0.23 |    0.01 |       - |       - |   47.35 KB |        0.44 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,282.88 μs** |    **70.299 μs** |    **46.498 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1063.29 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,290.42 μs |   105.874 μs |    63.004 μs |  0.31 |    0.01 | 23.4375 |  7.8125 |  577.51 KB |        0.54 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,543.76 μs** |    **17.821 μs** |     **9.321 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.43 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,148.01 μs |    55.850 μs |    33.235 μs |  0.18 |    0.00 |       - |       - |   60.12 KB |        0.31 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,494.19 μs** | **1,999.892 μs** | **1,190.104 μs** |  **1.01** |    **0.16** | **78.1250** | **31.2500** | **1938.46 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,412.45 μs |   343.502 μs |   204.413 μs |  0.58 |    0.07 | 46.8750 | 15.6250 | 1425.73 KB |        0.74 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **112.24 μs** |    **47.699 μs** |    **31.550 μs** |  **1.07** |    **0.40** |  **1.9531** |       **-** |   **49.08 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    62.42 μs |     8.124 μs |     4.835 μs |  0.60 |    0.16 |  0.2441 |       - |    6.48 KB |        0.13 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,275.58 μs** |    **26.347 μs** |    **17.427 μs** |  **1.00** |    **0.02** | **15.6250** |       **-** |  **421.63 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   709.34 μs |    78.219 μs |    51.737 μs |  0.56 |    0.04 |       - |       - |   89.25 KB |        0.21 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   478.27 μs |    35.369 μs |    21.048 μs |     ? |       ? |  0.4883 |       - |   21.08 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 4,621.48 μs |   638.369 μs |   379.883 μs |     ? |       ? |  7.8125 |       - |  212.99 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,386.42 μs** |    **16.885 μs** |    **11.169 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.57 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,241.39 μs |    58.540 μs |    34.836 μs |  0.23 |    0.01 |       - |       - |    5.03 KB |        3.21 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,368.04 μs** |    **17.411 μs** |    **10.361 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.56 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,303.01 μs |    49.939 μs |    33.031 μs |  0.24 |    0.01 |       - |       - |    5.03 KB |        3.22 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,369.71 μs** |    **16.119 μs** |     **9.592 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.43 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,315.10 μs |    50.045 μs |    33.102 μs |  0.24 |    0.01 |       - |       - |    5.03 KB |        2.07 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,368.15 μs** |     **9.121 μs** |     **5.428 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.43 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,115.77 μs |    35.831 μs |    18.741 μs |  0.21 |    0.00 |       - |       - |    5.03 KB |        2.07 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-TNLLLI(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-TNLLLI(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.168 s** | **0.0038 s** | **0.0006 s** |  **1.00** |   **76592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.011 s | 0.0036 s | 0.0006 s |  0.95 |  136096 B |        1.78 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.165 s** | **0.0050 s** | **0.0008 s** |  **1.00** |  **256592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.010 s | 0.0041 s | 0.0011 s |  0.95 |  327856 B |        1.28 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.165 s** | **0.0018 s** | **0.0005 s** |  **1.00** |  **616592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.011 s | 0.0052 s | 0.0013 s |  0.95 |  472272 B |        0.77 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.165 s** | **0.0018 s** | **0.0005 s** |  **1.00** | **2424608 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.011 s | 0.0007 s | 0.0002 s |  0.95 | 2286600 B |        0.94 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.157 s** | **0.0037 s** | **0.0010 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.006 s | 0.0021 s | 0.0005 s |  0.95 |   31784 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.157 s** | **0.0019 s** | **0.0003 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.007 s | 0.0020 s | 0.0005 s |  0.95 |   34640 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.157 s** | **0.0038 s** | **0.0010 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.006 s | 0.0013 s | 0.0003 s |  0.95 |   32616 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.157 s** | **0.0039 s** | **0.0010 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.006 s | 0.0017 s | 0.0003 s |  0.95 |   35792 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Median    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.987 μs |  9.4948 μs | 5.6502 μs | 22.072 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.422 μs |  0.3211 μs | 0.2124 μs | 13.450 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 11.317 μs |  0.1929 μs | 0.1276 μs | 11.322 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 34.771 μs | 12.7017 μs | 7.5586 μs | 37.374 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 17.396 μs | 14.4728 μs | 8.6126 μs | 22.151 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 29.935 μs | 14.3853 μs | 8.5604 μs | 34.084 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 21.088 μs |  2.2757 μs | 1.3542 μs | 21.429 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.321 μs |  0.2113 μs | 0.1397 μs |  4.319 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,496.7 ns | 229.12 ns | 136.35 ns |  0.36 |    0.03 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,634.8 ns | 126.99 ns |  83.99 ns |  0.40 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,577.2 ns |  37.96 ns |  19.85 ns |  0.38 |    0.01 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,937.0 ns |  97.86 ns |  58.23 ns |  0.71 |    0.02 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    646.4 ns |  44.55 ns |  26.51 ns |  0.16 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 32,499.2 ns | 304.97 ns | 181.49 ns |  7.86 |    0.17 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,136.3 ns | 137.44 ns |  90.91 ns |  1.00 |    0.03 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,529.3 ns | 312.95 ns | 186.23 ns |  0.85 |    0.05 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev      | Allocated |
|------------------------ |-------------:|-----------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.908 μs |   1.872 μs |   1.1140 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   926.587 μs |  11.170 μs |   5.8424 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     9.683 μs |   1.322 μs |   0.6917 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 2,059.509 μs | 748.319 μs | 494.9665 μs |         - |


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