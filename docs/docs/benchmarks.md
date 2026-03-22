---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-03-22 15:57 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0    | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,913.64 μs** |   **111.984 μs** |  **74.071 μs** |  **1.00** |    **0.02** |       **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,338.65 μs |   149.387 μs |  98.810 μs |  0.23 |    0.02 |       - |       - |   35.83 KB |        0.34 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,242.95 μs** |    **93.852 μs** |  **62.077 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1062.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,257.62 μs |    57.221 μs |  34.051 μs |  0.31 |    0.01 | 15.6250 |  7.8125 |  488.02 KB |        0.46 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,468.26 μs** |    **24.640 μs** |  **14.663 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,115.13 μs |    27.604 μs |  16.427 μs |  0.17 |    0.00 |       - |       - |   39.28 KB |        0.20 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,991.68 μs** | **1,046.170 μs** | **547.167 μs** |  **1.00** |    **0.08** | **78.1250** | **31.2500** | **1937.82 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 5,407.75 μs |   484.469 μs | 320.447 μs |  0.60 |    0.05 | 46.8750 | 15.6250 |  1225.2 KB |        0.63 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **123.17 μs** |     **5.228 μs** |   **3.458 μs** |  **1.00** |    **0.04** |  **1.7090** |       **-** |   **42.09 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    58.69 μs |     6.282 μs |   4.155 μs |  0.48 |    0.03 |       - |       - |    6.81 KB |        0.16 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,261.30 μs** |    **39.552 μs** |  **26.161 μs** |  **1.00** |    **0.03** | **15.6250** |       **-** |  **421.32 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   678.08 μs |   113.453 μs |  67.514 μs |  0.54 |    0.05 |       - |       - |   43.97 KB |        0.10 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |          **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   459.95 μs |    26.742 μs |  17.688 μs |     ? |       ? |       - |       - |    8.37 KB |           ? |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |          **NA** |           **NA** |         **NA** |     **?** |       **?** |      **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 4,306.57 μs |   137.199 μs |  81.645 μs |     ? |       ? |       - |       - |   84.66 KB |           ? |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,366.03 μs** |    **17.476 μs** |  **11.560 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,254.54 μs |    25.299 μs |  16.734 μs |  0.23 |    0.00 |       - |       - |    1.61 KB |        1.36 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,368.40 μs** |    **17.672 μs** |  **11.689 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,236.80 μs |    28.179 μs |  18.639 μs |  0.23 |    0.00 |       - |       - |    1.61 KB |        1.36 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,379.24 μs** |    **10.845 μs** |   **6.454 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,207.71 μs |    33.761 μs |  22.331 μs |  0.22 |    0.00 |       - |       - |    1.61 KB |        0.78 |
|                         |               |             |           |             |              |            |       |         |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,363.40 μs** |     **8.927 μs** |   **5.905 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,146.42 μs |    13.314 μs |   8.806 μs |  0.21 |    0.00 |       - |       - |    1.61 KB |        0.78 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-CVXFWD(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-CVXFWD(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean    | Error    | StdDev   | Ratio | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |--------:|---------:|---------:|------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3.168 s** | **0.0034 s** | **0.0009 s** |  **1.00** |   **76592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         | 3.011 s | 0.0020 s | 0.0005 s |  0.95 |  133448 B |        1.74 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3.165 s** | **0.0026 s** | **0.0007 s** |  **1.00** |  **256544 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        | 3.009 s | 0.0045 s | 0.0007 s |  0.95 |  320856 B |        1.25 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3.165 s** | **0.0021 s** | **0.0006 s** |  **1.00** |  **616592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         | 3.010 s | 0.0025 s | 0.0006 s |  0.95 |  542512 B |        0.88 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3.165 s** | **0.0044 s** | **0.0012 s** |  **1.00** | **2424608 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        | 3.010 s | 0.0026 s | 0.0004 s |  0.95 | 2289088 B |        0.94 |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3.157 s** | **0.0015 s** | **0.0004 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         | 3.006 s | 0.0049 s | 0.0008 s |  0.95 |   31720 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3.156 s** | **0.0053 s** | **0.0014 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        | 3.006 s | 0.0016 s | 0.0002 s |  0.95 |   34296 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3.157 s** | **0.0037 s** | **0.0010 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         | 3.006 s | 0.0016 s | 0.0004 s |  0.95 |   32416 B |          NA |
|                      |            |              |             |         |          |          |       |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3.156 s** | **0.0035 s** | **0.0009 s** |  **1.00** |         **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        | 3.006 s | 0.0022 s | 0.0006 s |  0.95 |   36360 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                           | Mean      | Error      | StdDev     | Allocated |
|--------------------------------- |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;              | 33.256 μs | 13.0221 μs |  7.7493 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;  | 13.601 μs |  0.2616 μs |  0.1730 μs |         - |
| &#39;Write 100 CompactStrings&#39;       | 11.224 μs |  0.0978 μs |  0.0511 μs |         - |
| &#39;Write 1000 VarInts&#39;             | 36.555 μs | 17.8534 μs | 10.6243 μs |         - |
| &#39;Read 1000 Int32s&#39;               | 16.521 μs | 10.0349 μs |  5.9716 μs |         - |
| &#39;Read 1000 VarInts&#39;              | 29.695 μs |  8.4714 μs |  5.0412 μs |         - |
| &#39;Write RecordBatch (10 records)&#39; | 17.055 μs |  0.4948 μs |  0.2945 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;  |  4.007 μs |  0.1558 μs |  0.0815 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,433.1 ns |  76.23 ns |  50.42 ns |  0.33 |    0.02 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,551.0 ns | 143.29 ns |  94.78 ns |  0.36 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  1,916.4 ns | 414.49 ns | 274.16 ns |  0.44 |    0.06 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,874.9 ns |  36.05 ns |  21.45 ns |  0.67 |    0.02 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    713.0 ns |  64.41 ns |  38.33 ns |  0.17 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 32,273.4 ns | 370.32 ns | 220.37 ns |  7.48 |    0.26 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,321.6 ns | 269.47 ns | 160.36 ns |  1.00 |    0.05 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,662.2 ns | 206.22 ns | 122.72 ns |  0.85 |    0.04 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.210 μs |  0.2835 μs |  0.1875 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   529.566 μs | 23.2052 μs | 15.3488 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     9.823 μs |  0.3748 μs |  0.2479 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,622.605 μs | 30.4440 μs | 18.1167 μs |         - |


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