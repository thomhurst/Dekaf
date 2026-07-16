---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-16 07:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,275.0 μs** |    **539.66 μs** |    **29.58 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,759.7 μs |    499.53 μs |    27.38 μs |  0.44 |    0.00 |        - |       - |   35166 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,525.9 μs** |  **2,027.56 μs** |   **111.14 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,921.5 μs |  1,184.70 μs |    64.94 μs |  0.52 |    0.01 |  15.6250 |       - |  347211 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,296.1 μs** |    **445.06 μs** |    **24.40 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,805.7 μs |  1,354.25 μs |    74.23 μs |  0.45 |    0.01 |        - |       - |   37807 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,518.8 μs** |  **1,753.24 μs** |    **96.10 μs** |  **1.00** |    **0.01** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,259.1 μs | 21,163.96 μs | 1,160.07 μs |  0.98 |    0.08 |  15.6250 |       - |  478087 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **134.0 μs** |     **61.93 μs** |     **3.39 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    147.9 μs |    192.95 μs |    10.58 μs |  1.10 |    0.07 |        - |       - |    4163 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,346.2 μs** |    **181.84 μs** |     **9.97 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,402.9 μs |  4,119.41 μs |   225.80 μs |  1.04 |    0.15 |        - |       - |   44408 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,067.8 μs** |     **80.22 μs** |     **4.40 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125478 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    744.9 μs |    840.62 μs |    46.08 μs |  0.70 |    0.04 |        - |       - |    6831 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,812.7 μs** | **28,964.96 μs** | **1,587.67 μs** |  **1.02** |    **0.21** |  **74.2188** |       **-** | **1255422 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,509.2 μs |  6,827.97 μs |   374.26 μs |  0.78 |    0.13 |        - |       - |   79522 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,495.4 μs** |     **71.98 μs** |     **3.95 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,626.9 μs |    182.12 μs |     9.98 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,505.0 μs** |    **112.48 μs** |     **6.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,617.6 μs |    112.93 μs |     6.19 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,515.3 μs** |      **5.22 μs** |     **0.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,606.0 μs |     45.49 μs |     2.49 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,495.3 μs** |    **190.17 μs** |    **10.42 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,625.6 μs |    134.01 μs |     7.35 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **120.9 μs** |    **581.3 μs** |  **31.86 μs** |   **102.8 μs** |  **1.04** |    **0.32** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   201.6 μs |    805.2 μs |  44.13 μs |   217.7 μs |  1.74 |    0.48 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **139.1 μs** |    **654.6 μs** |  **35.88 μs** |   **119.9 μs** |  **1.04** |    **0.31** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   192.5 μs |    773.1 μs |  42.37 μs |   169.6 μs |  1.44 |    0.40 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,115.3 μs** |  **4,734.1 μs** | **259.49 μs** | **1,114.9 μs** |  **1.04** |    **0.30** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,592.9 μs | 10,662.4 μs | 584.44 μs | 1,286.2 μs |  1.48 |    0.57 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,445.9 μs** | **11,984.4 μs** | **656.91 μs** | **1,069.7 μs** |  **1.12** |    **0.58** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,239.9 μs | 12,474.6 μs | 683.78 μs | 2,626.7 μs |  1.74 |    0.72 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **914.4 ns** |   **842.5 ns** |  **46.18 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,304.2 ns | 2,792.1 ns | 153.04 ns |  2.52 |    0.18 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,494.7 ns** |   **698.4 ns** |  **38.28 ns** |  **1.00** |    **0.03** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,571.0 ns | 2,415.7 ns | 132.41 ns |  2.39 |    0.09 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.571 μs |  41.5902 μs |  2.2797 μs | 26.429 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.616 μs |   2.4603 μs |  0.1349 μs | 10.559 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 16.788 μs | 267.8854 μs | 14.6837 μs |  8.436 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.216 μs |   1.6009 μs |  0.0878 μs |  8.256 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.091 μs |   0.4827 μs |  0.0265 μs | 11.081 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 14.958 μs |  66.1886 μs |  3.6280 μs | 12.964 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.011 μs |   5.5044 μs |  0.3017 μs | 13.155 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.512 μs |   6.3503 μs |  0.3481 μs | 27.431 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.090 μs |   2.4966 μs |  0.1368 μs |  9.027 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.241 μs |   2.0748 μs |  0.1137 μs | 20.208 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.823 μs |  10.8535 μs |  0.5949 μs | 19.596 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.668 μs |   8.2278 μs |  0.4510 μs | 22.821 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.837 μs |   0.7426 μs |  0.0407 μs |  5.860 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.461 μs |   3.4841 μs |  0.1910 μs | 14.511 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,174.58 ns | 232.727 ns | 12.757 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.350 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.29 ns |   0.429 ns |  0.024 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.57 ns |   0.192 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.88 ns |  20.992 ns |  1.151 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     13.22 ns |   0.104 ns |  0.006 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    115.63 ns |  49.742 ns |  2.727 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.13 ns |   0.820 ns |  0.045 ns |  0.66 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.355 μs |  18.881 μs |  1.0349 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 509.089 μs | 168.662 μs |  9.2449 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.181 μs |   2.936 μs |  0.1609 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 260.830 μs | 278.980 μs | 15.2918 μs |      80 B |


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