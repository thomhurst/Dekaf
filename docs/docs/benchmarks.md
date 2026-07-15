---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 11:56 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,235.1 μs** |    **314.85 μs** |    **17.26 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,769.4 μs |    593.54 μs |    32.53 μs |  0.44 |    0.00 |        - |       - |   35171 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,402.0 μs** |    **669.17 μs** |    **36.68 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,907.0 μs |  1,356.88 μs |    74.38 μs |  0.53 |    0.01 |  15.6250 |       - |  347196 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,343.4 μs** |    **143.48 μs** |     **7.86 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,558.5 μs |  6,957.95 μs |   381.39 μs |  0.56 |    0.05 |        - |       - |   37338 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,495.7 μs** |  **5,040.82 μs** |   **276.30 μs** |  **1.00** |    **0.03** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,621.7 μs | 12,408.24 μs |   680.14 μs |  0.93 |    0.05 |  15.6250 |       - |  473279 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.4 μs** |     **89.46 μs** |     **4.90 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    116.1 μs |    212.13 μs |    11.63 μs |  0.81 |    0.07 |        - |       - |    4239 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,189.5 μs** |  **2,508.54 μs** |   **137.50 μs** |  **1.01** |    **0.15** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,114.3 μs |  1,041.30 μs |    57.08 μs |  0.95 |    0.11 |        - |       - |   42316 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,059.9 μs** |     **35.71 μs** |     **1.96 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125465 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    804.5 μs |  1,176.24 μs |    64.47 μs |  0.76 |    0.05 |        - |       - |    7524 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,987.0 μs** | **26,169.55 μs** | **1,434.44 μs** |  **1.02** |    **0.19** |  **74.2188** |       **-** | **1255628 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  6,779.3 μs |  8,313.05 μs |   455.67 μs |  0.69 |    0.10 |        - |       - |  105063 B |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,493.5 μs** |    **335.63 μs** |    **18.40 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,612.3 μs |    479.60 μs |    26.29 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **6,037.2 μs** | **16,499.66 μs** |   **904.40 μs** |  **1.01** |    **0.18** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,607.9 μs |     51.55 μs |     2.83 μs |  0.44 |    0.05 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,518.2 μs** |     **87.51 μs** |     **4.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,610.5 μs |     54.75 μs |     3.00 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,517.5 μs** |     **65.10 μs** |     **3.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,591.1 μs |    482.12 μs |    26.43 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **159.4 μs** |    **817.14 μs** |  **44.79 μs** |   **167.2 μs** |  **1.06** |    **0.39** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   177.0 μs |    735.99 μs |  40.34 μs |   163.1 μs |  1.18 |    0.40 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **150.2 μs** |    **596.61 μs** |  **32.70 μs** |   **144.7 μs** |  **1.03** |    **0.27** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   180.1 μs |    157.70 μs |   8.64 μs |   180.8 μs |  1.24 |    0.23 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,099.2 μs** |  **4,502.47 μs** | **246.80 μs** | **1,097.2 μs** |  **1.04** |    **0.29** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,254.8 μs |     40.44 μs |   2.22 μs | 1,255.5 μs |  1.18 |    0.24 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,451.2 μs** | **12,068.93 μs** | **661.54 μs** | **1,071.8 μs** |  **1.12** |    **0.58** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,866.2 μs | 12,410.40 μs | 680.26 μs | 1,491.7 μs |  1.44 |    0.66 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **878.5 ns** |   **299.8 ns** |  **16.43 ns** |  **1.00** |    **0.02** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,127.3 ns | 2,736.6 ns | 150.00 ns |  2.42 |    0.15 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,480.5 ns** | **2,135.7 ns** | **117.07 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,422.6 ns | 1,773.5 ns |  97.21 ns |  2.32 |    0.16 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 17.985 μs |  5.054 μs | 0.2770 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  6.103 μs |  8.034 μs | 0.4404 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  4.372 μs |  9.571 μs | 0.5246 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  4.169 μs |  9.639 μs | 0.5283 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  6.125 μs | 12.422 μs | 0.6809 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          |  7.974 μs | 15.056 μs | 0.8253 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     |  7.480 μs | 11.063 μs | 0.6064 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 20.277 μs |  9.951 μs | 0.5454 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.771 μs |  5.957 μs | 0.3265 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 13.405 μs |  6.107 μs | 0.3348 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 11.268 μs | 59.262 μs | 3.2484 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 12.377 μs | 67.486 μs | 3.6992 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.694 μs | 21.342 μs | 1.1699 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  6.772 μs | 44.686 μs | 2.4494 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 7,189.883 ns | 630.1987 ns | 34.5433 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     8.486 ns |   0.6858 ns |  0.0376 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    10.680 ns |   0.3213 ns |  0.0176 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    20.355 ns |   4.9766 ns |  0.2728 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    19.552 ns |   2.0208 ns |  0.1108 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     5.971 ns |   0.2046 ns |  0.0112 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    63.823 ns |  16.8049 ns |  0.9211 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |    48.255 ns |   2.0255 ns |  0.1110 ns |  0.76 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   5.763 μs |  33.706 μs |  1.848 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 280.570 μs | 394.763 μs | 21.638 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   3.832 μs |  34.215 μs |  1.875 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 128.204 μs | 130.663 μs |  7.162 μs |      80 B |


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