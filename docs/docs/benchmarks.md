---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 06:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----------:|------------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,214.1 μs** |   **734.96 μs** |  **40.29 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 3,340.6 μs |   350.88 μs |  19.23 μs |  0.54 |    0.00 |       - |       - |   35192 B |        0.32 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,317.6 μs** |   **444.07 μs** |  **24.34 μs** |  **1.00** |    **0.00** | **31.2500** | **15.6250** | **1088298 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,688.1 μs | 4,133.72 μs | 226.58 μs |  0.50 |    0.03 |  7.8125 |       - |  347499 B |        0.32 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,554.8 μs** |   **388.43 μs** |  **21.29 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 3,287.7 μs |   569.47 μs |  31.21 μs |  0.50 |    0.00 |       - |       - |   37951 B |        0.19 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,961.9 μs** | **1,189.77 μs** |  **65.22 μs** |  **1.00** |    **0.01** | **78.1250** | **31.2500** | **1984309 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 7,798.0 μs | 2,809.30 μs | 153.99 μs |  0.87 |    0.02 | 15.6250 |       - |  393301 B |        0.20 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **117.2 μs** |    **49.83 μs** |   **2.73 μs** |  **1.00** |    **0.03** |  **1.3428** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   128.1 μs |   292.46 μs |  16.03 μs |  1.09 |    0.12 |       - |       - |    4177 B |        0.12 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,211.4 μs** | **4,606.47 μs** | **252.50 μs** |  **1.03** |    **0.27** | **13.6719** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      | 1,171.8 μs | 2,013.32 μs | 110.36 μs |  1.00 |    0.20 |       - |       - |   41974 B |        0.12 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **722.8 μs** |    **67.77 μs** |   **3.71 μs** |  **1.00** |    **0.01** |  **4.8828** |       **-** |  **124912 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   653.7 μs | 1,573.59 μs |  86.25 μs |  0.90 |    0.10 |       - |       - |    7167 B |        0.06 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,442.6 μs** | **3,143.03 μs** | **172.28 μs** |  **1.00** |    **0.03** | **48.8281** |       **-** | **1249764 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 6,078.8 μs | 1,960.01 μs | 107.43 μs |  0.82 |    0.02 |       - |       - |   90087 B |        0.07 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,452.2 μs** |    **43.69 μs** |   **2.39 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 3,403.7 μs |   323.06 μs |  17.71 μs |  0.62 |    0.00 |       - |       - |     800 B |        0.67 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,466.2 μs** |   **221.50 μs** |  **12.14 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 3,368.8 μs |    89.23 μs |   4.89 μs |  0.62 |    0.00 |       - |       - |     800 B |        0.67 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,443.7 μs** |   **178.35 μs** |   **9.78 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 3,379.1 μs |   204.52 μs |  11.21 μs |  0.62 |    0.00 |       - |       - |     800 B |        0.38 |
|                         |               |             |           |            |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,437.2 μs** |    **22.08 μs** |   **1.21 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 3,395.7 μs |   219.70 μs |  12.04 μs |  0.62 |    0.00 |       - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **165.6 μs** |   **369.5 μs** |  **20.25 μs** |  **1.01** |    **0.15** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   190.0 μs |   499.5 μs |  27.38 μs |  1.16 |    0.19 |   39.98 KB |        0.62 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **168.5 μs** |   **661.0 μs** |  **36.23 μs** |  **1.03** |    **0.26** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   207.3 μs |   607.6 μs |  33.30 μs |  1.27 |    0.28 |  215.77 KB |        0.90 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **974.5 μs** | **4,039.8 μs** | **221.44 μs** |  **1.03** |    **0.27** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,196.4 μs | 2,762.1 μs | 151.40 μs |  1.27 |    0.26 |  476.66 KB |        0.73 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,507.5 μs** | **6,157.5 μs** | **337.51 μs** |  **1.03** |    **0.28** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,684.4 μs | 8,547.4 μs | 468.51 μs |  1.15 |    0.35 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **755.1 ns** |   **437.6 ns** |  **23.98 ns** |  **1.00** |    **0.04** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,780.4 ns |   224.9 ns |  12.33 ns |  2.36 |    0.07 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,456.4 ns** | **1,773.3 ns** |  **97.20 ns** |  **1.00** |    **0.08** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,042.2 ns | 5,780.1 ns | 316.83 ns |  2.10 |    0.22 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.146 μs |  1.7613 μs | 0.0965 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.666 μs |  4.8441 μs | 0.2655 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.319 μs |  2.4499 μs | 0.1343 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.252 μs |  0.4591 μs | 0.0252 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.313 μs | 41.7156 μs | 2.2866 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.350 μs |  5.0029 μs | 0.2742 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.161 μs | 47.5053 μs | 2.6039 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.961 μs |  3.9761 μs | 0.2179 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.894 μs |  0.0948 μs | 0.0052 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 24.800 μs | 69.3894 μs | 3.8035 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.486 μs |  7.3165 μs | 0.4010 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 25.979 μs | 49.5253 μs | 2.7146 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.587 μs |  9.4305 μs | 0.5169 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.798 μs |  5.3848 μs | 0.2952 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,553.40 ns | 472.116 ns | 25.878 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.82 ns |   0.272 ns |  0.015 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.26 ns |   0.442 ns |  0.024 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.92 ns |   1.811 ns |  0.099 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.68 ns |  11.996 ns |  0.658 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.76 ns |   2.445 ns |  0.134 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    115.97 ns |  17.950 ns |  0.984 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.44 ns |   0.538 ns |  0.030 ns |  0.65 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.135 μs |   2.174 μs |  0.1192 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 540.769 μs | 868.587 μs | 47.6102 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.948 μs |   2.262 μs |  0.1240 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 238.664 μs | 356.392 μs | 19.5350 μs |      80 B |


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