---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 01:25 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,095.75 μs** |    **454.83 μs** |    **24.931 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,683.17 μs |    196.17 μs |    10.753 μs |  0.44 |    0.00 |       - |       - |   35160 B |        0.32 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,205.44 μs** |  **1,231.89 μs** |    **67.524 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1088298 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,873.07 μs |  2,371.90 μs |   130.012 μs |  0.54 |    0.02 |  7.8125 |       - |  347150 B |        0.32 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,659.61 μs** |    **602.63 μs** |    **33.032 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,803.08 μs |    716.46 μs |    39.271 μs |  0.57 |    0.01 |       - |       - |   37768 B |        0.19 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **9,385.88 μs** |  **4,199.18 μs** |   **230.171 μs** |  **1.00** |    **0.03** | **78.1250** | **31.2500** | **1984309 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,164.55 μs | 23,070.18 μs | 1,264.554 μs |  1.30 |    0.12 |       - |       - |  471068 B |        0.24 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **116.41 μs** |     **75.57 μs** |     **4.142 μs** |  **1.00** |    **0.04** |  **1.3428** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     97.09 μs |    105.92 μs |     5.806 μs |  0.83 |    0.05 |       - |       - |    4136 B |        0.12 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,179.69 μs** |    **649.22 μs** |    **35.586 μs** |  **1.00** |    **0.04** | **13.6719** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,064.56 μs |  1,685.42 μs |    92.383 μs |  0.90 |    0.07 |       - |       - |   43127 B |        0.13 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **747.56 μs** |     **24.84 μs** |     **1.361 μs** |  **1.00** |    **0.00** |  **4.8828** |       **-** |  **124956 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    783.34 μs |    940.26 μs |    51.539 μs |  1.05 |    0.06 |       - |       - |    5612 B |        0.04 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,616.60 μs** |  **2,794.61 μs** |   **153.182 μs** |  **1.00** |    **0.02** | **48.8281** |       **-** | **1250225 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,155.08 μs | 28,476.85 μs | 1,560.912 μs |  1.20 |    0.18 |       - |       - |   60704 B |        0.05 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,440.56 μs** |    **165.41 μs** |     **9.067 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,509.81 μs |     86.20 μs |     4.725 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.64 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,420.96 μs** |    **273.13 μs** |    **14.971 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,954.62 μs | 14,338.17 μs |   785.924 μs |  0.55 |    0.13 |       - |       - |     768 B |        0.64 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,429.48 μs** |    **279.85 μs** |    **15.339 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,511.75 μs |    145.63 μs |     7.982 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.37 |
|                         |               |             |           |              |              |              |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,517.72 μs** |    **210.31 μs** |    **11.528 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,527.88 μs |    344.51 μs |    18.884 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **202.4 μs** |     **56.68 μs** |   **3.11 μs** |  **1.00** |    **0.02** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   213.3 μs |    242.49 μs |  13.29 μs |  1.05 |    0.06 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **203.6 μs** |    **522.33 μs** |  **28.63 μs** |  **1.01** |    **0.17** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   221.0 μs |    809.57 μs |  44.38 μs |  1.10 |    0.23 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,164.3 μs** |  **2,248.21 μs** | **123.23 μs** |  **1.01** |    **0.14** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,032.7 μs |  1,733.30 μs |  95.01 μs |  0.89 |    0.11 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,571.2 μs** | **11,068.68 μs** | **606.71 μs** |  **1.09** |    **0.48** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,629.9 μs |  8,951.11 μs | 490.64 μs |  1.13 |    0.44 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **883.5 ns** | **2,195.4 ns** | **120.34 ns** |  **1.01** |    **0.17** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,766.6 ns | 1,849.5 ns | 101.38 ns |  2.02 |    0.26 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,450.7 ns** |   **983.7 ns** |  **53.92 ns** |  **1.00** |    **0.05** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,155.9 ns | 4,380.3 ns | 240.10 ns |  2.18 |    0.16 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 28.908 μs | 42.4860 μs | 2.3288 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 14.379 μs |  1.6658 μs | 0.0913 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.743 μs | 50.9745 μs | 2.7941 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.226 μs |  1.1393 μs | 0.0624 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.705 μs |  2.9298 μs | 0.1606 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.707 μs |  6.4386 μs | 0.3529 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.380 μs | 48.9259 μs | 2.6818 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.164 μs |  1.3954 μs | 0.0765 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.919 μs |  0.2809 μs | 0.0154 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.273 μs |  0.8360 μs | 0.0458 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.265 μs |  5.6669 μs | 0.3106 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.152 μs |  6.9405 μs | 0.3804 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.032 μs |  0.7291 μs | 0.0400 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 17.351 μs | 14.1243 μs | 0.7742 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,839.70 ns | 663.598 ns | 36.374 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.81 ns |   0.135 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.32 ns |   2.112 ns |  0.116 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.01 ns |   1.548 ns |  0.085 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.66 ns |   4.390 ns |  0.241 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.021 ns |  0.001 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    112.40 ns |  79.596 ns |  4.363 ns |  1.00 |    0.05 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.82 ns |   3.222 ns |  0.177 ns |  0.68 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.78 μs |  26.440 μs |  1.449 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 549.31 μs | 761.567 μs | 41.744 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.08 μs |   6.122 μs |  0.336 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 258.01 μs |  79.253 μs |  4.344 μs |      80 B |


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