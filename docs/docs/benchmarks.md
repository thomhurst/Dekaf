---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 00:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,246.7 μs** |    **207.31 μs** |    **11.36 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,753.2 μs |    638.91 μs |    35.02 μs |  0.44 |    0.00 |        - |       - |   35164 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,349.4 μs** |  **1,426.46 μs** |    **78.19 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,966.5 μs |  2,672.38 μs |   146.48 μs |  0.54 |    0.02 |  15.6250 |       - |  347302 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,462.0 μs** |    **272.57 μs** |    **14.94 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,314.4 μs |  7,678.91 μs |   420.91 μs |  0.51 |    0.06 |        - |       - |   37283 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,996.5 μs** |  **4,294.80 μs** |   **235.41 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,338.6 μs |  9,803.65 μs |   537.37 μs |  1.11 |    0.04 |        - |       - |  475718 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **120.0 μs** |    **110.79 μs** |     **6.07 μs** |  **1.00** |    **0.06** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    124.2 μs |     49.72 μs |     2.73 μs |  1.04 |    0.05 |        - |       - |    4105 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,269.3 μs** |    **187.72 μs** |    **10.29 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,360.1 μs |  1,931.74 μs |   105.89 μs |  1.07 |    0.07 |        - |       - |   41626 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,031.9 μs** |     **86.85 μs** |     **4.76 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125440 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,020.0 μs |    884.23 μs |    48.47 μs |  0.99 |    0.04 |        - |       - |    6080 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,295.5 μs** | **31,209.71 μs** | **1,710.71 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1254781 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,974.1 μs | 12,810.55 μs |   702.19 μs |  0.99 |    0.19 |        - |       - |   61285 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,486.0 μs** |     **92.33 μs** |     **5.06 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,578.5 μs |    124.61 μs |     6.83 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,741.6 μs** |  **7,450.09 μs** |   **408.36 μs** |  **1.00** |    **0.09** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,598.0 μs |    130.13 μs |     7.13 μs |  0.45 |    0.03 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,488.4 μs** |      **2.81 μs** |     **0.15 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,588.4 μs |    208.83 μs |    11.45 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,491.7 μs** |     **14.72 μs** |     **0.81 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,589.3 μs |    102.74 μs |     5.63 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **118.3 μs** |    **430.7 μs** |  **23.61 μs** |  **1.03** |    **0.26** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   202.2 μs |    746.0 μs |  40.89 μs |  1.76 |    0.45 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **170.1 μs** |    **853.8 μs** |  **46.80 μs** |  **1.06** |    **0.40** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   183.7 μs |    189.1 μs |  10.37 μs |  1.15 |    0.33 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **959.6 μs** |  **3,148.7 μs** | **172.59 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,296.1 μs |  1,756.8 μs |  96.30 μs |  1.38 |    0.21 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,557.9 μs** | **11,886.2 μs** | **651.52 μs** |  **1.11** |    **0.55** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,869.5 μs | 11,854.9 μs | 649.81 μs |  1.34 |    0.60 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **857.5 ns** |   **209.5 ns** | **11.48 ns** |  **1.00** |    **0.02** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,182.7 ns |   392.8 ns | 21.53 ns |  2.55 |    0.04 |      - |     452 B |        0.70 |
|                      |             |            |            |          |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,447.1 ns** | **1,414.5 ns** | **77.53 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,476.8 ns | 1,230.9 ns | 67.47 ns |  2.41 |    0.12 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.115 μs |   4.0661 μs | 0.2229 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.026 μs |  42.7276 μs | 2.3420 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 13.053 μs |  30.0140 μs | 1.6452 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.125 μs |   0.6553 μs | 0.0359 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.910 μs |   2.4767 μs | 0.1358 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 14.707 μs |  57.9850 μs | 3.1784 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 15.569 μs |  77.0148 μs | 4.2214 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.717 μs | 177.2152 μs | 9.7138 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.018 μs |  62.5782 μs | 3.4301 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.287 μs |   1.7451 μs | 0.0957 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.964 μs |   4.9037 μs | 0.2688 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.471 μs |   5.1556 μs | 0.2826 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.790 μs |   0.2787 μs | 0.0153 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.537 μs |   2.4619 μs | 0.1349 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 16,088.78 ns | 129.672 ns | 7.108 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.250 ns | 0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.26 ns |   0.086 ns | 0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.00 ns |   1.475 ns | 0.081 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.79 ns |   2.998 ns | 0.164 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.277 ns | 0.015 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.85 ns |  24.761 ns | 1.357 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.69 ns |   0.212 ns | 0.012 ns |  0.70 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error        | StdDev      | Allocated |
|------------------------ |-----------:|-------------:|------------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.237 μs |    10.434 μs |   0.5719 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 600.203 μs | 2,614.364 μs | 143.3021 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.982 μs |     7.645 μs |   0.4190 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 263.080 μs |    54.574 μs |   2.9914 μs |      80 B |


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