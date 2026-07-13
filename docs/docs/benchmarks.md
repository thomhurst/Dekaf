---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 14:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,141.1 μs** |    **647.76 μs** |    **35.51 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,886.8 μs |  1,612.58 μs |    88.39 μs |  0.31 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,245.4 μs** |  **1,610.54 μs** |    **88.28 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,670.2 μs | 11,427.75 μs |   626.39 μs |  0.51 |    0.08 |  15.6250 |       - |  345900 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,698.2 μs** |    **763.89 μs** |    **41.87 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198699 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,307.7 μs |  2,306.22 μs |   126.41 μs |  0.34 |    0.02 |        - |       - |   36099 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,387.0 μs** |  **4,830.95 μs** |   **264.80 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,270.7 μs |  9,223.90 μs |   505.59 μs |  0.81 |    0.04 |  15.6250 |       - |  384576 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **115.9 μs** |     **84.42 μs** |     **4.63 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    131.5 μs |     39.77 μs |     2.18 μs |  1.14 |    0.04 |        - |       - |    4177 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,069.6 μs** |  **3,614.01 μs** |   **198.10 μs** |  **1.03** |    **0.25** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,296.0 μs |  1,293.06 μs |    70.88 μs |  1.24 |    0.23 |        - |       - |   42102 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **954.0 μs** |     **76.54 μs** |     **4.20 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125318 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    865.8 μs |  1,594.40 μs |    87.39 μs |  0.91 |    0.08 |        - |       - |    8034 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,579.4 μs** | **29,621.96 μs** | **1,623.68 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1253889 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,815.6 μs | 15,358.26 μs |   841.84 μs |  0.94 |    0.19 |        - |       - |   90330 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,498.5 μs** |    **345.18 μs** |    **18.92 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,488.1 μs |    153.58 μs |     8.42 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,855.2 μs** | **10,963.26 μs** |   **600.93 μs** |  **1.01** |    **0.12** |        **-** |       **-** |    **1394 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,459.4 μs |     93.60 μs |     5.13 μs |  0.25 |    0.02 |        - |       - |     800 B |        0.57 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,465.1 μs** |    **446.94 μs** |    **24.50 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,451.3 μs |     16.22 μs |     0.89 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,481.3 μs** |    **259.52 μs** |    **14.23 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,469.0 μs |    120.60 μs |     6.61 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **138.6 μs** |    **247.7 μs** |  **13.58 μs** |  **1.01** |    **0.12** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   166.8 μs |    284.4 μs |  15.59 μs |  1.21 |    0.14 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **175.0 μs** |    **936.1 μs** |  **51.31 μs** |  **1.06** |    **0.40** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   233.2 μs |    941.3 μs |  51.59 μs |  1.42 |    0.47 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **976.3 μs** |  **4,198.5 μs** | **230.13 μs** |  **1.04** |    **0.33** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,169.7 μs |  2,710.6 μs | 148.58 μs |  1.25 |    0.33 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,653.8 μs** | **14,467.8 μs** | **793.03 μs** |  **1.16** |    **0.68** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,411.0 μs |    137.4 μs |   7.53 μs |  0.99 |    0.38 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **667.8 ns** |   **652.2 ns** |  **35.75 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,723.8 ns |   105.9 ns |   5.81 ns |  2.59 |    0.12 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,388.6 ns** | **1,956.0 ns** | **107.22 ns** |  **1.00** |    **0.09** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,301.8 ns | 3,831.6 ns | 210.02 ns |  2.39 |    0.20 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 23.567 μs |   8.934 μs | 0.4897 μs | 23.320 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  7.380 μs |   2.145 μs | 0.1176 μs |  7.426 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  5.290 μs |  20.658 μs | 1.1323 μs |  4.966 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  3.938 μs |   8.838 μs | 0.4845 μs |  3.871 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  6.343 μs |   7.888 μs | 0.4324 μs |  6.389 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          |  7.245 μs |  12.522 μs | 0.6863 μs |  6.921 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     |  8.765 μs |  14.064 μs | 0.7709 μs |  9.028 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 20.146 μs |  15.538 μs | 0.8517 μs | 20.166 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.222 μs |   6.075 μs | 0.3330 μs | 10.345 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 17.822 μs | 157.696 μs | 8.6438 μs | 13.135 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 16.451 μs |  89.521 μs | 4.9070 μs | 15.232 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.704 μs |  56.892 μs | 3.1184 μs | 22.403 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  2.467 μs |  22.314 μs | 1.2231 μs |  2.083 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  4.560 μs |  36.223 μs | 1.9855 μs |  3.906 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 6,676.386 ns | 24.4558 ns | 1.3405 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     8.238 ns |  0.6948 ns | 0.0381 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     9.550 ns |  3.6065 ns | 0.1977 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    17.020 ns | 10.3834 ns | 0.5692 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    16.775 ns |  1.1673 ns | 0.0640 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     5.802 ns |  0.1016 ns | 0.0056 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    54.560 ns |  5.0076 ns | 0.2745 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |    45.009 ns |  0.3977 ns | 0.0218 ns |  0.82 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev   | Allocated |
|------------------------ |-----------:|-----------:|---------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   5.582 μs |  32.870 μs | 1.802 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 258.259 μs | 151.785 μs | 8.320 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   4.949 μs |  26.393 μs | 1.447 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123.114 μs |  61.962 μs | 3.396 μs |      80 B |


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