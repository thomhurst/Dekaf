---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 23:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,265.7 μs** |    **222.58 μs** |    **12.20 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,780.1 μs |    810.23 μs |    44.41 μs |  0.44 |    0.01 |        - |       - |   35184 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,424.2 μs** |    **813.12 μs** |    **44.57 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,931.4 μs |  1,052.96 μs |    57.72 μs |  0.53 |    0.01 |  15.6250 |       - |  347439 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,164.8 μs** |    **158.96 μs** |     **8.71 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,438.1 μs |  8,227.09 μs |   450.95 μs |  0.56 |    0.06 |        - |       - |   37577 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,857.5 μs** |  **5,008.95 μs** |   **274.56 μs** |  **1.00** |    **0.03** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,324.6 μs | 19,827.00 μs | 1,086.78 μs |  1.04 |    0.08 |        - |       - |  470722 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.3 μs** |     **22.26 μs** |     **1.22 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    134.4 μs |    207.67 μs |    11.38 μs |  1.02 |    0.08 |        - |       - |    4127 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,335.4 μs** |    **257.93 μs** |    **14.14 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,306.9 μs |  1,552.68 μs |    85.11 μs |  0.98 |    0.06 |        - |       - |   43320 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,097.7 μs** |     **44.55 μs** |     **2.44 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125520 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    946.8 μs |    487.69 μs |    26.73 μs |  0.86 |    0.02 |        - |       - |    6127 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,941.5 μs** | **29,081.75 μs** | **1,594.07 μs** |  **1.02** |    **0.21** |  **74.2188** |       **-** | **1255696 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,681.2 μs | 12,787.49 μs |   700.93 μs |  0.99 |    0.16 |        - |       - |   62658 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,500.0 μs** |     **46.08 μs** |     **2.53 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1394 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,608.3 μs |    111.18 μs |     6.09 μs |  0.47 |    0.00 |        - |       - |     792 B |        0.57 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,517.8 μs** |     **56.63 μs** |     **3.10 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,652.6 μs |    326.08 μs |    17.87 μs |  0.48 |    0.00 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,520.9 μs** |     **29.50 μs** |     **1.62 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,634.8 μs |     43.74 μs |     2.40 μs |  0.48 |    0.00 |        - |       - |     792 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,518.0 μs** |     **84.57 μs** |     **4.64 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,633.7 μs |     50.85 μs |     2.79 μs |  0.48 |    0.00 |        - |       - |     792 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **159.8 μs** |    **390.9 μs** |  **21.43 μs** |   **171.0 μs** |  **1.01** |    **0.17** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   180.8 μs |    342.9 μs |  18.79 μs |   177.1 μs |  1.15 |    0.18 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **150.5 μs** |    **600.6 μs** |  **32.92 μs** |   **142.0 μs** |  **1.03** |    **0.27** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   196.6 μs |    385.1 μs |  21.11 μs |   185.7 μs |  1.35 |    0.27 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,041.9 μs** |  **3,389.1 μs** | **185.77 μs** | **1,027.0 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,268.0 μs |    442.5 μs |  24.26 μs | 1,257.0 μs |  1.24 |    0.19 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,538.6 μs** | **13,794.7 μs** | **756.14 μs** | **1,103.6 μs** |  **1.14** |    **0.64** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,523.0 μs |    392.4 μs |  21.51 μs | 1,521.9 μs |  1.13 |    0.38 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **909.9 ns** | **2,099.5 ns** | **115.1 ns** |  **1.01** |    **0.15** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,126.3 ns | 2,826.2 ns | 154.9 ns |  2.36 |    0.28 |      - |     452 B |        0.70 |
|                      |             |            |            |          |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,511.3 ns** | **2,433.6 ns** | **133.4 ns** |  **1.01** |    **0.11** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,415.4 ns | 2,684.0 ns | 147.1 ns |  2.27 |    0.19 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.387 μs |   4.996 μs | 0.2738 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.950 μs |  11.226 μs | 0.6153 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.622 μs |  31.720 μs | 1.7387 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.401 μs |   8.868 μs | 0.4861 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.778 μs |   8.592 μs | 0.4709 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.250 μs |  19.091 μs | 1.0464 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 11.527 μs |  11.860 μs | 0.6501 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 41.715 μs | 180.043 μs | 9.8688 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.540 μs |  48.906 μs | 2.6807 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.482 μs | 110.850 μs | 6.0760 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.826 μs |  23.025 μs | 1.2621 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 25.616 μs |  41.357 μs | 2.2669 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.755 μs |   9.560 μs | 0.5240 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 15.934 μs |  29.587 μs | 1.6217 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,211.74 ns | 99.619 ns | 5.460 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.35 ns |  4.883 ns | 0.268 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.70 ns |  0.123 ns | 0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.85 ns |  3.051 ns | 0.167 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.75 ns |  3.933 ns | 0.216 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.21 ns |  1.464 ns | 0.080 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.84 ns | 66.018 ns | 3.619 ns |  1.00 |    0.04 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     78.99 ns |  0.673 ns | 0.037 ns |  0.71 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.746 μs |   6.053 μs | 0.3318 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 519.074 μs | 164.619 μs | 9.0233 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.641 μs |   3.725 μs | 0.2042 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 215.098 μs |  21.475 μs | 1.1771 μs |      80 B |


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