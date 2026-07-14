---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 21:59 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,130.7 μs** |    **548.93 μs** |    **30.09 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109098 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,362.8 μs |    438.39 μs |    24.03 μs |  0.55 |    0.00 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,321.9 μs** |    **619.15 μs** |    **33.94 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,611.8 μs |  3,764.70 μs |   206.36 μs |  0.49 |    0.02 |  15.6250 |       - |  347405 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,594.2 μs** |    **639.78 μs** |    **35.07 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198699 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,326.1 μs |    524.63 μs |    28.76 μs |  0.50 |    0.00 |        - |       - |   38041 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,924.7 μs** |  **2,074.16 μs** |   **113.69 μs** |  **1.00** |    **0.01** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,309.0 μs |  1,674.37 μs |    91.78 μs |  0.86 |    0.01 |        - |       - |  395103 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **148.2 μs** |     **21.11 μs** |     **1.16 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    122.2 μs |    157.33 μs |     8.62 μs |  0.82 |    0.05 |        - |       - |    4245 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,309.9 μs** |    **152.78 μs** |     **8.37 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,195.7 μs |    705.49 μs |    38.67 μs |  0.91 |    0.03 |        - |       - |   42111 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,016.9 μs** |     **25.31 μs** |     **1.39 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125393 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    769.5 μs |    645.74 μs |    35.40 μs |  0.76 |    0.03 |        - |       - |    8630 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,328.7 μs** | **30,229.04 μs** | **1,656.96 μs** |  **1.02** |    **0.24** |  **74.2188** |       **-** | **1254842 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,299.6 μs | 13,834.26 μs |   758.30 μs |  0.80 |    0.16 |        - |       - |   74409 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,398.3 μs** |     **78.31 μs** |     **4.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,411.9 μs |    328.41 μs |    18.00 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,400.7 μs** |     **25.04 μs** |     **1.37 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,426.2 μs |    317.58 μs |    17.41 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,403.7 μs** |     **48.63 μs** |     **2.67 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,431.6 μs |    437.40 μs |    23.98 μs |  0.64 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,399.6 μs** |     **60.74 μs** |     **3.33 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,410.5 μs |    135.94 μs |     7.45 μs |  0.63 |    0.00 |        - |       - |     805 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **151.5 μs** |    **618.5 μs** |  **33.90 μs** |  **1.04** |    **0.30** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   172.8 μs |    449.2 μs |  24.62 μs |  1.18 |    0.29 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **142.4 μs** |    **740.4 μs** |  **40.59 μs** |  **1.05** |    **0.35** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   202.5 μs |    670.9 μs |  36.78 μs |  1.49 |    0.40 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **927.9 μs** |  **3,045.0 μs** | **166.90 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,213.7 μs |    449.5 μs |  24.64 μs |  1.33 |    0.19 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,624.4 μs** | **11,393.2 μs** | **624.50 μs** |  **1.11** |    **0.53** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,800.7 μs | 11,513.8 μs | 631.11 μs |  1.23 |    0.56 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **894.1 ns** |   **694.0 ns** |  **38.04 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,043.0 ns | 2,011.4 ns | 110.25 ns |  2.29 |    0.14 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,476.7 ns** | **2,201.7 ns** | **120.68 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,277.2 ns | 3,357.2 ns | 184.02 ns |  2.23 |    0.19 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 28.717 μs |  68.9896 μs |  3.7816 μs | 26.810 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.981 μs |   3.4082 μs |  0.1868 μs | 10.951 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 18.889 μs | 251.7896 μs | 13.8014 μs | 13.375 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.462 μs |   0.9182 μs |  0.0503 μs |  8.455 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.003 μs |   2.1918 μs |  0.1201 μs | 11.010 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.718 μs |   4.9528 μs |  0.2715 μs | 12.655 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.124 μs |   7.5574 μs |  0.4142 μs | 13.244 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 28.450 μs |  41.0874 μs |  2.2521 μs | 27.371 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.184 μs |  66.6372 μs |  3.6526 μs | 15.287 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.524 μs |  59.6606 μs |  3.2702 μs | 26.840 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 22.151 μs |  54.2180 μs |  2.9719 μs | 20.608 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.365 μs |   4.0191 μs |  0.2203 μs | 22.352 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.386 μs |   9.9520 μs |  0.5455 μs |  5.199 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.770 μs |   5.4683 μs |  0.2997 μs | 11.906 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,894.75 ns | 257.669 ns | 14.124 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |   0.042 ns |  0.002 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.64 ns |   0.137 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.01 ns |   0.932 ns |  0.051 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.80 ns |   8.386 ns |  0.460 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.223 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    119.66 ns |  63.508 ns |  3.481 ns |  1.00 |    0.04 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.73 ns |   1.308 ns |  0.072 ns |  0.63 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error     | StdDev    | Allocated |
|------------------------ |-----------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.455 μs | 28.799 μs | 1.5786 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 502.315 μs | 84.859 μs | 4.6514 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.337 μs |  4.883 μs | 0.2676 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 231.007 μs | 18.207 μs | 0.9980 μs |      80 B |


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