---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 14:50 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean       | Error        | StdDev      | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----------:|-------------:|------------:|------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,999.4 μs** |    **402.21 μs** |    **22.05 μs** |  **1.00** |    **0.00** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,804.9 μs |  2,407.79 μs |   131.98 μs |  0.30 |    0.02 |       - |   35192 B |        0.32 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,323.5 μs** |    **611.52 μs** |    **33.52 μs** |  **1.00** |    **0.01** |       **-** | **1088292 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,366.4 μs |  7,222.56 μs |   395.89 μs |  0.46 |    0.05 |       - |  345804 B |        0.32 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,341.7 μs** |    **507.00 μs** |    **27.79 μs** |  **1.00** |    **0.01** |       **-** |  **198690 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 2,146.7 μs |  2,137.72 μs |   117.18 μs |  0.34 |    0.02 |       - |   36042 B |        0.18 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,161.5 μs** |  **9,809.53 μs** |   **537.69 μs** |  **1.00** |    **0.08** | **15.6250** | **1984295 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 6,872.1 μs | 13,217.17 μs |   724.48 μs |  0.84 |    0.09 |       - |  368959 B |        0.19 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **123.6 μs** |     **29.39 μs** |     **1.61 μs** |  **1.00** |    **0.02** |  **0.2441** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   124.0 μs |     54.83 μs |     3.01 μs |  1.00 |    0.02 |       - |    4152 B |        0.12 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,230.0 μs** |    **460.01 μs** |    **25.21 μs** |  **1.00** |    **0.03** |  **3.9063** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      | 2,260.3 μs | 19,646.19 μs | 1,076.87 μs |  1.84 |    0.76 |       - |   43964 B |        0.13 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **618.5 μs** |  **1,046.62 μs** |    **57.37 μs** |  **1.01** |    **0.11** |  **1.4648** |  **124701 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   635.4 μs |    798.64 μs |    43.78 μs |  1.03 |    0.10 |       - |    6468 B |        0.05 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,159.4 μs** | **21,512.81 μs** | **1,179.19 μs** |  **1.02** |    **0.22** | **13.6719** | **1248407 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 7,831.0 μs | 30,958.90 μs | 1,696.96 μs |  1.12 |    0.27 |       - |   77198 B |        0.06 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,312.3 μs** |    **142.80 μs** |     **7.83 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,388.1 μs |    497.61 μs |    27.28 μs |  0.26 |    0.00 |       - |     800 B |        0.67 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,350.4 μs** |  **1,506.51 μs** |    **82.58 μs** |  **1.00** |    **0.02** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,439.4 μs |  1,704.49 μs |    93.43 μs |  0.27 |    0.02 |       - |     800 B |        0.67 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,469.2 μs** |  **4,695.73 μs** |   **257.39 μs** |  **1.00** |    **0.06** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,384.0 μs |    186.80 μs |    10.24 μs |  0.25 |    0.01 |       - |     800 B |        0.38 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,299.4 μs** |    **117.94 μs** |     **6.46 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,404.9 μs |     33.93 μs |     1.86 μs |  0.27 |    0.00 |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **150.0 μs** |   **564.8 μs** |  **30.96 μs** |  **1.03** |    **0.25** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   147.2 μs |   477.0 μs |  26.15 μs |  1.01 |    0.22 |   39.98 KB |        0.62 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **166.4 μs** |   **621.5 μs** |  **34.07 μs** |  **1.03** |    **0.25** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   165.0 μs |   315.7 μs |  17.30 μs |  1.02 |    0.19 |  215.77 KB |        0.90 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,091.2 μs** | **1,015.7 μs** |  **55.68 μs** |  **1.00** |    **0.06** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   995.0 μs |   142.6 μs |   7.82 μs |  0.91 |    0.04 |  476.66 KB |        0.73 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,579.7 μs** | **8,245.3 μs** | **451.95 μs** |  **1.05** |    **0.35** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,225.4 μs |   749.6 μs |  41.09 μs |  0.82 |    0.18 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **952.3 ns** | **1,968.8 ns** | **107.92 ns** |  **1.01** |    **0.14** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,671.3 ns | 1,444.2 ns |  79.16 ns |  1.77 |    0.19 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,400.1 ns** | **1,087.7 ns** |  **59.62 ns** |  **1.00** |    **0.05** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,270.4 ns | 1,075.7 ns |  58.96 ns |  1.62 |    0.07 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.486 μs |   6.1355 μs |  0.3363 μs | 26.469 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.178 μs |   0.7291 μs |  0.0400 μs | 11.171 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.813 μs |   3.2945 μs |  0.1806 μs |  8.806 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.632 μs |   2.9030 μs |  0.1591 μs |  8.545 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.385 μs |   3.6228 μs |  0.1986 μs | 11.302 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 14.691 μs |  61.4134 μs |  3.3663 μs | 13.125 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.575 μs |   3.9606 μs |  0.2171 μs | 13.545 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.986 μs |   1.6820 μs |  0.0922 μs | 26.965 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.939 μs |   0.5865 μs |  0.0321 μs |  8.926 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.095 μs |  77.4736 μs |  4.2466 μs | 27.302 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 41.806 μs | 370.6500 μs | 20.3166 μs | 30.738 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 41.030 μs |  92.9494 μs |  5.0949 μs | 38.992 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.761 μs |   3.4180 μs |  0.1874 μs |  5.792 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.509 μs |   3.8457 μs |  0.2108 μs | 13.436 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,305.15 ns | 799.134 ns | 43.803 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.61 ns |   2.625 ns |  0.144 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.21 ns |   0.157 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.73 ns |   3.105 ns |  0.170 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.16 ns |   7.364 ns |  0.404 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |   0.529 ns |  0.029 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    114.36 ns |  43.083 ns |  2.361 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.90 ns |   0.197 ns |  0.011 ns |  0.67 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.268 μs |   8.579 μs |  0.4703 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 511.486 μs | 361.532 μs | 19.8168 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.335 μs |  20.212 μs |  1.1079 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 263.455 μs | 638.982 μs | 35.0247 μs |      80 B |


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