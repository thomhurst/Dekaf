---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-27 14:23 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,959.7 μs** |  **75.79 μs** |  **50.13 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,570.8 μs |  31.01 μs |  18.45 μs |  0.43 |    0.00 |        - |       - |    5576 B |        0.05 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,114.3 μs** |  **71.30 μs** |  **47.16 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,724.1 μs |  26.02 μs |  13.61 μs |  0.52 |    0.00 |        - |       - |   51818 B |        0.05 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,656.7 μs** |  **17.68 μs** |  **11.69 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194772 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,841.8 μs |  57.04 μs |  37.73 μs |  0.43 |    0.01 |        - |       - |    6284 B |        0.03 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **10,906.3 μs** | **348.31 μs** | **230.39 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,157.2 μs | 659.59 μs | 436.28 μs |  1.12 |    0.04 |        - |       - |  349730 B |        0.18 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **112.9 μs** |   **4.18 μs** |   **2.49 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    115.2 μs |  12.27 μs |   7.30 μs |  1.02 |    0.06 |        - |       - |     167 B |       0.005 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,120.0 μs** |  **62.37 μs** |  **37.12 μs** |  **1.00** |    **0.04** |  **17.5781** |       **-** |  **304000 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,370.1 μs | 148.81 μs |  98.43 μs |  1.22 |    0.09 |        - |       - |    4575 B |        0.02 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **926.0 μs** |  **13.60 μs** |   **7.11 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121419 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    967.0 μs |  53.97 μs |  32.12 μs |  1.04 |    0.03 |        - |       - |    2015 B |        0.02 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,298.9 μs** | **115.00 μs** |  **68.44 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1217476 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,324.3 μs | 414.77 μs | 246.82 μs |  1.00 |    0.03 |        - |       - |   18561 B |        0.02 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,468.6 μs** |  **26.20 μs** |  **17.33 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,506.9 μs |  23.50 μs |  15.55 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.54 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,473.4 μs** |  **36.80 μs** |  **21.90 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,502.8 μs |   9.35 μs |   6.19 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.54 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,485.1 μs** |  **22.17 μs** |  **14.67 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,495.0 μs |   8.26 μs |   5.46 μs |  0.45 |    0.00 |        - |       - |     648 B |        0.31 |
|                         |               |             |           |             |           |           |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,474.7 μs** |  **38.53 μs** |  **25.49 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,520.2 μs |  21.56 μs |  14.26 μs |  0.46 |    0.00 |        - |       - |     648 B |        0.31 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **114.4 μs** |    **49.70 μs** |  **25.99 μs** |   **112.5 μs** |  **1.05** |    **0.32** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   167.7 μs |    37.99 μs |  19.87 μs |   164.2 μs |  1.54 |    0.38 |   40.18 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **112.4 μs** |    **24.13 μs** |  **10.71 μs** |   **107.6 μs** |  **1.01** |    **0.12** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   223.8 μs |    72.79 μs |  38.07 μs |   221.9 μs |  2.01 |    0.36 |  215.96 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **968.2 μs** |   **556.43 μs** | **291.02 μs** |   **881.0 μs** |  **1.07** |    **0.41** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,249.9 μs |   313.73 μs | 139.30 μs | 1,243.7 μs |  1.39 |    0.38 |  476.85 KB |        0.74 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,322.1 μs** | **1,062.30 μs** | **555.60 μs** |   **989.8 μs** |  **1.12** |    **0.57** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,203.0 μs | 1,691.89 μs | 751.21 μs | 2,377.7 μs |  1.87 |    0.83 | 2234.66 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **671.6 ns** |    **63.01 ns** |  **32.96 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,803.8 ns |   198.87 ns | 131.54 ns |  2.69 |    0.23 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,307.2 ns** |   **162.68 ns** | **107.60 ns** |  **1.01** |    **0.11** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,479.6 ns | 1,160.26 ns | 767.44 ns |  2.68 |    0.60 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error    | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|---------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 545.18 ns | 3.207 ns | 0.496 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.07 ns | 0.231 ns | 0.036 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.68 ns | 0.369 ns | 0.096 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.31 ns | 0.094 ns | 0.024 ns |      - |         - |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.999 μs** | **0.0041 μs** | **0.0006 μs** |         **-** |
| **WriteRequest** | **1**       | **2.003 μs** | **0.0049 μs** | **0.0008 μs** |         **-** |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **1.898 μs** | **0.0035 μs** | **0.0005 μs** |         **-** |
| **WriteRequest** | **9**       | **1.903 μs** | **0.0138 μs** | **0.0021 μs** |         **-** |
| **WriteRequest** | **10**      | **1.905 μs** | **0.0042 μs** | **0.0006 μs** |         **-** |
| **WriteRequest** | **11**      | **1.899 μs** | **0.0022 μs** | **0.0003 μs** |         **-** |


| Method                   | Version | Mean     | Error    | StdDev   | Allocated |
|------------------------- |-------- |---------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **83.67 ns** | **0.876 ns** | **0.136 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 79.29 ns | 0.756 ns | 0.117 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **79.78 ns** | **0.872 ns** | **0.227 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      | 79.80 ns | 0.346 ns | 0.090 ns |         - |


| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,351.5 ns | 2.28 ns | 1.51 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,581.0 ns | 2.42 ns | 1.26 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 1,948.4 ns | 1.43 ns | 0.75 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 1,991.7 ns | 2.62 ns | 1.37 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,520.4 ns | 3.57 ns | 2.13 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,552.2 ns | 2.59 ns | 1.54 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,517.0 ns | 9.36 ns | 5.57 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,250.9 ns | 2.31 ns | 1.21 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              |   926.6 ns | 0.89 ns | 0.53 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,584.8 ns | 4.16 ns | 2.48 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   551.7 ns | 1.18 ns | 0.61 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   618.6 ns | 1.11 ns | 0.66 ns | 0.0019 |      40 B |
| &#39;Read RecordBatch (10 records)&#39;                 |   128.0 ns | 0.08 ns | 0.04 ns |      - |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |   900.6 ns | 2.05 ns | 1.22 ns |      - |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error       | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 10,385.622 ns | 138.8301 ns | 91.8275 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     12.003 ns |   0.1874 ns |  0.1240 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     15.685 ns |   0.1562 ns |  0.0930 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     29.473 ns |   0.6302 ns |  0.3296 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     43.025 ns |   1.8832 ns |  1.2456 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.201 ns |   0.1962 ns |  0.1298 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |             |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    116.703 ns |   7.1882 ns |  4.7546 ns |  1.00 |    0.05 | 0.0105 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     60.014 ns |   0.7335 ns |  0.4365 ns |  0.52 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev   | Gen0   | Allocated |
|------------------------ |-------------:|----------:|---------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     288.6 ns |   1.55 ns |  0.92 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,131.8 ns | 161.29 ns | 95.98 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     219.0 ns |   0.70 ns |  0.36 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 122,739.0 ns | 141.27 ns | 84.07 ns |      - |      80 B |


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