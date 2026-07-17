---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 18:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,194.5 μs** |    **735.80 μs** |    **40.33 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,653.7 μs |    169.04 μs |     9.27 μs |  0.43 |    0.00 |        - |       - |   35184 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,179.3 μs** |    **559.23 μs** |    **30.65 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,940.4 μs |    772.57 μs |    42.35 μs |  0.55 |    0.01 |  15.6250 |       - |  347365 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,780.6 μs** |  **3,779.67 μs** |   **207.18 μs** |  **1.00** |    **0.04** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,510.8 μs |  7,570.59 μs |   414.97 μs |  0.52 |    0.05 |        - |       - |   37512 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,096.0 μs** |  **2,369.02 μs** |   **129.85 μs** |  **1.00** |    **0.01** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,319.7 μs | 10,515.09 μs |   576.37 μs |  1.11 |    0.05 |        - |       - |  471558 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **116.2 μs** |    **104.40 μs** |     **5.72 μs** |  **1.00** |    **0.06** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    113.2 μs |    177.32 μs |     9.72 μs |  0.98 |    0.08 |        - |       - |    4164 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,185.9 μs** |    **137.89 μs** |     **7.56 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,246.4 μs |  3,509.80 μs |   192.38 μs |  1.05 |    0.14 |        - |       - |   44495 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **932.6 μs** |    **114.14 μs** |     **6.26 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125285 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    862.4 μs |    614.90 μs |    33.70 μs |  0.92 |    0.03 |        - |       - |    7355 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,187.0 μs** | **32,194.27 μs** | **1,764.68 μs** |  **1.04** |    **0.30** |  **74.2188** |       **-** | **1253256 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,641.8 μs |  6,510.31 μs |   356.85 μs |  1.22 |    0.26 |        - |       - |   62448 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,469.1 μs** |    **428.99 μs** |    **23.51 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,505.3 μs |    349.96 μs |    19.18 μs |  0.46 |    0.00 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,466.9 μs** |    **148.80 μs** |     **8.16 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,486.7 μs |     65.47 μs |     3.59 μs |  0.45 |    0.00 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,467.3 μs** |    **392.30 μs** |    **21.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,544.8 μs |  1,795.63 μs |    98.42 μs |  0.47 |    0.02 |        - |       - |     792 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,470.5 μs** |    **173.44 μs** |     **9.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,494.9 μs |    237.79 μs |    13.03 μs |  0.46 |    0.00 |        - |       - |     792 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **103.9 μs** |    **508.5 μs** |  **27.87 μs** |    **90.94 μs** |  **1.04** |    **0.33** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   157.2 μs |    524.5 μs |  28.75 μs |   152.08 μs |  1.58 |    0.41 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **165.4 μs** |    **954.2 μs** |  **52.30 μs** |   **173.46 μs** |  **1.08** |    **0.45** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   174.3 μs |    130.7 μs |   7.16 μs |   171.11 μs |  1.14 |    0.35 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,046.9 μs** | **10,078.5 μs** | **552.44 μs** |   **741.82 μs** |  **1.17** |    **0.69** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,288.1 μs |  2,933.2 μs | 160.78 μs | 1,367.81 μs |  1.43 |    0.53 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,591.1 μs** | **14,494.7 μs** | **794.50 μs** | **1,225.65 μs** |  **1.15** |    **0.66** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,496.4 μs |    325.4 μs |  17.84 μs | 1,499.53 μs |  1.08 |    0.38 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **711.2 ns** |    **903.6 ns** |  **49.53 ns** |  **1.00** |    **0.09** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,719.7 ns |    190.7 ns |  10.45 ns |  2.43 |    0.15 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,549.7 ns** |  **6,868.0 ns** | **376.46 ns** |  **1.04** |    **0.30** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,799.5 ns | 10,511.3 ns | 576.16 ns |  2.54 |    0.60 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.993 μs |  47.062 μs |  2.5796 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.682 μs |   1.465 μs |  0.0803 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.473 μs |   3.746 μs |  0.2053 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.559 μs |  10.090 μs |  0.5531 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.402 μs |  34.154 μs |  1.8721 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.659 μs |   1.496 μs |  0.0820 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.404 μs |   9.952 μs |  0.5455 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 42.069 μs | 318.060 μs | 17.4339 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.024 μs |   2.697 μs |  0.1478 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.665 μs |  89.329 μs |  4.8964 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 22.159 μs |  58.997 μs |  3.2338 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.348 μs |   7.893 μs |  0.4326 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.955 μs |   4.590 μs |  0.2516 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 15.288 μs |  20.332 μs |  1.1145 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,454.26 ns | 694.627 ns | 38.075 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.72 ns |   5.228 ns |  0.287 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.27 ns |   0.287 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.49 ns |   1.846 ns |  0.101 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.24 ns |  18.705 ns |  1.025 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.72 ns |   0.230 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    119.72 ns |  12.663 ns |  0.694 ns |  1.00 |    0.01 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.63 ns |   4.951 ns |  0.271 ns |  0.64 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.775 μs |   4.334 μs |  0.2376 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 517.975 μs | 576.783 μs | 31.6154 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.021 μs |  27.469 μs |  1.5057 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 239.498 μs |  64.055 μs |  3.5111 μs |      80 B |


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