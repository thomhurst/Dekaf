---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-03-30 02:06 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,018.52 μs** |    **78.607 μs** |    **51.993 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,150.34 μs |    64.800 μs |    42.861 μs |  0.19 |    0.01 |        - |       - |   48.21 KB |        0.45 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,339.86 μs** |    **51.319 μs** |    **33.944 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1062.82 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,323.94 μs |    38.530 μs |    20.152 μs |  0.32 |    0.00 |  31.2500 | 15.6250 |  523.11 KB |        0.49 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,588.08 μs** |    **42.768 μs** |    **28.288 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,208.13 μs |    24.255 μs |    12.686 μs |  0.18 |    0.00 |   3.9063 |       - |   110.6 KB |        0.57 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,750.21 μs** |   **772.855 μs** |   **404.218 μs** |  **1.00** |    **0.04** | **109.3750** | **31.2500** | **1937.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,484.85 μs |   127.196 μs |    75.692 μs |  0.51 |    0.02 |  78.1250 | 62.5000 | 1376.12 KB |        0.71 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **163.50 μs** |    **24.034 μs** |    **15.897 μs** |  **1.01** |    **0.14** |   **2.6855** |       **-** |   **44.49 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.40 μs |     2.749 μs |     1.438 μs |  0.36 |    0.04 |   0.4883 |  0.2441 |   14.94 KB |        0.34 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,428.43 μs** |    **60.507 μs** |    **40.022 μs** |  **1.00** |    **0.04** |  **25.3906** |       **-** |  **421.37 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    704.00 μs |    81.843 μs |    54.134 μs |  0.49 |    0.04 |   3.9063 |       - |  156.41 KB |        0.37 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,144.63 μs |   129.435 μs |    85.613 μs |     ? |       ? |   0.9766 |       - |   19.64 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,664.89 μs | 2,159.923 μs | 1,428.655 μs |     ? |       ? |   7.8125 |       - |  209.95 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,410.19 μs** |     **5.435 μs** |     **3.595 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.19 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,353.26 μs |    31.933 μs |    21.121 μs |  0.25 |    0.00 |        - |       - |    1.46 KB |        1.23 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,420.63 μs** |    **11.889 μs** |     **7.075 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,382.22 μs |    30.925 μs |    20.455 μs |  0.25 |    0.00 |        - |       - |    1.46 KB |        1.24 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,427.60 μs** |    **12.595 μs** |     **8.331 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,400.24 μs |    25.822 μs |    17.080 μs |  0.26 |    0.00 |        - |       - |    1.46 KB |        0.71 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,410.82 μs** |     **6.587 μs** |     **4.357 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,115.40 μs |     2.431 μs |     1.608 μs |  0.21 |    0.00 |        - |       - |    1.46 KB |        0.71 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-AYGBDK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-AYGBDK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.167 s** | **0.0024 s** | **0.0006 s** |  **1.00** |   **76880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.016 s | 0.0058 s | 0.0015 s |  0.95 |  509472 B |        6.63 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.164 s** | **0.0016 s** | **0.0004 s** |  **1.00** |  **256880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.013 s | 0.0062 s | 0.0010 s |  0.95 | 1134296 B |        4.42 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.164 s** | **0.0026 s** | **0.0007 s** |  **1.00** |  **616592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.013 s | 0.0057 s | 0.0009 s |  0.95 | 1413608 B |        2.29 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.165 s** | **0.0022 s** | **0.0006 s** |  **1.00** | **2424896 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.015 s | 0.0064 s | 0.0017 s |  0.95 | 6894552 B |        2.84 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.155 s** | **0.0063 s** | **0.0016 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.007 s | 0.0023 s | 0.0004 s |  0.95 |  245008 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.157 s** | **0.0024 s** | **0.0006 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.007 s | 0.0018 s | 0.0005 s |  0.95 | 1099984 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.157 s** | **0.0071 s** | **0.0011 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.008 s | 0.0040 s | 0.0010 s |  0.95 | 1098344 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.156 s** | **0.0061 s** | **0.0016 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.008 s | 0.0044 s | 0.0011 s |  0.95 | 3264776 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 30.343 μs |  9.9297 μs | 5.1935 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 12.623 μs |  2.3253 μs | 1.3837 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 10.878 μs |  0.1287 μs | 0.0766 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 34.907 μs | 12.8094 μs | 7.6227 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 21.256 μs | 12.9239 μs | 7.6908 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 25.393 μs |  9.0427 μs | 5.3811 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 20.685 μs |  2.0104 μs | 1.1963 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;           |  6.043 μs |  1.3107 μs | 0.7800 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 11.055 μs |  0.5629 μs | 0.3350 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,396.9 ns |    68.83 ns |    40.96 ns |  0.34 |    0.01 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,496.2 ns |    92.43 ns |    55.00 ns |  0.37 |    0.01 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,505.4 ns |    35.98 ns |    21.41 ns |  0.37 |    0.01 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,401.2 ns |   163.08 ns |    97.05 ns |  0.59 |    0.02 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    636.8 ns |    52.02 ns |    27.21 ns |  0.16 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 34,697.0 ns | 3,127.81 ns | 1,861.31 ns |  8.49 |    0.44 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,087.6 ns |    71.14 ns |    37.21 ns |  1.00 |    0.01 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,302.2 ns |    95.21 ns |    49.80 ns |  0.81 |    0.01 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.676 μs |  1.8058 μs |  1.1944 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   510.710 μs | 16.0543 μs |  9.5537 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     7.924 μs |  0.3326 μs |  0.2200 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,654.288 μs | 26.3461 μs | 17.4263 μs |         - |


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