---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 10:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,185.3 μs** |    **684.08 μs** |    **37.50 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,379.2 μs |    523.27 μs |    28.68 μs |  0.55 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,328.7 μs** |    **707.12 μs** |    **38.76 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,724.8 μs |  2,447.72 μs |   134.17 μs |  0.51 |    0.02 |  15.6250 |       - |  347439 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,509.8 μs** |  **1,519.57 μs** |    **83.29 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,325.6 μs |    486.51 μs |    26.67 μs |  0.51 |    0.01 |        - |       - |   37792 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,512.2 μs** |  **2,856.10 μs** |   **156.55 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,723.6 μs | 10,524.54 μs |   576.89 μs |  0.94 |    0.04 |  15.6250 |       - |  474230 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **151.8 μs** |     **35.29 μs** |     **1.93 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    120.2 μs |    127.35 μs |     6.98 μs |  0.79 |    0.04 |        - |       - |    4211 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,380.3 μs** |  **4,779.34 μs** |   **261.97 μs** |  **1.02** |    **0.24** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,207.4 μs |  3,410.24 μs |   186.93 μs |  0.90 |    0.19 |        - |       - |   42136 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,033.1 μs** |    **176.84 μs** |     **9.69 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125700 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    962.5 μs |    813.59 μs |    44.60 μs |  0.93 |    0.04 |        - |       - |    8622 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,733.1 μs** | **23,673.03 μs** | **1,297.60 μs** |  **1.01** |    **0.17** |  **74.2188** |       **-** | **1255117 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,871.8 μs | 11,705.60 μs |   641.62 μs |  0.82 |    0.12 |        - |       - |   75148 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,408.2 μs** |     **25.56 μs** |     **1.40 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,714.8 μs |  9,449.06 μs |   517.94 μs |  0.69 |    0.08 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,407.0 μs** |    **148.60 μs** |     **8.15 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,438.1 μs |    268.35 μs |    14.71 μs |  0.64 |    0.00 |        - |       - |     769 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.9 μs** |    **117.55 μs** |     **6.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,406.8 μs |    182.49 μs |    10.00 μs |  0.63 |    0.00 |        - |       - |     777 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,409.5 μs** |     **36.60 μs** |     **2.01 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,397.1 μs |    277.89 μs |    15.23 μs |  0.63 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **134.2 μs** |    **411.91 μs** |  **22.58 μs** |   **136.4 μs** |  **1.02** |    **0.22** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   198.5 μs |    126.86 μs |   6.95 μs |   201.6 μs |  1.51 |    0.23 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **164.5 μs** |    **740.72 μs** |  **40.60 μs** |   **186.7 μs** |  **1.05** |    **0.35** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   176.2 μs |     95.09 μs |   5.21 μs |   176.9 μs |  1.12 |    0.28 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,118.9 μs** |  **4,756.60 μs** | **260.73 μs** | **1,146.1 μs** |  **1.04** |    **0.31** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,233.8 μs |    261.62 μs |  14.34 μs | 1,230.9 μs |  1.15 |    0.25 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,464.0 μs** | **12,614.63 μs** | **691.45 μs** | **1,077.4 μs** |  **1.13** |    **0.61** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,837.8 μs | 12,050.07 μs | 660.50 μs | 1,461.1 μs |  1.42 |    0.65 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **845.3 ns** |   **799.1 ns** |  **43.80 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,022.2 ns |   215.3 ns |  11.80 ns |  2.40 |    0.11 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,463.2 ns** | **2,235.1 ns** | **122.51 ns** |  **1.00** |    **0.11** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,559.5 ns | 2,888.1 ns | 158.31 ns |  2.44 |    0.21 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 19.569 μs | 19.155 μs | 1.0500 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  6.791 μs | 11.545 μs | 0.6328 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  4.513 μs | 10.989 μs | 0.6024 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  5.761 μs | 11.122 μs | 0.6096 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      |  6.653 μs | 16.226 μs | 0.8894 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          |  8.569 μs | 27.110 μs | 1.4860 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     |  7.581 μs | 14.373 μs | 0.7878 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 21.319 μs |  2.229 μs | 0.1222 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.780 μs |  6.734 μs | 0.3691 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 10.751 μs |  6.878 μs | 0.3770 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 13.003 μs | 81.285 μs | 4.4555 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 12.555 μs | 69.355 μs | 3.8016 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  2.672 μs | 21.149 μs | 1.1592 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.042 μs | 50.161 μs | 2.7495 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 7,120.944 ns | 128.1882 ns | 7.0264 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     8.548 ns |   0.3908 ns | 0.0214 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    10.687 ns |   0.8901 ns | 0.0488 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    22.908 ns |   1.3680 ns | 0.0750 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    19.728 ns |   1.3127 ns | 0.0720 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     5.998 ns |   0.3787 ns | 0.0208 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    64.915 ns |  16.2939 ns | 0.8931 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |    47.837 ns |   0.5918 ns | 0.0324 ns |  0.74 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   5.250 μs |  35.416 μs |  1.941 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 266.140 μs | 293.340 μs | 16.079 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   4.139 μs |  25.198 μs |  1.381 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123.023 μs |  61.549 μs |  3.374 μs |      80 B |


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