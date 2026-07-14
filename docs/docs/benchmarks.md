---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 22:55 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,152.4 μs** |    **332.21 μs** |    **18.21 μs** |  **6,153.7 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109091 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,399.3 μs |  1,042.26 μs |    57.13 μs |  3,368.3 μs |  0.55 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,349.4 μs** |  **1,452.38 μs** |    **79.61 μs** |  **7,333.9 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,828.7 μs |  2,154.22 μs |   118.08 μs |  3,794.9 μs |  0.52 |    0.01 |  15.6250 |       - |  347527 B |        0.32 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,257.1 μs** |    **832.32 μs** |    **45.62 μs** |  **6,270.9 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,355.2 μs |    193.53 μs |    10.61 μs |  3,358.5 μs |  0.54 |    0.00 |        - |       - |   38040 B |        0.19 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,241.9 μs** |  **3,475.47 μs** |   **190.50 μs** | **12,317.5 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,663.1 μs | 24,688.49 μs | 1,353.26 μs | 11,353.2 μs |  0.87 |    0.10 |  15.6250 |       - |  395342 B |        0.20 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.6 μs** |     **53.08 μs** |     **2.91 μs** |    **130.9 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    120.4 μs |    155.99 μs |     8.55 μs |    121.9 μs |  0.92 |    0.06 |        - |       - |    4172 B |        0.12 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,325.2 μs** |    **459.94 μs** |    **25.21 μs** |  **1,332.3 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  2,054.2 μs | 21,367.27 μs | 1,171.21 μs |  1,512.1 μs |  1.55 |    0.77 |        - |       - |   43360 B |        0.13 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,095.8 μs** |    **306.88 μs** |    **16.82 μs** |  **1,101.8 μs** |  **1.00** |    **0.02** |   **7.3242** |       **-** |  **125516 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    826.1 μs |  1,252.68 μs |    68.66 μs |    814.7 μs |  0.75 |    0.06 |        - |       - |    8545 B |        0.07 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,127.8 μs** | **32,034.26 μs** | **1,755.91 μs** | **11,015.5 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1255574 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,678.2 μs | 17,405.28 μs |   954.04 μs |  7,655.2 μs |  0.78 |    0.15 |        - |       - |   75975 B |        0.06 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,426.4 μs** |    **140.09 μs** |     **7.68 μs** |  **5,425.5 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,454.6 μs |    450.91 μs |    24.72 μs |  3,446.4 μs |  0.64 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,433.4 μs** |    **221.39 μs** |    **12.14 μs** |  **5,426.8 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,435.8 μs |    118.69 μs |     6.51 μs |  3,433.4 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,457.7 μs** |    **240.32 μs** |    **13.17 μs** |  **5,463.0 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,461.5 μs |     60.46 μs |     3.31 μs |  3,459.7 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,415.4 μs** |     **23.17 μs** |     **1.27 μs** |  **5,414.8 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,430.5 μs |    229.06 μs |    12.56 μs |  3,435.9 μs |  0.63 |    0.00 |        - |       - |     805 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **132.5 μs** |    **563.6 μs** |  **30.89 μs** |   **138.0 μs** |  **1.04** |    **0.31** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   152.3 μs |    190.8 μs |  10.46 μs |   156.7 μs |  1.20 |    0.27 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **175.5 μs** |    **701.4 μs** |  **38.45 μs** |   **192.8 μs** |  **1.04** |    **0.30** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   188.7 μs |    162.0 μs |   8.88 μs |   185.2 μs |  1.12 |    0.25 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,102.7 μs** |  **4,945.3 μs** | **271.07 μs** | **1,114.0 μs** |  **1.04** |    **0.32** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,358.1 μs |  6,363.1 μs | 348.78 μs | 1,163.3 μs |  1.29 |    0.41 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,440.4 μs** | **12,025.7 μs** | **659.17 μs** | **1,071.4 μs** |  **1.12** |    **0.58** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,838.4 μs | 11,030.6 μs | 604.62 μs | 1,601.1 μs |  1.43 |    0.62 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **829.3 ns** |   **571.4 ns** |  **31.32 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,031.3 ns |   490.3 ns |  26.88 ns |  2.45 |    0.09 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,433.9 ns** | **1,690.9 ns** |  **92.69 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,844.2 ns | 9,916.8 ns | 543.57 ns |  2.69 |    0.36 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.873 μs | 10.021 μs | 0.5493 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.893 μs | 11.030 μs | 0.6046 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.407 μs | 39.035 μs | 2.1397 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.116 μs |  1.496 μs | 0.0820 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.666 μs | 34.262 μs | 1.8780 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 17.493 μs |  9.040 μs | 0.4955 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.291 μs |  2.930 μs | 0.1606 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.129 μs |  3.217 μs | 0.1764 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.147 μs |  2.593 μs | 0.1421 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 24.970 μs | 79.907 μs | 4.3800 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.722 μs | 23.558 μs | 1.2913 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.505 μs | 17.698 μs | 0.9701 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.401 μs | 19.541 μs | 1.0711 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.414 μs | 12.737 μs | 0.6982 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,107.77 ns | 2,385.816 ns | 130.775 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.82 ns |     1.308 ns |   0.072 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.72 ns |     6.140 ns |   0.337 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.43 ns |     0.160 ns |   0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.62 ns |     2.717 ns |   0.149 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.99 ns |     0.321 ns |   0.018 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.33 ns |    30.979 ns |   1.698 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     80.66 ns |    12.338 ns |   0.676 ns |  0.74 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.776 μs |  15.736 μs |  0.8626 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 526.035 μs | 362.310 μs | 19.8594 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.526 μs |  11.533 μs |  0.6322 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 222.753 μs | 280.393 μs | 15.3693 μs |      80 B |


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