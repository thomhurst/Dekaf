---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-27 15:39 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,099.2 μs** |   **313.91 μs** |   **164.18 μs** |  **1.00** |    **0.04** |       **-** |  **105170 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,629.5 μs |    39.67 μs |    23.61 μs |  0.43 |    0.01 |       - |    5576 B |        0.05 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,453.5 μs** |    **91.72 μs** |    **60.67 μs** |  **1.00** |    **0.01** |       **-** | **1048372 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,815.9 μs |    70.85 μs |    42.16 μs |  0.51 |    0.01 |       - |   51523 B |        0.05 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,569.7 μs** |    **26.02 μs** |    **13.61 μs** |  **1.00** |    **0.00** |       **-** |  **194772 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,952.7 μs |   204.46 μs |   106.94 μs |  0.45 |    0.02 |       - |    7508 B |        0.04 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **8,166.4 μs** |    **83.60 μs** |    **43.72 μs** |  **1.00** |    **0.01** | **15.6250** | **1944375 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,706.2 μs | 4,672.89 μs | 2,780.76 μs |  1.56 |    0.32 |       - |  344594 B |        0.18 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.9 μs** |     **3.18 μs** |     **2.10 μs** |  **1.00** |    **0.02** |  **0.2441** |   **30400 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    107.7 μs |    33.54 μs |    22.19 μs |  0.86 |    0.17 |       - |     214 B |       0.007 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,290.1 μs** |    **62.32 μs** |    **41.22 μs** |  **1.00** |    **0.04** |  **1.9531** |  **304000 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,087.7 μs |   251.16 μs |   149.46 μs |  0.84 |    0.11 |       - |    2071 B |       0.007 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **696.7 μs** |    **84.88 μs** |    **50.51 μs** |  **1.00** |    **0.10** |  **1.2207** |  **120969 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    837.4 μs |   132.86 μs |    79.06 μs |  1.21 |    0.14 |       - |    1949 B |        0.02 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,157.5 μs** |   **586.17 μs** |   **348.82 μs** |  **1.00** |    **0.07** | **13.6719** | **1208353 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,019.7 μs | 2,752.67 μs | 1,820.72 μs |  1.26 |    0.25 |       - |   17768 B |        0.01 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,437.9 μs** |    **28.39 μs** |    **18.78 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,597.3 μs |   126.64 μs |    75.36 μs |  0.48 |    0.01 |       - |     648 B |        0.54 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,469.2 μs** |   **124.37 μs** |    **74.01 μs** |  **1.00** |    **0.02** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,556.5 μs |    14.27 μs |     8.49 μs |  0.47 |    0.01 |       - |     648 B |        0.54 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,451.1 μs** |    **25.59 μs** |    **15.23 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,547.8 μs |    24.17 μs |    12.64 μs |  0.47 |    0.00 |       - |     648 B |        0.31 |
|                         |               |             |           |             |             |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,492.2 μs** |   **152.38 μs** |    **79.70 μs** |  **1.00** |    **0.02** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,576.1 μs |    36.39 μs |    19.03 μs |  0.47 |    0.01 |       - |     649 B |        0.31 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **139.5 μs** |    **61.59 μs** |  **27.35 μs** |  **1.03** |    **0.24** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   175.0 μs |    35.35 μs |  18.49 μs |  1.29 |    0.23 |   40.18 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **183.4 μs** |    **59.57 μs** |  **31.16 μs** |  **1.02** |    **0.22** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   211.9 μs |    42.01 μs |  21.97 μs |  1.18 |    0.21 |  215.96 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,306.5 μs** |   **629.60 μs** | **329.29 μs** |  **1.05** |    **0.34** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,290.4 μs |   292.67 μs | 129.95 μs |  1.04 |    0.24 |  476.85 KB |        0.74 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,346.2 μs** |    **68.19 μs** |  **24.32 μs** |  **1.00** |    **0.02** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,236.4 μs | 1,576.47 μs | 699.96 μs |  1.66 |    0.49 | 2234.66 KB |        0.93 |


| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **951.7 ns** |  **88.77 ns** |  **52.82 ns** |  **1.00** |    **0.08** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,866.7 ns | 312.36 ns | 206.60 ns |  1.97 |    0.23 |     452 B |        0.70 |
|                      |             |            |           |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,615.0 ns** | **142.80 ns** |  **94.46 ns** |  **1.00** |    **0.08** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,256.5 ns | 519.06 ns | 308.88 ns |  2.02 |    0.21 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 553.75 ns | 21.235 ns | 3.286 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.33 ns |  0.414 ns | 0.107 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.42 ns |  0.103 ns | 0.027 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.21 ns |  0.131 ns | 0.020 ns |      - |         - |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.967 μs** | **0.0165 μs** | **0.0043 μs** |         **-** |
| **WriteRequest** | **1**       | **2.001 μs** | **0.0036 μs** | **0.0006 μs** |         **-** |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.446 μs** | **0.0059 μs** | **0.0015 μs** |         **-** |
| **WriteRequest** | **9**       | **2.474 μs** | **0.0134 μs** | **0.0035 μs** |         **-** |
| **WriteRequest** | **10**      | **2.452 μs** | **0.0061 μs** | **0.0016 μs** |         **-** |
| **WriteRequest** | **11**      | **2.464 μs** | **0.0046 μs** | **0.0007 μs** |         **-** |


| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **108.95 ns** | **0.269 ns** | **0.042 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 101.72 ns | 0.347 ns | 0.054 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **102.17 ns** | **0.405 ns** | **0.063 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  91.17 ns | 0.822 ns | 0.127 ns |         - |


| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,742.1 ns | 7.57 ns | 5.01 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,071.9 ns | 2.23 ns | 1.33 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,417.2 ns | 3.51 ns | 2.09 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,389.1 ns | 6.27 ns | 4.14 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,175.4 ns | 5.25 ns | 3.12 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 4,056.1 ns | 2.24 ns | 1.33 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,956.8 ns | 5.09 ns | 3.03 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,895.8 ns | 5.80 ns | 3.04 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.0 ns | 0.49 ns | 0.33 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,041.6 ns | 1.85 ns | 1.10 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   709.2 ns | 1.76 ns | 1.05 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   789.1 ns | 4.08 ns | 2.70 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   162.7 ns | 0.07 ns | 0.04 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,699.2 ns | 3.68 ns | 2.19 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,185.3 ns | 1.13 ns | 0.59 ns |      - |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,318.065 ns | 6.2275 ns | 3.2571 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    12.603 ns | 0.2126 ns | 0.1406 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    13.874 ns | 0.1002 ns | 0.0524 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    24.684 ns | 0.2077 ns | 0.1236 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    30.756 ns | 1.1078 ns | 0.6593 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     7.124 ns | 0.0241 ns | 0.0159 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    96.405 ns | 1.8011 ns | 1.1913 ns |  1.00 |    0.02 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    50.375 ns | 0.0773 ns | 0.0404 ns |  0.52 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     292.0 ns |   1.68 ns |   1.00 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,880.4 ns | 269.24 ns | 160.22 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.4 ns |   0.29 ns |   0.17 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,074.1 ns | 255.93 ns | 169.28 ns |      - |      80 B |


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