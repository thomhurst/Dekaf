---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 15:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,283.6 μs** |  **1,550.91 μs** |    **85.01 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **109098 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,390.2 μs |    545.99 μs |    29.93 μs |  0.54 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,490.7 μs** |    **398.77 μs** |    **21.86 μs** |  **1.00** |    **0.00** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,894.8 μs |  9,007.76 μs |   493.75 μs |  0.52 |    0.06 |  15.6250 |       - |  347433 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,155.1 μs** |    **490.14 μs** |    **26.87 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198702 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,343.4 μs |    252.97 μs |    13.87 μs |  0.54 |    0.00 |        - |       - |   38026 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,724.6 μs** |  **3,835.66 μs** |   **210.25 μs** |  **1.00** |    **0.02** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,795.0 μs | 12,543.48 μs |   687.55 μs |  0.77 |    0.05 |  15.6250 |       - |  397454 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.3 μs** |     **21.54 μs** |     **1.18 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    113.7 μs |     95.28 μs |     5.22 μs |  0.83 |    0.03 |        - |       - |    4190 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,371.9 μs** |    **320.32 μs** |    **17.56 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,811.7 μs |  6,807.87 μs |   373.16 μs |  1.32 |    0.24 |        - |       - |   42286 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,087.4 μs** |     **55.97 μs** |     **3.07 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125490 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    847.3 μs |  1,040.17 μs |    57.02 μs |  0.78 |    0.05 |        - |       - |    8918 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,949.7 μs** | **32,480.31 μs** | **1,780.36 μs** |  **1.02** |    **0.24** |  **74.2188** |       **-** | **1255475 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,777.1 μs |  8,409.07 μs |   460.93 μs |  0.80 |    0.14 |        - |       - |   89906 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,425.1 μs** |     **22.94 μs** |     **1.26 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,427.5 μs |    200.67 μs |    11.00 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,440.4 μs** |    **306.17 μs** |    **16.78 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,398.6 μs |    146.41 μs |     8.03 μs |  0.62 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,448.4 μs** |    **135.75 μs** |     **7.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,397.3 μs |     21.98 μs |     1.20 μs |  0.62 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,457.5 μs** |     **79.79 μs** |     **4.37 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,395.8 μs |     58.62 μs |     3.21 μs |  0.62 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **126.7 μs** |    **814.1 μs** |  **44.62 μs** |   **103.3 μs** |  **1.07** |    **0.43** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   175.8 μs |    275.5 μs |  15.10 μs |   168.0 μs |  1.49 |    0.40 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **165.9 μs** |  **1,038.4 μs** |  **56.92 μs** |   **144.5 μs** |  **1.07** |    **0.43** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   229.4 μs |    848.4 μs |  46.50 μs |   213.8 μs |  1.48 |    0.47 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,123.0 μs** |  **5,007.7 μs** | **274.49 μs** | **1,135.8 μs** |  **1.04** |    **0.32** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,267.2 μs |    349.7 μs |  19.17 μs | 1,271.4 μs |  1.18 |    0.26 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,509.3 μs** | **12,522.6 μs** | **686.41 μs** | **1,136.2 μs** |  **1.12** |    **0.58** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,912.9 μs | 11,615.6 μs | 636.69 μs | 1,558.0 μs |  1.42 |    0.62 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **881.5 ns** |   **744.2 ns** |  **40.79 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,137.4 ns |   241.6 ns |  13.24 ns |  2.43 |    0.10 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,528.0 ns** | **2,586.9 ns** | **141.80 ns** |  **1.01** |    **0.11** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,507.7 ns | 6,378.5 ns | 349.63 ns |  2.31 |    0.27 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.171 μs |  4.216 μs | 0.2311 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.452 μs |  6.788 μs | 0.3721 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.057 μs |  2.898 μs | 0.1589 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  6.299 μs |  3.157 μs | 0.1731 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.593 μs |  3.243 μs | 0.1778 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          |  9.951 μs |  6.399 μs | 0.3508 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 11.303 μs | 31.723 μs | 1.7389 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.378 μs |  4.494 μs | 0.2463 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  7.932 μs |  1.493 μs | 0.0819 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 15.907 μs |  2.356 μs | 0.1292 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 16.134 μs |  9.354 μs | 0.5127 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 16.414 μs | 17.476 μs | 0.9579 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.921 μs |  5.549 μs | 0.3041 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.596 μs |  6.941 μs | 0.3805 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,892.099 ns | 111.9440 ns | 6.1360 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    13.010 ns |   0.1632 ns | 0.0089 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    14.969 ns |   0.3587 ns | 0.0197 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    30.225 ns |   0.7139 ns | 0.0391 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    25.263 ns |   0.2131 ns | 0.0117 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     9.287 ns |   0.2088 ns | 0.0114 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    79.918 ns |   4.9081 ns | 0.2690 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |    67.408 ns |   3.8862 ns | 0.2130 ns |  0.84 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   8.038 μs |  10.369 μs |  0.5684 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 421.788 μs | 624.218 μs | 34.2155 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.917 μs |   7.683 μs |  0.4211 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 170.376 μs | 105.488 μs |  5.7822 μs |      80 B |


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