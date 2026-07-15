---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 01:37 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,187.7 μs** |    **968.35 μs** |    **53.08 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,347.0 μs |    601.51 μs |    32.97 μs |  0.54 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,197.1 μs** |    **812.37 μs** |    **44.53 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,677.0 μs |  2,353.72 μs |   129.02 μs |  0.51 |    0.02 |  15.6250 |       - |  347409 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,602.6 μs** |    **354.81 μs** |    **19.45 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,297.2 μs |    183.01 μs |    10.03 μs |  0.50 |    0.00 |        - |       - |   38068 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,688.4 μs** |  **1,403.28 μs** |    **76.92 μs** |  **1.00** |    **0.01** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,132.2 μs |  2,179.80 μs |   119.48 μs |  0.78 |    0.01 |  15.6250 |       - |  401521 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **122.1 μs** |     **68.76 μs** |     **3.77 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    117.0 μs |    253.65 μs |    13.90 μs |  0.96 |    0.10 |        - |       - |    4375 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,398.6 μs** |  **1,387.38 μs** |    **76.05 μs** |  **1.00** |    **0.07** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,184.2 μs |  1,186.78 μs |    65.05 μs |  0.85 |    0.06 |        - |       - |   42718 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **967.3 μs** |    **199.87 μs** |    **10.96 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125331 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    795.4 μs |    569.88 μs |    31.24 μs |  0.82 |    0.03 |        - |       - |    8632 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,569.9 μs** | **37,222.36 μs** | **2,040.28 μs** |  **1.05** |    **0.33** |  **74.2188** |       **-** | **1253912 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,118.4 μs |  8,027.30 μs |   440.00 μs |  0.87 |    0.21 |        - |       - |   78692 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,390.8 μs** |     **14.00 μs** |     **0.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,395.5 μs |    409.08 μs |    22.42 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,397.0 μs** |    **106.97 μs** |     **5.86 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,421.0 μs |    485.80 μs |    26.63 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,393.5 μs** |     **16.27 μs** |     **0.89 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,410.5 μs |    131.66 μs |     7.22 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,397.1 μs** |    **169.57 μs** |     **9.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,374.4 μs |    153.31 μs |     8.40 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **136.5 μs** |    **619.7 μs** |  **33.97 μs** |  **1.05** |    **0.35** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   169.9 μs |    399.2 μs |  21.88 μs |  1.31 |    0.36 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.0 μs** |    **944.5 μs** |  **51.77 μs** |  **1.07** |    **0.42** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   275.1 μs |  1,384.8 μs |  75.90 μs |  1.94 |    0.69 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **870.6 μs** |  **4,034.0 μs** | **221.12 μs** |  **1.04** |    **0.31** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,103.4 μs |    247.5 μs |  13.57 μs |  1.32 |    0.25 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,677.6 μs** | **13,447.1 μs** | **737.08 μs** |  **1.14** |    **0.63** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,458.6 μs |    375.9 μs |  20.61 μs |  0.99 |    0.37 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **813.0 ns** | **2,569.7 ns** | **140.85 ns** |  **1.02** |    **0.21** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,909.5 ns |   872.2 ns |  47.81 ns |  2.39 |    0.34 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,430.0 ns** | **2,153.6 ns** | **118.04 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,782.2 ns | 9,854.8 ns | 540.17 ns |  2.66 |    0.38 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.612 μs | 56.185 μs | 3.0797 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 14.034 μs | 37.603 μs | 2.0612 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 13.490 μs | 17.364 μs | 0.9518 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 13.692 μs |  8.603 μs | 0.4716 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.093 μs | 15.493 μs | 0.8492 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.424 μs | 17.207 μs | 0.9432 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 22.508 μs | 41.121 μs | 2.2540 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 31.983 μs | 77.115 μs | 4.2270 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.928 μs |  3.460 μs | 0.1897 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.534 μs | 13.734 μs | 0.7528 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.790 μs | 30.068 μs | 1.6482 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.368 μs | 39.351 μs | 2.1569 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.347 μs |  6.516 μs | 0.3572 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.025 μs | 21.106 μs | 1.1569 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,253.63 ns | 259.581 ns | 14.228 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     14.30 ns |   1.658 ns |  0.091 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.11 ns |   0.571 ns |  0.031 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     35.92 ns |   0.258 ns |  0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     41.81 ns |  17.091 ns |  0.937 ns |     ? |       ? | 0.0089 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     10.67 ns |   0.715 ns |  0.039 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    143.74 ns |  54.676 ns |  2.997 ns |  1.00 |    0.03 | 0.0355 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     82.74 ns |   0.542 ns |  0.030 ns |  0.58 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error        | StdDev      | Allocated |
|------------------------ |-----------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  14.273 μs |    22.087 μs |   1.2107 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 628.112 μs | 3,500.600 μs | 191.8797 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.353 μs |    12.939 μs |   0.7092 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 198.165 μs |   172.739 μs |   9.4684 μs |      80 B |


---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Unit tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*