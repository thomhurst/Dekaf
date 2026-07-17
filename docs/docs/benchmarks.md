---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 02:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,145.3 μs** |    **444.05 μs** |    **24.34 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109097 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,706.9 μs |    511.67 μs |    28.05 μs |  0.44 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,224.5 μs** |    **636.04 μs** |    **34.86 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,952.6 μs |    910.15 μs |    49.89 μs |  0.55 |    0.01 |  15.6250 |       - |  347269 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,678.7 μs** |    **767.72 μs** |    **42.08 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,055.2 μs |  3,645.79 μs |   199.84 μs |  0.46 |    0.03 |        - |       - |   37828 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,998.7 μs** |  **3,681.19 μs** |   **201.78 μs** |  **1.00** |    **0.02** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 15,634.4 μs | 58,998.40 μs | 3,233.90 μs |  1.30 |    0.23 |  15.6250 |       - |  470428 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **112.6 μs** |     **30.37 μs** |     **1.66 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    131.5 μs |    148.82 μs |     8.16 μs |  1.17 |    0.06 |        - |       - |    4140 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,108.7 μs** |  **2,829.57 μs** |   **155.10 μs** |  **1.01** |    **0.17** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,305.5 μs |    144.63 μs |     7.93 μs |  1.19 |    0.15 |        - |       - |   42888 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **964.8 μs** |     **32.32 μs** |     **1.77 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125371 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,088.1 μs |    898.33 μs |    49.24 μs |  1.13 |    0.04 |        - |       - |    6195 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,755.2 μs** | **27,189.05 μs** | **1,490.32 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1253935 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,517.1 μs |  2,020.23 μs |   110.74 μs |  1.11 |    0.18 |        - |       - |   61066 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,460.3 μs** |    **461.58 μs** |    **25.30 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,522.3 μs |    189.89 μs |    10.41 μs |  0.46 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,451.8 μs** |    **456.64 μs** |    **25.03 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,508.2 μs |     84.11 μs |     4.61 μs |  0.46 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,478.7 μs** |    **102.39 μs** |     **5.61 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,501.4 μs |    105.78 μs |     5.80 μs |  0.46 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,453.4 μs** |    **171.68 μs** |     **9.41 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,507.5 μs |    111.94 μs |     6.14 μs |  0.46 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **125.0 μs** |    **725.6 μs** |  **39.77 μs** |   **109.2 μs** |  **1.06** |    **0.39** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   170.0 μs |    744.0 μs |  40.78 μs |   149.6 μs |  1.44 |    0.46 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **151.3 μs** |  **1,025.3 μs** |  **56.20 μs** |   **124.5 μs** |  **1.08** |    **0.46** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   195.1 μs |    600.6 μs |  32.92 μs |   178.5 μs |  1.40 |    0.43 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **847.5 μs** |  **3,673.1 μs** | **201.33 μs** |   **742.8 μs** |  **1.03** |    **0.29** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,102.7 μs |    213.5 μs |  11.70 μs | 1,106.2 μs |  1.35 |    0.24 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,476.9 μs** | **15,680.3 μs** | **859.49 μs** |   **987.8 μs** |  **1.20** |    **0.79** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,956.4 μs | 14,100.1 μs | 772.88 μs | 1,600.3 μs |  1.59 |    0.83 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **743.3 ns** | **1,711.3 ns** |  **93.80 ns** |  **1.01** |    **0.16** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,970.1 ns | 2,970.3 ns | 162.81 ns |  2.68 |    0.37 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,388.8 ns** |   **502.6 ns** |  **27.55 ns** |  **1.00** |    **0.02** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,636.5 ns | 6,408.8 ns | 351.29 ns |  2.62 |    0.22 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.261 μs |  0.3770 μs | 0.0207 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.026 μs | 40.3169 μs | 2.2099 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 12.996 μs |  3.8080 μs | 0.2087 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.261 μs |  4.6986 μs | 0.2575 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.327 μs |  0.7471 μs | 0.0410 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 18.886 μs |  7.1104 μs | 0.3897 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.640 μs |  5.4396 μs | 0.2982 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.091 μs | 25.1166 μs | 1.3767 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.159 μs |  0.2212 μs | 0.0121 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.381 μs | 66.1707 μs | 3.6270 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.481 μs |  2.9302 μs | 0.1606 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.547 μs |  0.7952 μs | 0.0436 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.324 μs |  6.9208 μs | 0.3794 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 15.239 μs |  7.0374 μs | 0.3857 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,446.61 ns | 169.361 ns | 9.283 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.85 ns |   0.612 ns | 0.034 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.26 ns |   0.172 ns | 0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.15 ns |   5.164 ns | 0.283 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.41 ns |   7.295 ns | 0.400 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.80 ns |   1.273 ns | 0.070 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    122.64 ns |  11.918 ns | 0.653 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.38 ns |   2.815 ns | 0.154 ns |  0.65 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.74 μs |  10.034 μs |  0.550 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 526.39 μs | 871.135 μs | 47.750 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.13 μs |   4.449 μs |  0.244 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.58 μs |  14.122 μs |  0.774 μs |      80 B |


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