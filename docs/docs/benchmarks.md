---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-16 20:19 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,413.1 μs** |    **698.05 μs** |    **38.26 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,767.6 μs |    381.07 μs |    20.89 μs |  0.43 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,495.2 μs** |  **1,157.25 μs** |    **63.43 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,943.0 μs |  3,217.35 μs |   176.35 μs |  0.53 |    0.02 |  15.6250 |       - |  347279 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,191.3 μs** |  **1,273.41 μs** |    **69.80 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,860.3 μs |    917.15 μs |    50.27 μs |  0.62 |    0.01 |        - |       - |   37465 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,229.4 μs** |  **5,267.39 μs** |   **288.72 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,960.8 μs |  8,238.20 μs |   451.56 μs |  0.90 |    0.03 |        - |       - |  468572 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **143.9 μs** |     **53.08 μs** |     **2.91 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    135.0 μs |    128.88 μs |     7.06 μs |  0.94 |    0.05 |        - |       - |    4116 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,347.8 μs** |    **366.99 μs** |    **20.12 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,390.1 μs |  4,796.41 μs |   262.91 μs |  1.03 |    0.17 |        - |       - |   43226 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,064.7 μs** |     **19.52 μs** |     **1.07 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125468 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    926.9 μs |    475.25 μs |    26.05 μs |  0.87 |    0.02 |        - |       - |    6068 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,247.1 μs** | **24,812.03 μs** | **1,360.03 μs** |  **1.01** |    **0.17** |  **74.2188** |       **-** | **1255872 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,482.3 μs |  7,078.80 μs |   388.01 μs |  0.94 |    0.12 |        - |       - |   67964 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,509.2 μs** |    **194.84 μs** |    **10.68 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,613.0 μs |    613.07 μs |    33.60 μs |  0.47 |    0.01 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,501.5 μs** |    **541.95 μs** |    **29.71 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,601.0 μs |    104.63 μs |     5.74 μs |  0.47 |    0.00 |        - |       - |     776 B |        0.65 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,495.5 μs** |    **123.50 μs** |     **6.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,619.5 μs |    429.24 μs |    23.53 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,502.1 μs** |    **206.01 μs** |    **11.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,600.6 μs |    126.28 μs |     6.92 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **130.5 μs** |    **261.5 μs** |  **14.33 μs** |  **1.01** |    **0.13** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   178.5 μs |    407.0 μs |  22.31 μs |  1.38 |    0.19 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **187.8 μs** |    **643.4 μs** |  **35.27 μs** |  **1.03** |    **0.25** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   224.2 μs |    655.9 μs |  35.95 μs |  1.23 |    0.28 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,278.5 μs** |  **5,251.2 μs** | **287.84 μs** |  **1.03** |    **0.28** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,303.3 μs |    553.4 μs |  30.33 μs |  1.05 |    0.20 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,694.2 μs** | **14,443.7 μs** | **791.71 μs** |  **1.13** |    **0.61** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,536.0 μs |    318.3 μs |  17.45 μs |  1.03 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **945.0 ns** | **1,144.3 ns** |  **62.72 ns** |  **1.00** |    **0.08** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,351.3 ns | 5,126.3 ns | 280.99 ns |  2.50 |    0.30 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,490.7 ns** | **1,932.2 ns** | **105.91 ns** |  **1.00** |    **0.09** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,752.7 ns | 2,859.4 ns | 156.73 ns |  2.53 |    0.19 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.115 μs |  4.1111 μs | 0.2253 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.681 μs | 43.7967 μs | 2.4006 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.166 μs |  1.1833 μs | 0.0649 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.099 μs |  1.6876 μs | 0.0925 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.154 μs |  5.3264 μs | 0.2920 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.591 μs |  2.0748 μs | 0.1137 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.817 μs |  7.0677 μs | 0.3874 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.130 μs |  5.7205 μs | 0.3136 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.982 μs |  4.1742 μs | 0.2288 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.662 μs | 80.2511 μs | 4.3988 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.615 μs | 53.4149 μs | 2.9279 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.631 μs | 62.0954 μs | 3.4037 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.036 μs |  1.6040 μs | 0.0879 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.558 μs |  0.3055 μs | 0.0167 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,900.19 ns | 1,331.262 ns | 72.971 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.84 ns |     0.340 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.07 ns |     2.410 ns |  0.132 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.70 ns |     6.741 ns |  0.370 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.29 ns |     7.248 ns |  0.397 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.73 ns |     0.746 ns |  0.041 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.30 ns |    24.072 ns |  1.319 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.60 ns |     3.185 ns |  0.175 ns |  0.69 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.113 μs |  35.126 μs | 1.9253 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 535.708 μs | 180.865 μs | 9.9138 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.648 μs |   6.340 μs | 0.3475 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.482 μs |  12.651 μs | 0.6935 μs |      80 B |


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