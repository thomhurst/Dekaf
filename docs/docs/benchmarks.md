---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 07:35 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,132.5 μs** |    **713.06 μs** |    **39.08 μs** |  **1.00** |    **0.01** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,645.4 μs |    341.07 μs |    18.70 μs |  0.43 |    0.00 |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,464.6 μs** |    **410.83 μs** |    **22.52 μs** |  **1.00** |    **0.00** |       **-** | **1088292 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,849.0 μs |  1,546.92 μs |    84.79 μs |  0.52 |    0.01 |       - |  347177 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **7,340.7 μs** | **15,054.46 μs** |   **825.19 μs** |  **1.01** |    **0.14** |       **-** |  **198690 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,289.1 μs |  2,533.47 μs |   138.87 μs |  0.45 |    0.05 |       - |   37797 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **8,158.6 μs** |    **849.59 μs** |    **46.57 μs** |  **1.00** |    **0.01** | **15.6250** | **1984295 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,321.1 μs |  4,899.48 μs |   268.56 μs |  1.39 |    0.03 |       - |  467347 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **129.0 μs** |     **92.02 μs** |     **5.04 μs** |  **1.00** |    **0.05** |  **0.2441** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    108.7 μs |    245.41 μs |    13.45 μs |  0.84 |    0.09 |       - |    4087 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,392.4 μs** |  **1,380.05 μs** |    **75.65 μs** |  **1.00** |    **0.07** |  **3.9063** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  2,438.9 μs | 23,697.96 μs | 1,298.97 μs |  1.75 |    0.81 |       - |   41696 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **724.0 μs** |    **585.79 μs** |    **32.11 μs** |  **1.00** |    **0.05** |  **1.4648** |  **124911 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    893.7 μs |    583.79 μs |    32.00 μs |  1.24 |    0.06 |       - |    5613 B |        0.04 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,973.9 μs** | **38,289.54 μs** | **2,098.78 μs** |  **1.03** |    **0.28** | **13.6719** | **1248443 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,895.8 μs |  9,024.32 μs |   494.65 μs |  0.91 |    0.17 |       - |   60823 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,375.6 μs** |    **669.38 μs** |    **36.69 μs** |  **1.00** |    **0.01** |       **-** |    **1204 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,452.2 μs |     57.15 μs |     3.13 μs |  0.46 |    0.00 |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,384.2 μs** |    **110.81 μs** |     **6.07 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,544.2 μs |  1,287.40 μs |    70.57 μs |  0.47 |    0.01 |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,421.5 μs** |  **1,876.08 μs** |   **102.83 μs** |  **1.00** |    **0.02** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,449.2 μs |     85.64 μs |     4.69 μs |  0.45 |    0.01 |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,362.6 μs** |     **54.37 μs** |     **2.98 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,468.5 μs |     43.99 μs |     2.41 μs |  0.46 |    0.00 |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **177.4 μs** |    **481.0 μs** |  **26.37 μs** |  **1.02** |    **0.19** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   190.2 μs |    693.5 μs |  38.02 μs |  1.09 |    0.24 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **195.0 μs** |    **395.8 μs** |  **21.70 μs** |  **1.01** |    **0.14** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   199.7 μs |    744.8 μs |  40.82 μs |  1.03 |    0.21 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,121.4 μs** |  **3,211.6 μs** | **176.04 μs** |  **1.02** |    **0.19** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,294.6 μs |  1,942.9 μs | 106.50 μs |  1.17 |    0.17 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,641.4 μs** | **10,161.3 μs** | **556.98 μs** |  **1.07** |    **0.42** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,392.0 μs |    279.4 μs |  15.31 μs |  0.91 |    0.22 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         | **1.033 μs** | **1.7905 μs** | **0.0981 μs** |  **1.01** |    **0.12** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1.929 μs | 0.4365 μs | 0.0239 μs |  1.88 |    0.16 |     452 B |        0.70 |
|                      |             |          |           |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1.656 μs** | **0.8625 μs** | **0.0473 μs** |  **1.00** |    **0.04** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3.583 μs | 4.3143 μs | 0.2365 μs |  2.16 |    0.14 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.935 μs |   7.7918 μs | 0.4271 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.279 μs |   1.7056 μs | 0.0935 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.940 μs |  25.8084 μs | 1.4146 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  5.772 μs |   3.1093 μs | 0.1704 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  8.546 μs |   0.8895 μs | 0.0488 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 10.978 μs |  20.7184 μs | 1.1356 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     |  9.970 μs |   9.4073 μs | 0.5156 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 25.708 μs |   5.0870 μs | 0.2788 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  7.813 μs |   3.7308 μs | 0.2045 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.110 μs |  68.3105 μs | 3.7443 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 15.738 μs |  15.0209 μs | 0.8233 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.271 μs | 168.9207 μs | 9.2591 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  3.768 μs |   5.6147 μs | 0.3078 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.569 μs |  14.3304 μs | 0.7855 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,844.981 ns | 91.8232 ns | 5.0331 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    13.464 ns |  0.1512 ns | 0.0083 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    19.023 ns |  0.4748 ns | 0.0260 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    29.339 ns |  1.5946 ns | 0.0874 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    26.504 ns |  3.1424 ns | 0.1722 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     9.307 ns |  0.1598 ns | 0.0088 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    83.703 ns | 21.1968 ns | 1.1619 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |    63.733 ns |  2.2298 ns | 0.1222 ns |  0.76 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error        | StdDev     | Allocated |
|------------------------ |-----------:|-------------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   8.061 μs |     7.719 μs |  0.4231 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 439.302 μs |   442.866 μs | 24.2750 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.298 μs |     6.381 μs |  0.3498 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 198.583 μs | 1,027.581 μs | 56.3252 μs |      80 B |


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