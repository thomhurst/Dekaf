---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 03:51 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,319.2 μs** |    **210.04 μs** |    **11.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,860.4 μs |  1,487.64 μs |    81.54 μs |  0.29 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,504.1 μs** |  **1,177.36 μs** |    **64.54 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,787.6 μs |  4,308.14 μs |   236.14 μs |  0.50 |    0.03 |  15.6250 |       - |  346127 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,183.9 μs** |    **235.71 μs** |    **12.92 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,807.7 μs |    237.44 μs |    13.01 μs |  0.45 |    0.00 |        - |       - |   36029 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,115.1 μs** |  **8,254.31 μs** |   **452.45 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,610.5 μs |  8,077.57 μs |   442.76 μs |  0.81 |    0.04 |  15.6250 |       - |  380828 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.8 μs** |     **72.40 μs** |     **3.97 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    242.1 μs |  1,166.58 μs |    63.94 μs |  1.76 |    0.40 |        - |       - |    4167 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,337.2 μs** |     **62.16 μs** |     **3.41 μs** |  **1.00** |    **0.00** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,523.7 μs |    594.39 μs |    32.58 μs |  1.14 |    0.02 |        - |       - |   41948 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,090.4 μs** |     **56.66 μs** |     **3.11 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125512 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    935.2 μs |    449.86 μs |    24.66 μs |  0.86 |    0.02 |        - |       - |    8473 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,967.3 μs** | **27,582.96 μs** | **1,511.92 μs** |  **1.02** |    **0.20** |  **74.2188** |       **-** | **1255841 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,697.2 μs | 16,612.44 μs |   910.58 μs |  0.89 |    0.15 |        - |       - |   77604 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,504.1 μs** |    **117.11 μs** |     **6.42 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,547.3 μs |     91.43 μs |     5.01 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,501.0 μs** |    **133.99 μs** |     **7.34 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,541.9 μs |    136.03 μs |     7.46 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,495.4 μs** |     **37.71 μs** |     **2.07 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,557.6 μs |     98.24 μs |     5.38 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,506.1 μs** |     **86.16 μs** |     **4.72 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,542.3 μs |    113.15 μs |     6.20 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **124.6 μs** |    **501.9 μs** |  **27.51 μs** |   **129.0 μs** |  **1.04** |    **0.29** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   163.8 μs |    302.3 μs |  16.57 μs |   155.4 μs |  1.36 |    0.31 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **164.3 μs** |    **850.4 μs** |  **46.61 μs** |   **158.8 μs** |  **1.06** |    **0.37** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   203.2 μs |    347.2 μs |  19.03 μs |   204.2 μs |  1.31 |    0.34 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **930.0 μs** |  **2,776.2 μs** | **152.17 μs** |   **882.2 μs** |  **1.02** |    **0.20** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,208.7 μs |    243.3 μs |  13.34 μs | 1,202.8 μs |  1.32 |    0.18 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,444.2 μs** | **12,078.2 μs** | **662.05 μs** | **1,093.5 μs** |  **1.13** |    **0.59** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,810.6 μs | 11,961.4 μs | 655.65 μs | 1,442.1 μs |  1.41 |    0.64 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **991.9 ns** |  **3,870.4 ns** | **212.15 ns** |  **1.03** |    **0.26** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,093.3 ns |  2,375.0 ns | 130.18 ns |  2.17 |    0.40 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,445.7 ns** |  **1,499.2 ns** |  **82.17 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,825.1 ns | 15,460.1 ns | 847.42 ns |  2.65 |    0.52 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.418 μs | 169.758 μs | 9.3050 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.141 μs |   1.861 μs | 0.1020 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.767 μs |   3.418 μs | 0.1874 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 10.170 μs |  47.548 μs | 2.6063 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.418 μs |   3.434 μs | 0.1882 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 18.775 μs |   3.814 μs | 0.2090 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.269 μs |   3.863 μs | 0.2117 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.354 μs |  12.266 μs | 0.6723 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.043 μs |   1.387 μs | 0.0760 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.418 μs |   3.176 μs | 0.1741 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.198 μs |  34.591 μs | 1.8961 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.063 μs |  38.297 μs | 2.0992 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.095 μs |   6.810 μs | 0.3733 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.885 μs |  18.345 μs | 1.0055 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,667.14 ns | 95.343 ns | 5.226 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.58 ns |  0.836 ns | 0.046 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.36 ns |  0.093 ns | 0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.58 ns |  2.705 ns | 0.148 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.46 ns |  2.395 ns | 0.131 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.77 ns |  0.173 ns | 0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.68 ns | 44.040 ns | 2.414 ns |  1.00 |    0.03 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     74.22 ns |  2.371 ns | 0.130 ns |  0.66 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.248 μs |   3.516 μs |  0.1927 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 520.497 μs | 298.385 μs | 16.3555 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.673 μs |  19.060 μs |  1.0447 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 243.244 μs |  98.613 μs |  5.4053 μs |      80 B |


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