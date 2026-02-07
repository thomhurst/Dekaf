---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-02-07 19:19 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,945.16 μs** |    **56.014 μs** |    **37.049 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,326.95 μs |    27.154 μs |    14.202 μs |  0.22 |    0.00 |       - |       - |   37.62 KB |        0.35 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,265.61 μs** |    **59.975 μs** |    **35.690 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,423.99 μs |   216.757 μs |   143.371 μs |  0.33 |    0.02 | 15.6250 |       - |  459.27 KB |        0.43 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,519.81 μs** |    **51.154 μs** |    **30.441 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,095.64 μs |     0.732 μs |     0.436 μs |  0.17 |    0.00 |       - |       - |   43.88 KB |        0.23 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,349.66 μs** |    **90.243 μs** |    **53.702 μs** |  **1.00** |    **0.01** | **78.1250** | **31.2500** | **1937.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 6,611.55 μs | 1,261.882 μs |   659.989 μs |  0.79 |    0.07 | 46.8750 | 15.6250 | 1153.02 KB |        0.60 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **121.35 μs** |     **3.402 μs** |     **2.250 μs** |  **1.00** |    **0.02** |  **1.7090** |       **-** |   **42.08 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    59.53 μs |     6.304 μs |     4.170 μs |  0.49 |    0.03 |  0.4883 |  0.2441 |   14.42 KB |        0.34 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,196.67 μs** |   **106.216 μs** |    **55.553 μs** |  **1.00** |    **0.07** | **15.6250** |       **-** |  **421.79 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   605.42 μs |    30.128 μs |    17.928 μs |  0.51 |    0.03 |  1.9531 |       - |   49.67 KB |        0.12 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   553.99 μs |    24.158 μs |    14.376 μs |     ? |       ? |  0.4883 |       - |   13.03 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |           **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 6,001.01 μs | 1,597.827 μs | 1,056.863 μs |     ? |       ? |  3.9063 |       - |  131.43 KB |           ? |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,380.83 μs** |    **41.970 μs** |    **27.761 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,284.26 μs |    34.926 μs |    23.101 μs |  0.24 |    0.00 |       - |       - |    1.55 KB |        1.30 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,839.62 μs** | **1,157.129 μs** |   **765.369 μs** |  **1.01** |    **0.17** |       **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,283.23 μs |    33.286 μs |    22.016 μs |  0.22 |    0.02 |       - |       - |    1.55 KB |        1.30 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,374.52 μs** |    **22.065 μs** |    **14.595 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,281.64 μs |    22.800 μs |    15.081 μs |  0.24 |    0.00 |       - |       - |    1.55 KB |        0.75 |
|                         |               |             |           |             |              |              |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,392.10 μs** |    **60.148 μs** |    **39.784 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **2.07 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,276.16 μs |    28.759 μs |    19.022 μs |  0.24 |    0.00 |       - |       - |    1.55 KB |        0.75 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-BISDHS(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-BISDHS(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.178 s** | **0.0030 s** | **0.0005 s** |  **1.00** |   **76720 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.015 s | 0.0018 s | 0.0005 s |  0.95 |  127136 B |        1.66 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.175 s** | **0.0037 s** | **0.0010 s** |  **1.00** |  **256720 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.015 s | 0.0034 s | 0.0005 s |  0.95 |  308136 B |        1.20 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.175 s** | **0.0039 s** | **0.0010 s** |  **1.00** |  **616720 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.015 s | 0.0007 s | 0.0002 s |  0.95 |  370336 B |        0.60 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.175 s** | **0.0022 s** | **0.0006 s** |  **1.00** | **2424736 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.016 s | 0.0064 s | 0.0017 s |  0.95 | 2233416 B |        0.92 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.165 s** | **0.0015 s** | **0.0004 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.013 s | 0.0031 s | 0.0008 s |  0.95 |   83992 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.164 s** | **0.0041 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.013 s | 0.0017 s | 0.0004 s |  0.95 |   86776 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.165 s** | **0.0030 s** | **0.0008 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.013 s | 0.0021 s | 0.0005 s |  0.95 |   84400 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.165 s** | **0.0022 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.013 s | 0.0030 s | 0.0008 s |  0.95 |   87976 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev    | Allocated |
|--------------------------------- |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 20.343 μs |  9.1753 μs | 5.4601 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 11.325 μs |  1.9670 μs | 1.3010 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 11.034 μs |  0.2075 μs | 0.1235 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 37.529 μs | 15.7568 μs | 8.2411 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 14.800 μs |  9.5988 μs | 5.7121 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 25.052 μs |  9.5848 μs | 5.7038 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 18.192 μs |  2.5483 μs | 1.6855 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.810 μs |  0.7882 μs | 0.5214 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,176.0 ns | 171.17 ns |  89.53 ns |  0.28 |    0.03 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,675.8 ns | 143.61 ns |  94.99 ns |  0.41 |    0.03 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,498.8 ns | 103.37 ns |  54.06 ns |  0.36 |    0.03 |         - |          NA |
| &#39;Deserialize String&#39;                 |  3,374.7 ns | 387.20 ns | 256.11 ns |  0.82 |    0.08 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    597.7 ns |  60.41 ns |  35.95 ns |  0.14 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 34,675.4 ns | 886.00 ns | 527.25 ns |  8.40 |    0.52 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,143.3 ns | 412.36 ns | 272.75 ns |  1.00 |    0.09 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,565.2 ns |  59.48 ns |  39.34 ns |  0.86 |    0.05 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Allocated |
|------------------------ |-------------:|------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.071 μs |   0.1502 μs |   0.0894 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   505.093 μs |  31.9018 μs |  21.1011 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     8.652 μs |   1.3153 μs |   0.8700 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 2,867.898 μs | 791.5803 μs | 523.5813 μs |         - |


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