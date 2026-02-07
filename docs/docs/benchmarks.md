---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-07 17:09 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,947.84 μs** |    **52.672 μs** |    **34.839 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,273.38 μs |   108.899 μs |    72.030 μs |  0.21 |    0.01 |        - |       - |   37.64 KB |        0.35 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,208.34 μs** |    **71.753 μs** |    **37.528 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,535.57 μs |   320.663 μs |   212.098 μs |  0.35 |    0.03 |  23.4375 |  7.8125 |  476.22 KB |        0.45 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,669.26 μs** |    **25.124 μs** |    **14.951 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,130.98 μs |    12.540 μs |     8.294 μs |  0.17 |    0.00 |   1.9531 |       - |   43.96 KB |        0.23 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,659.84 μs** |   **329.566 μs** |   **217.988 μs** |  **1.00** |    **0.03** | **109.3750** | **62.5000** | **1937.84 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,468.70 μs | 3,077.145 μs | 1,831.160 μs |  0.81 |    0.15 |  62.5000 | 46.8750 | 1194.88 KB |        0.62 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.72 μs** |     **2.534 μs** |     **1.676 μs** |  **1.00** |    **0.02** |   **2.4414** |       **-** |   **42.11 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     79.10 μs |    28.551 μs |    18.885 μs |  0.61 |    0.14 |   0.2441 |       - |    4.93 KB |        0.12 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,344.63 μs** |    **17.721 μs** |    **11.721 μs** |  **1.00** |    **0.01** |  **25.3906** |       **-** |  **421.63 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    705.66 μs |   118.800 μs |    78.579 μs |  0.52 |    0.06 |   7.8125 |  3.9063 |  174.55 KB |        0.41 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    810.05 μs |    86.516 μs |    57.225 μs |     ? |       ? |   0.4883 |       - |   13.14 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,736.33 μs |   203.523 μs |   106.446 μs |     ? |       ? |   7.8125 |       - |  131.47 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,375.04 μs** |     **7.901 μs** |     **4.702 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,343.48 μs |    21.184 μs |    14.012 μs |  0.25 |    0.00 |        - |       - |    1.55 KB |        1.30 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,371.37 μs** |     **4.399 μs** |     **2.618 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,314.11 μs |    64.193 μs |    42.460 μs |  0.24 |    0.01 |        - |       - |    1.56 KB |        1.31 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,374.08 μs** |     **4.971 μs** |     **2.600 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,341.02 μs |    25.535 μs |    16.890 μs |  0.25 |    0.00 |        - |       - |    1.56 KB |        0.75 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,373.56 μs** |     **4.014 μs** |     **2.389 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,341.37 μs |    29.022 μs |    19.196 μs |  0.25 |    0.00 |        - |       - |    1.55 KB |        0.75 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-DRKKNA(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-DRKKNA(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.177 s** | **0.0050 s** | **0.0013 s** |  **1.00** |   **77008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0031 s | 0.0008 s |  0.95 |  121536 B |        1.58 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.175 s** | **0.0022 s** | **0.0006 s** |  **1.00** |  **257008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.016 s | 0.0033 s | 0.0009 s |  0.95 |  314712 B |        1.22 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.176 s** | **0.0019 s** | **0.0005 s** |  **1.00** |  **617008 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0025 s | 0.0006 s |  0.95 |  370376 B |        0.60 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.175 s** | **0.0019 s** | **0.0005 s** |  **1.00** | **2424736 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.016 s | 0.0049 s | 0.0013 s |  0.95 | 2179992 B |        0.90 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.164 s** | **0.0047 s** | **0.0012 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.012 s | 0.0018 s | 0.0005 s |  0.95 |   83536 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.164 s** | **0.0023 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0031 s | 0.0008 s |  0.95 |   87656 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.164 s** | **0.0019 s** | **0.0005 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.012 s | 0.0034 s | 0.0005 s |  0.95 |   91416 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.164 s** | **0.0023 s** | **0.0004 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.013 s | 0.0028 s | 0.0007 s |  0.95 |   88200 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 34.391 μs | 16.6774 μs | 9.9245 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.414 μs |  0.4625 μs | 0.3059 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 10.929 μs |  0.1960 μs | 0.1025 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 34.167 μs | 12.1621 μs | 7.2375 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 15.409 μs |  9.1012 μs | 5.4160 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 26.503 μs |  7.6353 μs | 4.5436 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 16.095 μs |  0.5534 μs | 0.3293 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.340 μs |  0.0854 μs | 0.0508 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,409.8 ns |   225.0 ns |   117.66 ns |  0.31 |    0.03 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,604.5 ns |   137.4 ns |    90.87 ns |  0.36 |    0.03 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,894.6 ns |   500.5 ns |   297.82 ns |  0.42 |    0.07 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,564.1 ns |   195.5 ns |   116.36 ns |  0.79 |    0.05 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    740.1 ns |   103.9 ns |    68.71 ns |  0.16 |    0.02 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 45,581.3 ns | 7,117.2 ns | 4,235.35 ns | 10.14 |    1.03 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,506.1 ns |   343.5 ns |   227.21 ns |  1.00 |    0.07 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,471.4 ns |   108.7 ns |    64.67 ns |  0.77 |    0.04 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Allocated |
|------------------------ |-------------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.763 μs |  2.228 μs |  1.474 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   510.058 μs | 20.164 μs | 13.337 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     8.473 μs |  1.560 μs |  1.032 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,629.072 μs | 39.627 μs | 26.211 μs |         - |


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