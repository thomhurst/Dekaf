---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 17:45 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,216.4 μs** |    **581.15 μs** |    **31.85 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,829.8 μs |  2,522.91 μs |   138.29 μs |  0.29 |    0.02 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,427.8 μs** |    **498.28 μs** |    **27.31 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,518.8 μs |  2,687.16 μs |   147.29 μs |  0.47 |    0.02 |  15.6250 |       - |  346104 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,546.2 μs** |    **577.21 μs** |    **31.64 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,755.1 μs |    192.10 μs |    10.53 μs |  0.42 |    0.00 |        - |       - |   36019 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,010.5 μs** |  **3,335.28 μs** |   **182.82 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,858.9 μs | 11,355.27 μs |   622.42 μs |  0.90 |    0.05 |        - |       - |  377209 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.7 μs** |      **9.71 μs** |     **0.53 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    138.2 μs |    147.70 μs |     8.10 μs |  1.07 |    0.05 |        - |       - |    4151 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,322.5 μs** |    **156.48 μs** |     **8.58 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,396.7 μs |  2,485.69 μs |   136.25 μs |  1.06 |    0.09 |        - |       - |   41858 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,017.3 μs** |     **48.62 μs** |     **2.67 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125405 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    784.3 μs |    258.66 μs |    14.18 μs |  0.77 |    0.01 |        - |       - |    6778 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,157.1 μs** | **29,214.39 μs** | **1,601.34 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1254507 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,039.8 μs | 12,918.04 μs |   708.08 μs |  0.90 |    0.17 |        - |       - |   97042 B |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,451.7 μs** |     **59.91 μs** |     **3.28 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,491.8 μs |     34.86 μs |     1.91 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **6,008.4 μs** | **17,254.17 μs** |   **945.76 μs** |  **1.02** |    **0.19** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,500.2 μs |     63.74 μs |     3.49 μs |  0.25 |    0.03 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,469.3 μs** |    **135.69 μs** |     **7.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,494.0 μs |     37.11 μs |     2.03 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,459.9 μs** |    **109.56 μs** |     **6.01 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,530.9 μs |    201.12 μs |    11.02 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **116.1 μs** |    **600.06 μs** |  **32.89 μs** |   **100.0 μs** |  **1.05** |    **0.34** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   199.8 μs |    102.69 μs |   5.63 μs |   197.2 μs |  1.80 |    0.39 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **138.3 μs** |    **656.43 μs** |  **35.98 μs** |   **119.5 μs** |  **1.04** |    **0.31** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   222.3 μs |    766.38 μs |  42.01 μs |   233.8 μs |  1.67 |    0.43 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **943.9 μs** |  **3,094.68 μs** | **169.63 μs** |   **847.0 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,212.8 μs |     98.30 μs |   5.39 μs | 1,210.4 μs |  1.31 |    0.18 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,474.7 μs** | **13,037.13 μs** | **714.61 μs** | **1,085.6 μs** |  **1.14** |    **0.63** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,182.9 μs | 11,847.37 μs | 649.39 μs | 2,543.9 μs |  1.69 |    0.72 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **837.2 ns** |   **871.4 ns** |  **47.76 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,032.5 ns |   618.4 ns |  33.90 ns |  2.43 |    0.12 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,394.2 ns** |   **888.6 ns** |  **48.71 ns** |  **1.00** |    **0.04** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,160.4 ns | 2,491.1 ns | 136.55 ns |  2.27 |    0.11 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.780 μs |   4.590 μs | 0.2516 μs | 32.844 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 16.017 μs | 166.098 μs | 9.1044 μs | 10.816 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.099 μs |   4.155 μs | 0.2278 μs |  8.062 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 10.616 μs |  35.771 μs | 1.9607 μs | 11.697 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.938 μs |   5.196 μs | 0.2848 μs | 14.070 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 11.955 μs |   4.501 μs | 0.2467 μs | 11.889 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 11.946 μs |   8.340 μs | 0.4571 μs | 11.703 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.619 μs |  32.244 μs | 1.7674 μs | 35.074 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.011 μs |   2.008 μs | 0.1101 μs | 10.024 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.097 μs |   3.940 μs | 0.2160 μs | 20.081 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.659 μs |  43.914 μs | 2.4071 μs | 31.527 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.745 μs |  51.896 μs | 2.8446 μs | 32.548 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.029 μs |  14.447 μs | 0.7919 μs |  4.638 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.054 μs |   7.557 μs | 0.4142 μs | 10.956 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,683.14 ns | 223.870 ns | 12.271 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.96 ns |   0.155 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     22.61 ns |   0.528 ns |  0.029 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.56 ns |   0.434 ns |  0.024 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.75 ns |   3.121 ns |  0.171 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     17.91 ns |   0.190 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    103.28 ns |  11.896 ns |  0.652 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     82.04 ns |   0.326 ns |  0.018 ns |  0.79 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.914 μs |  15.948 μs |  0.8742 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 518.726 μs | 165.611 μs |  9.0777 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.152 μs |   9.508 μs |  0.5212 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 223.774 μs | 349.651 μs | 19.1656 μs |      80 B |


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