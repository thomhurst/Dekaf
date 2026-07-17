---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 06:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,287.5 μs** |    **367.09 μs** |    **20.12 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,739.4 μs |    583.13 μs |    31.96 μs |  0.44 |    0.00 |        - |       - |   35161 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,557.7 μs** |    **621.06 μs** |    **34.04 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,958.5 μs |  3,021.59 μs |   165.62 μs |  0.52 |    0.02 |  15.6250 |       - |  347372 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,152.7 μs** |     **95.68 μs** |     **5.24 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,724.9 μs |  5,243.08 μs |   287.39 μs |  0.61 |    0.04 |        - |       - |   37403 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,945.5 μs** | **10,850.79 μs** |   **594.77 μs** |  **1.00** |    **0.06** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,532.6 μs | 35,306.94 μs | 1,935.29 μs |  1.05 |    0.14 |  15.6250 |       - |  471338 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.9 μs** |     **25.54 μs** |     **1.40 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    138.8 μs |    161.08 μs |     8.83 μs |  1.04 |    0.06 |        - |       - |    4101 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,327.2 μs** |    **550.19 μs** |    **30.16 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,271.8 μs |  2,000.40 μs |   109.65 μs |  0.96 |    0.07 |        - |       - |   42000 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,060.7 μs** |     **30.88 μs** |     **1.69 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125440 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    866.2 μs |    926.76 μs |    50.80 μs |  0.82 |    0.04 |        - |       - |    7212 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,488.7 μs** | **31,745.82 μs** | **1,740.10 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1255086 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,585.2 μs | 19,696.31 μs | 1,079.62 μs |  1.14 |    0.23 |        - |       - |   61386 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,521.0 μs** |    **468.38 μs** |    **25.67 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,595.5 μs |    106.64 μs |     5.85 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,525.5 μs** |    **301.16 μs** |    **16.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,600.5 μs |    205.94 μs |    11.29 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,522.3 μs** |     **75.55 μs** |     **4.14 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,602.3 μs |     62.40 μs |     3.42 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,522.1 μs** |    **266.34 μs** |    **14.60 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,630.0 μs |    142.65 μs |     7.82 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **108.2 μs** |    **167.8 μs** |   **9.20 μs** |  **1.00** |    **0.10** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   206.0 μs |    324.9 μs |  17.81 μs |  1.91 |    0.20 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **174.2 μs** |    **925.2 μs** |  **50.71 μs** |  **1.05** |    **0.35** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   246.1 μs |    839.0 μs |  45.99 μs |  1.48 |    0.40 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,158.6 μs** |  **5,955.2 μs** | **326.43 μs** |  **1.06** |    **0.38** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,348.6 μs |  2,609.9 μs | 143.06 μs |  1.23 |    0.34 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,704.2 μs** | **11,163.1 μs** | **611.89 μs** |  **1.10** |    **0.51** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,024.4 μs | 10,752.5 μs | 589.38 μs |  1.30 |    0.55 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **951.1 ns** | **1,044.9 ns** |  **57.28 ns** |  **1.00** |    **0.08** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,326.6 ns | 3,042.0 ns | 166.74 ns |  2.45 |    0.20 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,504.6 ns** | **1,543.9 ns** |  **84.63 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,706.1 ns | 1,944.2 ns | 106.57 ns |  2.47 |    0.14 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.203 μs |   3.3145 μs |  0.1817 μs | 26.128 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.909 μs |  36.3041 μs |  1.9900 μs | 10.800 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 17.115 μs | 278.2278 μs | 15.2506 μs |  8.386 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.562 μs |  39.7359 μs |  2.1781 μs |  8.310 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.373 μs |  38.7853 μs |  2.1260 μs | 11.171 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.740 μs |   7.7189 μs |  0.4231 μs | 12.553 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.763 μs |  64.0847 μs |  3.5127 μs | 12.970 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.075 μs |   1.7337 μs |  0.0950 μs | 27.072 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.962 μs |   0.6502 μs |  0.0356 μs |  8.966 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.416 μs |   2.0275 μs |  0.1111 μs | 20.402 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.408 μs |   3.0083 μs |  0.1649 μs | 20.317 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.698 μs |   0.6320 μs |  0.0346 μs | 22.677 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.980 μs |   0.7297 μs |  0.0400 μs |  4.980 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.878 μs |   1.6318 μs |  0.0894 μs | 14.908 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,379.62 ns | 636.585 ns | 34.893 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.78 ns |   0.077 ns |  0.004 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.29 ns |   1.070 ns |  0.059 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.50 ns |   1.123 ns |  0.062 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.92 ns |   2.499 ns |  0.137 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.75 ns |   0.059 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    118.11 ns |  22.617 ns |  1.240 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     78.89 ns |   0.724 ns |  0.040 ns |  0.67 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.237 μs |  35.915 μs |  1.9686 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 527.744 μs | 125.943 μs |  6.9034 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.333 μs |   4.336 μs |  0.2377 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 254.955 μs | 295.331 μs | 16.1881 μs |      80 B |


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