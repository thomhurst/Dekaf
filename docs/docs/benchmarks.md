---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 09:26 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,122.5 μs** |    **433.59 μs** |    **23.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,924.0 μs |    744.25 μs |    40.80 μs |  0.31 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,259.6 μs** |    **260.65 μs** |    **14.29 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,445.8 μs |  3,200.16 μs |   175.41 μs |  0.47 |    0.02 |  15.6250 |       - |  345856 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,623.2 μs** |    **961.93 μs** |    **52.73 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198702 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,722.8 μs |    252.91 μs |    13.86 μs |  0.41 |    0.00 |        - |       - |   36029 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,637.6 μs** |  **7,930.00 μs** |   **434.67 μs** |  **1.00** |    **0.05** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,162.3 μs |  3,763.04 μs |   206.26 μs |  0.79 |    0.03 |  15.6250 |       - |  381978 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **115.1 μs** |     **29.46 μs** |     **1.61 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    131.2 μs |     59.61 μs |     3.27 μs |  1.14 |    0.03 |        - |       - |    4238 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,216.0 μs** |  **1,410.13 μs** |    **77.29 μs** |  **1.00** |    **0.08** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,177.7 μs |  3,312.69 μs |   181.58 μs |  0.97 |    0.14 |        - |       - |   42574 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **958.8 μs** |      **9.24 μs** |     **0.51 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125328 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    812.5 μs |  1,601.13 μs |    87.76 μs |  0.85 |    0.08 |        - |       - |    6983 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,579.4 μs** | **33,652.91 μs** | **1,844.63 μs** |  **1.04** |    **0.29** |  **74.2188** |       **-** | **1253996 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,032.5 μs |  7,982.22 μs |   437.53 μs |  0.97 |    0.21 |        - |       - |   87994 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,476.7 μs** |    **665.31 μs** |    **36.47 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,451.8 μs |     93.57 μs |     5.13 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,472.8 μs** |     **59.30 μs** |     **3.25 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,462.6 μs |     80.59 μs |     4.42 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,470.3 μs** |    **392.55 μs** |    **21.52 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,484.6 μs |    148.74 μs |     8.15 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,531.6 μs** |    **512.57 μs** |    **28.10 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,486.5 μs |    127.76 μs |     7.00 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean        | Error        | StdDev     | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |------------:|-------------:|-----------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |    **97.62 μs** |     **20.55 μs** |   **1.127 μs** |    **97.79 μs** |  **1.00** |    **0.01** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   191.42 μs |    501.46 μs |  27.487 μs |   192.65 μs |  1.96 |    0.24 |   39.98 KB |        0.62 |
|                      |              |             |             |              |            |             |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **156.40 μs** |    **781.13 μs** |  **42.816 μs** |   **167.94 μs** |  **1.06** |    **0.39** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   189.58 μs |    248.28 μs |  13.609 μs |   194.92 μs |  1.28 |    0.36 |  215.77 KB |        0.90 |
|                      |              |             |             |              |            |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **849.86 μs** |  **3,834.70 μs** | **210.193 μs** |   **734.66 μs** |  **1.04** |    **0.30** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,175.72 μs |  3,031.43 μs | 166.163 μs | 1,084.53 μs |  1.43 |    0.32 |  476.66 KB |        0.73 |
|                      |              |             |             |              |            |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,515.93 μs** | **14,371.46 μs** | **787.748 μs** | **1,171.81 μs** |  **1.17** |    **0.70** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,933.10 μs | 16,118.26 μs | 883.496 μs | 1,424.28 μs |  1.49 |    0.82 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **803.0 ns** | **1,630.5 ns** |  **89.38 ns** |  **1.01** |    **0.14** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,777.6 ns |   550.9 ns |  30.19 ns |  2.23 |    0.23 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,313.1 ns** |   **533.1 ns** |  **29.22 ns** |  **1.00** |    **0.03** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,122.2 ns | 3,793.5 ns | 207.93 ns |  2.38 |    0.14 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.843 μs |  3.2139 μs | 0.1762 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.891 μs |  0.6553 μs | 0.0359 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.574 μs | 47.6765 μs | 2.6133 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.407 μs | 43.4116 μs | 2.3795 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.100 μs |  2.1250 μs | 0.1165 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.194 μs |  6.3380 μs | 0.3474 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.271 μs | 42.1847 μs | 2.3123 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 35.046 μs |  0.5673 μs | 0.0311 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 16.083 μs |  6.1044 μs | 0.3346 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.254 μs |  8.8473 μs | 0.4850 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.867 μs | 52.8009 μs | 2.8942 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.879 μs | 67.9673 μs | 3.7255 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.432 μs |  5.1905 μs | 0.2845 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.395 μs |  5.0212 μs | 0.2752 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,859.54 ns | 419.674 ns | 23.004 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.84 ns |   1.922 ns |  0.105 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.82 ns |   1.536 ns |  0.084 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.52 ns |   1.163 ns |  0.064 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.07 ns |   1.983 ns |  0.109 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     16.23 ns |   0.357 ns |  0.020 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    105.86 ns |  29.724 ns |  1.629 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.79 ns |   0.370 ns |  0.020 ns |  0.73 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.956 μs |   4.743 μs | 0.2600 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 505.727 μs | 106.647 μs | 5.8457 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.499 μs |   6.018 μs | 0.3299 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 212.726 μs |  20.067 μs | 1.0999 μs |      80 B |


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