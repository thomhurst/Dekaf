---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-07 14:59 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,953.39 μs** |    **77.730 μs** |    **51.413 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,110.25 μs |     2.494 μs |     1.484 μs |  0.19 |    0.00 |   1.9531 |       - |   37.63 KB |        0.35 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,308.17 μs** |    **51.011 μs** |    **30.356 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1062.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,355.78 μs |    91.986 μs |    48.111 μs |  0.32 |    0.01 |  23.4375 |       - |  477.46 KB |        0.45 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,667.71 μs** |   **112.190 μs** |    **74.207 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,145.27 μs |     9.122 μs |     4.771 μs |  0.17 |    0.00 |   1.9531 |       - |   44.07 KB |        0.23 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,857.06 μs** |   **238.877 μs** |   **158.003 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,289.36 μs | 2,043.043 μs | 1,215.782 μs |  0.78 |    0.10 |  62.5000 | 46.8750 | 1186.24 KB |        0.61 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.73 μs** |     **3.559 μs** |     **2.354 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |    **42.1 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     83.34 μs |    28.688 μs |    18.975 μs |  0.63 |    0.14 |   0.2441 |       - |    4.91 KB |        0.12 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,338.27 μs** |    **19.132 μs** |    **12.654 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **421.75 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    692.54 μs |    84.758 μs |    56.062 μs |  0.52 |    0.04 |   7.8125 |  3.9063 |  175.31 KB |        0.42 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    774.90 μs |    17.704 μs |     9.259 μs |     ? |       ? |   0.4883 |       - |   13.18 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,820.59 μs |   354.332 μs |   210.858 μs |     ? |       ? |   7.8125 |       - |  131.42 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,381.74 μs** |     **4.478 μs** |     **2.962 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,269.41 μs |    27.361 μs |    18.097 μs |  0.24 |    0.00 |        - |       - |    1.55 KB |        1.30 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,386.75 μs** |     **3.160 μs** |     **1.881 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,298.49 μs |    90.745 μs |    60.022 μs |  0.24 |    0.01 |        - |       - |    1.56 KB |        1.31 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,389.16 μs** |     **2.426 μs** |     **1.605 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,356.40 μs |    30.500 μs |    20.174 μs |  0.25 |    0.00 |        - |       - |    1.55 KB |        0.75 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,390.15 μs** |     **4.153 μs** |     **2.747 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,100.39 μs |     3.956 μs |     2.069 μs |  0.20 |    0.00 |        - |       - |    1.56 KB |        0.75 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-RHYPSS(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-RHYPSS(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.177 s** | **0.0046 s** | **0.0012 s** |  **1.00** |    **75.2 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0034 s | 0.0005 s |  0.95 |   112.8 KB |        1.50 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.174 s** | **0.0030 s** | **0.0005 s** |  **1.00** |  **250.98 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0031 s | 0.0005 s |  0.95 |  301.49 KB |        1.20 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.175 s** | **0.0052 s** | **0.0008 s** |  **1.00** |  **602.27 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.014 s | 0.0012 s | 0.0002 s |  0.95 |   363.2 KB |        0.60 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.174 s** | **0.0020 s** | **0.0005 s** |  **1.00** | **2368.19 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.015 s | 0.0029 s | 0.0008 s |  0.95 | 2126.48 KB |        0.90 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.177 s** | **0.0023 s** | **0.0004 s** |  **1.00** |   **16.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.013 s | 0.0017 s | 0.0004 s |  0.95 |  106.13 KB |        6.56 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.177 s** | **0.0019 s** | **0.0005 s** |  **1.00** |   **17.66 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0029 s | 0.0008 s |  0.95 |   106.1 KB |        6.01 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.179 s** | **0.0060 s** | **0.0009 s** |  **1.00** |    **15.9 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.013 s | 0.0013 s | 0.0002 s |  0.95 |  104.96 KB |        6.60 |
|                      |            |              |             |         |          |          |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.179 s** | **0.0055 s** | **0.0014 s** |  **1.00** |   **17.96 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.014 s | 0.0011 s | 0.0003 s |  0.95 |  107.83 KB |        6.00 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev     | Allocated |
|--------------------------------- |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 26.441 μs | 22.3707 μs | 13.3124 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 10.281 μs |  0.1807 μs |  0.1076 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 13.452 μs |  0.2262 μs |  0.1496 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 43.198 μs | 10.7019 μs |  5.5973 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 14.807 μs |  9.8452 μs |  5.8587 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 26.538 μs |  8.4442 μs |  5.0250 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.863 μs |  0.7444 μs |  0.4430 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.774 μs |  0.7506 μs |  0.4466 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,386.1 ns | 222.60 ns | 147.24 ns |  0.31 |    0.03 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,552.7 ns |  90.33 ns |  59.75 ns |  0.34 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,839.5 ns | 253.53 ns | 167.70 ns |  0.41 |    0.04 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,146.9 ns | 304.49 ns | 201.40 ns |  0.69 |    0.05 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    711.9 ns |  37.24 ns |  24.63 ns |  0.16 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 33,675.0 ns | 740.40 ns | 387.24 ns |  7.43 |    0.33 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,539.2 ns | 305.25 ns | 201.90 ns |  1.00 |    0.06 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,571.5 ns | 226.53 ns | 149.84 ns |  0.79 |    0.05 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev     | Allocated |
|------------------------ |-------------:|----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    14.461 μs |  2.318 μs |  1.2122 μs |   77672 B |
| &#39;Snappy Compress 1MB&#39;   |   927.252 μs | 22.257 μs | 13.2451 μs |   78104 B |
| &#39;Snappy Decompress 1KB&#39; |     9.969 μs |  1.602 μs |  0.9535 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 2,050.204 μs | 26.188 μs | 15.5842 μs |         - |


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