---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-27 17:14 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,381.0 μs** |   **149.32 μs** |  **98.76 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,710.8 μs |    45.76 μs |  30.27 μs |  0.42 |    0.01 |        - |       - |    5576 B |        0.05 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,388.1 μs** |    **84.62 μs** |  **55.97 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,804.8 μs |    90.52 μs |  59.87 μs |  0.52 |    0.01 |        - |       - |   51764 B |        0.05 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,450.6 μs** |   **209.90 μs** | **124.91 μs** |  **1.00** |    **0.03** |   **7.8125** |       **-** |  **194772 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,675.5 μs |    32.58 μs |  17.04 μs |  0.41 |    0.01 |        - |       - |    6304 B |        0.03 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,470.4 μs** |   **254.32 μs** | **168.22 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,909.4 μs | 1,171.11 μs | 774.61 μs |  1.04 |    0.06 |        - |       - |  342932 B |        0.18 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **140.8 μs** |    **14.70 μs** |   **9.72 μs** |  **1.00** |    **0.09** |   **1.7090** |       **-** |   **30400 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    147.6 μs |     7.82 μs |   4.65 μs |  1.05 |    0.08 |        - |       - |     213 B |       0.007 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,311.1 μs** |    **27.04 μs** |  **17.89 μs** |  **1.00** |    **0.02** |  **17.5781** |       **-** |  **304000 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,339.7 μs |   197.08 μs | 130.36 μs |  1.02 |    0.10 |        - |       - |    2087 B |       0.007 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,055.1 μs** |     **5.75 μs** |   **3.42 μs** |  **1.00** |    **0.00** |   **7.0801** |       **-** |  **121534 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,058.5 μs |    63.87 μs |  42.25 μs |  1.00 |    0.04 |        - |       - |    2043 B |        0.02 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,535.3 μs** |   **317.01 μs** | **209.68 μs** |  **1.00** |    **0.03** |  **70.3125** |       **-** | **1214808 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,995.7 μs |   520.16 μs | 309.54 μs |  0.95 |    0.03 |        - |       - |   18616 B |        0.02 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,496.6 μs** |    **21.48 μs** |  **14.21 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,631.6 μs |    15.93 μs |  10.53 μs |  0.48 |    0.00 |        - |       - |     648 B |        0.54 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,618.3 μs** |    **96.39 μs** |  **50.41 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,614.0 μs |    27.46 μs |  16.34 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,519.0 μs** |    **55.12 μs** |  **36.46 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,574.2 μs |    16.39 μs |  10.84 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 |
|                         |               |             |           |             |             |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,481.2 μs** |    **24.65 μs** |  **16.30 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,565.3 μs |     5.73 μs |   3.41 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **123.7 μs** |    **66.18 μs** |    **34.61 μs** |  **1.06** |    **0.37** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   171.0 μs |    27.47 μs |    12.20 μs |  1.47 |    0.34 |   40.18 KB |        0.62 |
|                      |              |             |            |             |             |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **148.3 μs** |    **58.04 μs** |    **25.77 μs** |  **1.03** |    **0.24** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   242.1 μs |    61.97 μs |    32.41 μs |  1.68 |    0.34 |  215.96 KB |        0.90 |
|                      |              |             |            |             |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **877.0 μs** |    **43.64 μs** |    **15.56 μs** |  **1.00** |    **0.02** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,478.8 μs |   361.31 μs |   160.42 μs |  1.69 |    0.17 |  476.85 KB |        0.74 |
|                      |              |             |            |             |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,064.9 μs** |    **33.92 μs** |    **12.10 μs** |  **1.00** |    **0.01** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,806.6 μs | 2,526.54 μs | 1,321.43 μs |  2.64 |    1.17 | 2234.66 KB |        0.93 |


| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **837.3 ns** |  **69.23 ns** |  **41.20 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,161.1 ns | 359.70 ns | 237.92 ns |  2.59 |    0.29 |      - |     452 B |        0.70 |
|                      |             |            |           |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,425.0 ns** |  **83.73 ns** |  **49.83 ns** |  **1.00** |    **0.05** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,691.1 ns | 659.63 ns | 436.30 ns |  2.59 |    0.30 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 378.77 ns | 30.071 ns | 7.809 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  15.96 ns |  1.018 ns | 0.264 ns |      - |         - |
| WriteDescribeGroupsV6      |  27.65 ns |  0.381 ns | 0.059 ns |      - |         - |
| WriteListConfigResourcesV1 |  14.64 ns |  0.146 ns | 0.023 ns |      - |         - |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.485 μs** | **0.1013 μs** | **0.0263 μs** |         **-** |
| **WriteRequest** | **1**       | **1.444 μs** | **0.0127 μs** | **0.0033 μs** |         **-** |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.900 μs** | **0.0081 μs** | **0.0012 μs** |         **-** |
| **WriteRequest** | **9**       | **1.899 μs** | **0.0026 μs** | **0.0007 μs** |         **-** |
| **WriteRequest** | **10**      | **1.903 μs** | **0.0022 μs** | **0.0003 μs** |         **-** |
| **WriteRequest** | **11**      | **1.903 μs** | **0.0073 μs** | **0.0011 μs** |         **-** |


| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **87.07 ns** | **0.206 ns** | **0.032 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 75.79 ns | 0.505 ns | 0.131 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **74.27 ns** | **1.097 ns** | **0.285 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 70.69 ns | 0.494 ns | 0.076 ns |         - |


| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,350.1 ns | 2.20 ns | 1.46 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,580.0 ns | 2.25 ns | 1.18 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,952.7 ns | 1.06 ns | 0.63 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,935.8 ns | 1.26 ns | 0.75 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,674.0 ns | 1.85 ns | 1.10 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,601.1 ns | 1.82 ns | 1.21 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,596.3 ns | 3.02 ns | 1.80 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,248.5 ns | 3.28 ns | 2.17 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   925.8 ns | 1.00 ns | 0.59 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,582.5 ns | 1.91 ns | 1.00 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   557.6 ns | 1.19 ns | 0.71 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   666.9 ns | 1.16 ns | 0.76 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   129.8 ns | 0.17 ns | 0.11 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,285.8 ns | 2.38 ns | 1.42 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   883.9 ns | 0.80 ns | 0.47 ns |      - |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,575.93 ns | 34.621 ns | 20.603 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.20 ns |  0.029 ns |  0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.89 ns |  0.017 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.64 ns |  0.015 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.13 ns |  0.345 ns |  0.205 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.94 ns |  0.009 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    104.39 ns |  0.380 ns |  0.251 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.00 ns |  0.040 ns |  0.024 ns |  0.53 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean        | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    216.4 ns |   1.73 ns |   1.03 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 84,304.0 ns | 364.22 ns | 240.91 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    143.3 ns |   0.81 ns |   0.53 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 82,682.9 ns | 447.05 ns | 295.70 ns |      - |      80 B |


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