---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 13:43 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,026.59 μs** |  **1,825.02 μs** | **100.036 μs** |  **1.00** |    **0.02** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,573.05 μs |    490.20 μs |  26.870 μs |  0.43 |    0.01 |       - |   35160 B |        0.32 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,153.93 μs** |  **1,937.87 μs** | **106.221 μs** |  **1.00** |    **0.02** |       **-** | **1088292 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,816.38 μs |  5,210.00 μs | 285.577 μs |  0.53 |    0.04 |       - |  347134 B |        0.32 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,999.23 μs** |  **9,610.23 μs** | **526.769 μs** |  **1.00** |    **0.09** |       **-** |  **198690 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,046.79 μs |  5,003.48 μs | 274.258 μs |  0.44 |    0.05 |       - |   37712 B |        0.19 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **7,829.53 μs** |  **3,015.21 μs** | **165.274 μs** |  **1.00** |    **0.03** | **15.6250** | **1984295 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,757.36 μs |  6,957.39 μs | 381.358 μs |  1.37 |    0.05 |       - |  467611 B |        0.24 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **118.53 μs** |    **102.36 μs** |   **5.611 μs** |  **1.00** |    **0.06** |  **0.3662** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     90.58 μs |     60.68 μs |   3.326 μs |  0.77 |    0.04 |       - |    4293 B |        0.13 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,076.99 μs** |  **1,103.39 μs** |  **60.481 μs** |  **1.00** |    **0.07** |  **3.9063** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    928.54 μs |  2,895.17 μs | 158.694 μs |  0.86 |    0.13 |       - |   42072 B |        0.12 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **664.78 μs** |    **404.41 μs** |  **22.167 μs** |  **1.00** |    **0.04** |  **1.4648** |  **124793 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    695.44 μs |  2,239.38 μs | 122.748 μs |  1.05 |    0.16 |       - |    5638 B |        0.05 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,076.50 μs** | **11,320.59 μs** | **620.520 μs** |  **1.00** |    **0.11** | **13.6719** | **1247617 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,128.94 μs |  3,217.47 μs | 176.360 μs |  1.01 |    0.08 |       - |   57909 B |        0.05 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,288.49 μs** |     **85.59 μs** |   **4.691 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,460.78 μs |  1,984.54 μs | 108.779 μs |  0.47 |    0.02 |       - |     768 B |        0.64 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,279.94 μs** |    **153.23 μs** |   **8.399 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,558.82 μs |  4,184.50 μs | 229.366 μs |  0.48 |    0.04 |       - |     768 B |        0.64 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,360.62 μs** |    **840.41 μs** |  **46.066 μs** |  **1.00** |    **0.01** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,437.04 μs |    132.11 μs |   7.241 μs |  0.45 |    0.00 |       - |     768 B |        0.37 |
|                         |               |             |           |              |              |            |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,304.74 μs** |    **209.31 μs** |  **11.473 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,446.35 μs |    845.75 μs |  46.358 μs |  0.46 |    0.01 |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **133.2 μs** |   **541.02 μs** |  **29.66 μs** |   **122.8 μs** |  **1.03** |    **0.27** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   163.7 μs |   516.07 μs |  28.29 μs |   178.8 μs |  1.27 |    0.29 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **163.3 μs** |   **424.10 μs** |  **23.25 μs** |   **161.5 μs** |  **1.01** |    **0.18** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   138.6 μs |    54.69 μs |   3.00 μs |   138.8 μs |  0.86 |    0.11 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **921.8 μs** | **2,422.77 μs** | **132.80 μs** |   **850.2 μs** |  **1.01** |    **0.17** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   981.8 μs | 1,627.85 μs |  89.23 μs |   944.0 μs |  1.08 |    0.15 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,394.2 μs** | **8,900.51 μs** | **487.87 μs** | **1,115.3 μs** |  **1.07** |    **0.43** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,168.2 μs |   663.51 μs |  36.37 μs | 1,160.0 μs |  0.90 |    0.23 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **861.4 ns** |  **1,268.0 ns** |  **69.51 ns** |  **1.00** |    **0.10** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,675.2 ns |    268.3 ns |  14.71 ns |  1.95 |    0.14 |     452 B |        0.70 |
|                      |             |            |             |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,403.6 ns** |    **546.1 ns** |  **29.93 ns** |  **1.00** |    **0.03** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,506.0 ns | 10,250.0 ns | 561.84 ns |  2.50 |    0.35 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.779 μs |  12.527 μs |  0.6867 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.430 μs |  18.984 μs |  1.0406 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.844 μs |  26.523 μs |  1.4538 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.619 μs |  27.140 μs |  1.4876 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.176 μs |  18.921 μs |  1.0371 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.086 μs |  38.156 μs |  2.0915 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.780 μs |  24.971 μs |  1.3688 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.078 μs | 166.669 μs |  9.1357 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 14.403 μs |  66.152 μs |  3.6260 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.731 μs |  19.842 μs |  1.0876 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 25.802 μs |  84.651 μs |  4.6400 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.991 μs | 206.135 μs | 11.2989 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.435 μs |  36.135 μs |  1.9807 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 26.581 μs |  27.338 μs |  1.4985 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error         | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|--------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,686.066 ns | 1,244.6592 ns | 68.2240 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |               |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     13.639 ns |     0.9203 ns |  0.0504 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.072 ns |     3.6441 ns |  0.1997 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     32.063 ns |     3.1252 ns |  0.1713 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     46.284 ns |     3.4954 ns |  0.1916 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.512 ns |     0.5700 ns |  0.0312 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |               |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    158.181 ns |    46.0547 ns |  2.5244 ns |  1.00 |    0.02 | 0.0105 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     85.691 ns |    11.6816 ns |  0.6403 ns |  0.54 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error     | StdDev    | Allocated |
|------------------------ |----------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.56 μs |  43.28 μs |  2.373 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 454.13 μs | 140.93 μs |  7.725 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  11.84 μs |  51.25 μs |  2.809 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 193.37 μs | 254.32 μs | 13.940 μs |      80 B |


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