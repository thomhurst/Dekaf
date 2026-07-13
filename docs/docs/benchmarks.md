---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 08:12 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,223.1 μs** |   **846.09 μs** |  **46.38 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,924.9 μs | 1,780.61 μs |  97.60 μs |  0.31 |    0.01 |       - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,274.3 μs** |   **356.49 μs** |  **19.54 μs** |  **1.00** |    **0.00** | **31.2500** | **15.6250** | **1088298 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,522.0 μs | 6,993.02 μs | 383.31 μs |  0.48 |    0.05 |       - |       - |  345966 B |        0.32 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,716.4 μs** |   **552.73 μs** |  **30.30 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,937.2 μs |   826.75 μs |  45.32 μs |  0.44 |    0.01 |       - |       - |   36012 B |        0.18 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **8,882.0 μs** | **1,950.31 μs** | **106.90 μs** |  **1.00** |    **0.01** | **78.1250** | **46.8750** | **1984309 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,215.0 μs | 7,056.22 μs | 386.78 μs |  1.26 |    0.04 |       - |       - |  364628 B |        0.18 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **116.2 μs** |     **7.07 μs** |   **0.39 μs** |  **1.00** |    **0.00** |  **1.3428** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    143.4 μs |    54.94 μs |   3.01 μs |  1.23 |    0.02 |       - |       - |    4097 B |        0.12 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,152.3 μs** |   **116.13 μs** |   **6.37 μs** |  **1.00** |    **0.01** | **13.6719** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,787.1 μs | 8,391.11 μs | 459.95 μs |  1.55 |    0.35 |       - |       - |   42110 B |        0.12 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **728.9 μs** |   **209.86 μs** |  **11.50 μs** |  **1.00** |    **0.02** |  **4.8828** |       **-** |  **124923 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    832.0 μs | 2,834.02 μs | 155.34 μs |  1.14 |    0.19 |       - |       - |    6030 B |        0.05 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **7,369.1 μs** | **1,728.35 μs** |  **94.74 μs** |  **1.00** |    **0.02** | **48.8281** |       **-** | **1249882 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,227.1 μs | 8,707.02 μs | 477.26 μs |  0.98 |    0.06 |       - |       - |   73416 B |        0.06 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,588.2 μs** |   **740.33 μs** |  **40.58 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,447.8 μs |   320.99 μs |  17.59 μs |  0.44 |    0.00 |       - |       - |     800 B |        0.67 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,539.4 μs** |   **448.39 μs** |  **24.58 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,436.3 μs |   304.64 μs |  16.70 μs |  0.44 |    0.00 |       - |       - |     800 B |        0.67 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,513.9 μs** |   **356.55 μs** |  **19.54 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,403.6 μs |    27.99 μs |   1.53 μs |  0.44 |    0.00 |       - |       - |     800 B |        0.38 |
|                         |               |             |           |             |             |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,426.7 μs** |   **833.66 μs** |  **45.70 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,414.7 μs |   126.07 μs |   6.91 μs |  0.44 |    0.00 |       - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **156.1 μs** |   **408.3 μs** |  **22.38 μs** |  **1.01** |    **0.18** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   147.8 μs |   828.4 μs |  45.41 μs |  0.96 |    0.28 |   39.98 KB |        0.62 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **205.6 μs** |   **542.1 μs** |  **29.71 μs** |  **1.02** |    **0.19** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   156.8 μs |   256.2 μs |  14.04 μs |  0.77 |    0.12 |  215.77 KB |        0.90 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,213.2 μs** | **5,443.2 μs** | **298.36 μs** |  **1.04** |    **0.31** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   918.4 μs |   119.0 μs |   6.52 μs |  0.79 |    0.16 |  476.66 KB |        0.73 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,746.3 μs** | **8,982.5 μs** | **492.36 μs** |  **1.06** |    **0.38** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,784.4 μs | 9,042.8 μs | 495.67 μs |  1.08 |    0.39 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **839.0 ns** |   **965.2 ns** |  **52.91 ns** |  **1.00** |    **0.08** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,740.3 ns | 2,169.7 ns | 118.93 ns |  2.08 |    0.16 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,505.6 ns** | **4,596.6 ns** | **251.96 ns** |  **1.02** |    **0.20** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,612.6 ns | 2,281.1 ns | 125.04 ns |  1.77 |    0.24 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.019 μs |  1.4933 μs | 0.0819 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.221 μs |  2.9944 μs | 0.1641 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.630 μs |  1.8700 μs | 0.1025 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 11.481 μs | 36.5735 μs | 2.0047 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.963 μs | 38.2586 μs | 2.0971 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.440 μs |  2.6280 μs | 0.1440 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.841 μs |  2.0264 μs | 0.1111 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.437 μs | 14.2471 μs | 0.7809 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.881 μs |  1.1964 μs | 0.0656 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.294 μs |  1.7798 μs | 0.0976 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.524 μs | 24.0255 μs | 1.3169 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.308 μs | 16.0828 μs | 0.8816 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.110 μs |  0.8381 μs | 0.0459 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.425 μs | 26.4292 μs | 1.4487 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,657.91 ns | 242.105 ns | 13.271 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.59 ns |   2.221 ns |  0.122 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.19 ns |   0.182 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.60 ns |   1.013 ns |  0.056 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.55 ns |  11.638 ns |  0.638 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.84 ns |   0.440 ns |  0.024 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    123.64 ns |  46.286 ns |  2.537 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.43 ns |  14.896 ns |  0.816 ns |  0.62 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev   | Allocated |
|------------------------ |----------:|-----------:|---------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.23 μs |  20.225 μs | 1.109 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 530.67 μs | 120.786 μs | 6.621 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.18 μs |   3.246 μs | 0.178 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 236.74 μs | 169.859 μs | 9.311 μs |      80 B |


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