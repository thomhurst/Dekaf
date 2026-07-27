---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-27 13:13 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,165.5 μs** |   **109.99 μs** |  **72.75 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,719.2 μs |    25.58 μs |  16.92 μs |  0.44 |    0.01 |        - |       - |    5576 B |        0.05 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,423.2 μs** |    **62.14 μs** |  **41.10 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,800.9 μs |    49.80 μs |  32.94 μs |  0.51 |    0.01 |        - |       - |   51869 B |        0.05 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,259.5 μs** |    **55.90 μs** |  **33.26 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,687.3 μs |    73.41 μs |  48.56 μs |  0.43 |    0.01 |        - |       - |    6291 B |        0.03 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,599.9 μs** |   **229.06 μs** | **136.31 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,736.6 μs | 1,087.72 μs | 568.90 μs |  1.01 |    0.04 |  15.6250 |       - |  348426 B |        0.18 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.2 μs** |     **1.80 μs** |   **1.19 μs** |  **1.00** |    **0.01** |   **1.7090** |       **-** |   **30400 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    136.2 μs |    29.39 μs |  19.44 μs |  1.09 |    0.15 |        - |       - |     214 B |       0.007 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,290.3 μs** |    **26.51 μs** |  **17.53 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,452.5 μs |   158.03 μs | 104.53 μs |  1.13 |    0.08 |        - |       - |    4670 B |        0.02 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,084.7 μs** |    **10.54 μs** |   **5.51 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121580 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,041.1 μs |   133.57 μs |  88.35 μs |  0.96 |    0.08 |        - |       - |    2255 B |        0.02 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,600.0 μs** |    **80.07 μs** |  **41.88 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215601 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,475.2 μs |   356.89 μs | 236.06 μs |  0.99 |    0.02 |        - |       - |   18700 B |        0.02 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,553.4 μs** |    **34.69 μs** |  **22.95 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,613.2 μs |    34.88 μs |  23.07 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,509.7 μs** |    **17.05 μs** |  **10.14 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,601.1 μs |    10.34 μs |   6.84 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,510.4 μs** |    **15.55 μs** |  **10.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,609.3 μs |     8.60 μs |   5.69 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,510.1 μs** |     **8.83 μs** |   **5.26 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,605.5 μs |     6.92 μs |   4.12 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **112.7 μs** |    **35.14 μs** |  **15.60 μs** |  **1.02** |    **0.18** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   181.2 μs |    81.81 μs |  36.32 μs |  1.63 |    0.37 |   40.18 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **143.0 μs** |    **75.97 μs** |  **39.74 μs** |  **1.06** |    **0.36** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   219.0 μs |    75.16 μs |  33.37 μs |  1.62 |    0.41 |  215.96 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,042.9 μs** |   **555.26 μs** | **290.41 μs** |  **1.06** |    **0.37** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,493.0 μs |   565.93 μs | 251.28 μs |  1.52 |    0.41 |  476.85 KB |        0.74 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,482.0 μs** |   **893.79 μs** | **467.47 μs** |  **1.09** |    **0.45** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,417.1 μs | 1,424.52 μs | 632.50 μs |  1.77 |    0.67 | 2234.66 KB |        0.93 |


| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **870.8 ns** |  **81.34 ns** |  **48.41 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,516.7 ns | 517.41 ns | 342.23 ns |  2.90 |    0.40 |      - |     452 B |        0.70 |
|                      |             |            |           |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,478.5 ns** | **146.85 ns** |  **97.13 ns** |  **1.00** |    **0.09** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,856.6 ns | 774.74 ns | 512.44 ns |  2.62 |    0.37 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 422.11 ns | 13.265 ns | 2.053 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  15.93 ns |  0.479 ns | 0.074 ns |      - |         - |
| WriteDescribeGroupsV6      |  28.06 ns |  1.506 ns | 0.391 ns |      - |         - |
| WriteListConfigResourcesV1 |  14.90 ns |  0.600 ns | 0.156 ns |      - |         - |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.453 μs** | **0.0567 μs** | **0.0147 μs** |         **-** |
| **WriteRequest** | **1**       | **1.453 μs** | **0.0342 μs** | **0.0089 μs** |         **-** |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.377 μs** | **0.0054 μs** | **0.0008 μs** |         **-** |
| **WriteRequest** | **9**       | **2.402 μs** | **0.0072 μs** | **0.0019 μs** |         **-** |
| **WriteRequest** | **10**      | **2.401 μs** | **0.0207 μs** | **0.0032 μs** |         **-** |
| **WriteRequest** | **11**      | **2.402 μs** | **0.0208 μs** | **0.0032 μs** |         **-** |


| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.57 ns** | **0.241 ns** | **0.063 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  96.51 ns | 0.271 ns | 0.070 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **93.15 ns** | **1.054 ns** | **0.163 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  93.01 ns | 0.300 ns | 0.078 ns |         - |


| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,637.2 ns | 1.76 ns | 1.16 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,117.8 ns | 1.32 ns | 0.78 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,307.4 ns | 2.35 ns | 1.56 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,267.4 ns | 3.03 ns | 1.58 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,116.1 ns | 1.94 ns | 1.15 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,980.2 ns | 4.67 ns | 3.09 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,988.5 ns | 3.93 ns | 2.60 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,854.2 ns | 3.33 ns | 2.20 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,119.0 ns | 3.24 ns | 1.93 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,815.2 ns | 0.85 ns | 0.45 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   701.2 ns | 1.94 ns | 1.28 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   841.4 ns | 1.65 ns | 0.98 ns | 0.0019 |      40 B |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.1 ns | 0.33 ns | 0.19 ns |      - |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,298.2 ns | 1.15 ns | 0.76 ns |      - |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,650.14 ns | 21.224 ns | 12.630 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.32 ns |  0.040 ns |  0.024 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.92 ns |  0.062 ns |  0.037 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.28 ns |  0.050 ns |  0.030 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.54 ns |  0.203 ns |  0.121 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.97 ns |  0.012 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    125.20 ns |  2.140 ns |  1.415 ns |  1.00 |    0.02 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.40 ns |  0.075 ns |  0.050 ns |  0.44 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean        | Error       | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|------------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    221.8 ns |     2.67 ns |   1.77 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 84,756.7 ns | 1,147.66 ns | 759.10 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    144.9 ns |     1.25 ns |   0.83 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 83,163.0 ns |   512.35 ns | 267.97 ns |      - |      80 B |


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