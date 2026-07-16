---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-16 03:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,205.6 μs** |    **431.12 μs** |    **23.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,740.7 μs |    955.49 μs |    52.37 μs |  0.44 |    0.01 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,412.7 μs** |    **492.60 μs** |    **27.00 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1088566 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,901.7 μs |  2,736.39 μs |   149.99 μs |  0.53 |    0.02 |  15.6250 |       - |  347219 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,542.2 μs** |    **320.03 μs** |    **17.54 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,200.7 μs |  7,306.58 μs |   400.50 μs |  0.49 |    0.05 |        - |       - |   37336 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,913.8 μs** |    **335.88 μs** |    **18.41 μs** |  **1.00** |    **0.00** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,553.2 μs | 11,541.19 μs |   632.61 μs |  0.89 |    0.05 |  15.6250 |       - |  473045 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **128.9 μs** |     **20.34 μs** |     **1.11 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    119.6 μs |    206.78 μs |    11.33 μs |  0.93 |    0.08 |        - |       - |    4140 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,274.0 μs** |    **276.80 μs** |    **15.17 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,338.1 μs |  1,854.01 μs |   101.62 μs |  1.05 |    0.07 |   1.9531 |       - |   42422 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,001.4 μs** |     **97.55 μs** |     **5.35 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125367 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    843.3 μs |    860.20 μs |    47.15 μs |  0.84 |    0.04 |        - |       - |    8310 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,077.9 μs** | **28,612.02 μs** | **1,568.32 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1254354 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,026.3 μs |  2,254.47 μs |   123.57 μs |  0.90 |    0.15 |        - |       - |   63199 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,460.4 μs** |     **80.02 μs** |     **4.39 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,558.2 μs |     36.26 μs |     1.99 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,461.9 μs** |    **104.68 μs** |     **5.74 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,555.1 μs |     86.08 μs |     4.72 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,460.7 μs** |     **47.65 μs** |     **2.61 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,557.9 μs |     69.15 μs |     3.79 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,464.4 μs** |     **21.43 μs** |     **1.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,591.3 μs |    154.47 μs |     8.47 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **176.5 μs** | **1,248.2 μs** |  **68.42 μs** |  **1.12** |    **0.56** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   195.3 μs |   660.0 μs |  36.18 μs |  1.24 |    0.49 |   39.98 KB |        0.62 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **161.9 μs** |   **744.8 μs** |  **40.82 μs** |  **1.05** |    **0.36** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   198.2 μs |   710.4 μs |  38.94 μs |  1.29 |    0.40 |  215.77 KB |        0.90 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,114.3 μs** | **4,739.2 μs** | **259.77 μs** |  **1.04** |    **0.30** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,295.1 μs | 2,006.1 μs | 109.96 μs |  1.21 |    0.27 |  476.66 KB |        0.73 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,793.1 μs** | **6,877.6 μs** | **376.98 μs** |  **1.03** |    **0.26** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,415.1 μs |   139.8 μs |   7.66 μs |  0.81 |    0.14 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **829.3 ns** |   **442.9 ns** |  **24.28 ns** |  **1.00** |    **0.04** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,131.3 ns | 2,303.9 ns | 126.28 ns |  2.57 |    0.15 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,461.7 ns** | **2,164.7 ns** | **118.66 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,530.0 ns | 4,218.5 ns | 231.23 ns |  2.43 |    0.22 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.399 μs |  2.3392 μs | 0.1282 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.694 μs |  1.0048 μs | 0.0551 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.266 μs |  1.2061 μs | 0.0661 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.152 μs |  3.1980 μs | 0.1753 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.530 μs | 41.9545 μs | 2.2997 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.143 μs | 10.0693 μs | 0.5519 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.427 μs | 63.4748 μs | 3.4793 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.281 μs |  5.6373 μs | 0.3090 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.957 μs |  0.4793 μs | 0.0263 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.622 μs | 12.8825 μs | 0.7061 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.924 μs |  6.2779 μs | 0.3441 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.575 μs |  3.6228 μs | 0.1986 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.981 μs |  1.8739 μs | 0.1027 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.468 μs |  2.8110 μs | 0.1541 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 17,146.82 ns | 593.075 ns | 32.508 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.88 ns |   1.964 ns |  0.108 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.29 ns |   1.044 ns |  0.057 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.11 ns |   6.159 ns |  0.338 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.86 ns |   7.094 ns |  0.389 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.79 ns |   0.987 ns |  0.054 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    109.35 ns |  27.006 ns |  1.480 ns |  1.00 |    0.02 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     78.26 ns |   2.337 ns |  0.128 ns |  0.72 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.050 μs |   1.586 μs |  0.0870 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 532.480 μs | 598.603 μs | 32.8114 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.532 μs |   4.699 μs |  0.2575 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 239.751 μs |  83.252 μs |  4.5633 μs |      80 B |


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