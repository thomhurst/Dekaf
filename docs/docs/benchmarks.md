---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 00:31 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,272.4 μs** |    **109.31 μs** |     **5.99 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,873.0 μs |  2,655.75 μs |   145.57 μs |  0.30 |    0.02 |        - |       - |   35229 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,395.4 μs** |    **591.74 μs** |    **32.44 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  4,003.5 μs |  3,926.47 μs |   215.22 μs |  0.54 |    0.03 |  15.6250 |       - |  348180 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,208.3 μs** |    **815.76 μs** |    **44.71 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,740.5 μs |  1,322.07 μs |    72.47 μs |  0.28 |    0.01 |        - |       - |   37991 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,969.1 μs** |  **5,374.03 μs** |   **294.57 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1984448 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,936.4 μs | 49,172.80 μs | 2,695.33 μs |  1.00 |    0.18 |        - |       - |  369614 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.9 μs** |      **9.79 μs** |     **0.54 μs** |  **1.00** |    **0.00** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    146.3 μs |     76.99 μs |     4.22 μs |  1.07 |    0.03 |        - |       - |    4233 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,357.5 μs** |    **530.85 μs** |    **29.10 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,356.8 μs |    254.43 μs |    13.95 μs |  1.00 |    0.02 |        - |       - |   42613 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,076.9 μs** |     **54.55 μs** |     **2.99 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125485 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    913.7 μs |    192.35 μs |    10.54 μs |  0.85 |    0.01 |        - |       - |    7000 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,804.3 μs** | **31,706.24 μs** | **1,737.93 μs** |  **1.02** |    **0.24** |  **74.2188** |       **-** | **1255628 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,218.4 μs | 18,314.26 μs | 1,003.87 μs |  0.86 |    0.17 |        - |       - |   79740 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,485.5 μs** |    **152.40 μs** |     **8.35 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,506.3 μs |     45.26 μs |     2.48 μs |  0.27 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,491.1 μs** |    **312.10 μs** |    **17.11 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,497.0 μs |     94.61 μs |     5.19 μs |  0.27 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,499.8 μs** |     **71.56 μs** |     **3.92 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,537.2 μs |     67.86 μs |     3.72 μs |  0.28 |    0.00 |        - |       - |     832 B |        0.40 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,514.6 μs** |    **225.09 μs** |    **12.34 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,535.8 μs |    104.79 μs |     5.74 μs |  0.28 |    0.00 |        - |       - |     832 B |        0.40 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **185.5 μs** |    **449.6 μs** |  **24.65 μs** |   **198.0 μs** |  **1.01** |    **0.17** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   197.5 μs |    655.8 μs |  35.95 μs |   215.8 μs |  1.08 |    0.22 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **165.8 μs** |    **765.4 μs** |  **41.95 μs** |   **184.1 μs** |  **1.05** |    **0.36** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   186.6 μs |    270.1 μs |  14.81 μs |   184.9 μs |  1.18 |    0.31 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **959.7 μs** |  **3,080.5 μs** | **168.85 μs** |   **863.4 μs** |  **1.02** |    **0.21** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,263.0 μs |  2,732.3 μs | 149.77 μs | 1,209.9 μs |  1.34 |    0.23 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,457.1 μs** | **11,648.1 μs** | **638.47 μs** | **1,097.7 μs** |  **1.11** |    **0.55** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,823.8 μs | 11,396.1 μs | 624.66 μs | 1,496.9 μs |  1.39 |    0.60 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **856.4 ns** | **1,173.1 ns** |  **64.30 ns** |  **1.00** |    **0.09** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,946.5 ns |   453.6 ns |  24.87 ns |  2.28 |    0.15 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,439.1 ns** | **1,840.4 ns** | **100.88 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,263.1 ns | 3,386.7 ns | 185.64 ns |  2.27 |    0.17 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.767 μs |   1.388 μs |  0.0761 μs | 26.800 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.981 μs |   4.322 μs |  0.2369 μs | 10.951 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.563 μs |   3.119 μs |  0.1710 μs |  8.657 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.935 μs |  46.405 μs |  2.5436 μs |  8.655 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.187 μs |   6.355 μs |  0.3483 μs | 11.060 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.776 μs |   1.242 μs |  0.0681 μs | 12.800 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.082 μs |   7.121 μs |  0.3903 μs | 12.925 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.147 μs |   4.116 μs |  0.2256 μs | 27.241 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.033 μs |   2.034 μs |  0.1115 μs |  9.076 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.287 μs |   2.432 μs |  0.1333 μs | 20.253 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 37.520 μs | 185.107 μs | 10.1463 μs | 34.154 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 43.661 μs | 344.707 μs | 18.8946 μs | 34.014 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.302 μs |   3.276 μs |  0.1796 μs |  4.209 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.618 μs |   4.413 μs |  0.2419 μs | 10.555 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,283.30 ns | 1,497.178 ns | 82.065 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |     0.565 ns |  0.031 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.23 ns |     1.587 ns |  0.087 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.58 ns |     1.734 ns |  0.095 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.18 ns |     3.089 ns |  0.169 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.77 ns |     6.370 ns |  0.349 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    126.26 ns |   156.528 ns |  8.580 ns |  1.00 |    0.08 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.45 ns |     2.337 ns |  0.128 ns |  0.62 |    0.03 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.294 μs |   3.575 μs |  0.1960 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 532.174 μs | 184.073 μs | 10.0897 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.010 μs |  40.365 μs |  2.2125 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 233.858 μs | 128.503 μs |  7.0437 μs |      80 B |


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