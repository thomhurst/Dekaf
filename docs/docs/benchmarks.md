---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 11:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,621.4 μs** |    **165.10 μs** |     **9.05 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,920.1 μs |  2,904.66 μs |   159.21 μs |  0.29 |    0.02 |        - |       - |   35206 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,527.6 μs** |    **728.69 μs** |    **39.94 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,809.6 μs |  2,001.64 μs |   109.72 μs |  0.51 |    0.01 |  15.6250 |       - |  345857 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,242.1 μs** |  **1,194.78 μs** |    **65.49 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,827.0 μs |    299.26 μs |    16.40 μs |  0.45 |    0.00 |        - |       - |   36025 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,814.5 μs** |  **1,453.47 μs** |    **79.67 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,246.2 μs | 20,135.72 μs | 1,103.71 μs |  0.96 |    0.07 |        - |       - |  367840 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.2 μs** |     **33.08 μs** |     **1.81 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    155.0 μs |    565.77 μs |    31.01 μs |  1.15 |    0.20 |        - |       - |    4266 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,349.4 μs** |    **348.11 μs** |    **19.08 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,548.6 μs |  3,716.03 μs |   203.69 μs |  1.15 |    0.13 |        - |       - |   41930 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,080.7 μs** |     **30.50 μs** |     **1.67 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125492 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    956.6 μs |    499.73 μs |    27.39 μs |  0.89 |    0.02 |        - |       - |    7027 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,964.3 μs** | **27,964.93 μs** | **1,532.85 μs** |  **1.02** |    **0.20** |  **74.2188** |       **-** | **1255628 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,246.8 μs |  9,132.67 μs |   500.59 μs |  0.84 |    0.13 |        - |       - |   86664 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,517.6 μs** |     **93.32 μs** |     **5.11 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,533.0 μs |     45.79 μs |     2.51 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,528.2 μs** |    **140.05 μs** |     **7.68 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,526.2 μs |     16.77 μs |     0.92 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,600.5 μs** |    **363.05 μs** |    **19.90 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,538.1 μs |      7.88 μs |     0.43 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,613.0 μs** |    **381.18 μs** |    **20.89 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,534.2 μs |    154.10 μs |     8.45 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **112.0 μs** |    **239.8 μs** |  **13.14 μs** |   **109.4 μs** |  **1.01** |    **0.14** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   189.4 μs |    362.4 μs |  19.86 μs |   178.9 μs |  1.71 |    0.23 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **189.5 μs** |  **1,845.0 μs** | **101.13 μs** |   **134.1 μs** |  **1.17** |    **0.70** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   213.9 μs |    369.9 μs |  20.28 μs |   203.3 μs |  1.32 |    0.48 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,144.4 μs** |  **5,386.6 μs** | **295.26 μs** | **1,162.0 μs** |  **1.05** |    **0.34** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,353.9 μs |  2,564.1 μs | 140.55 μs | 1,285.5 μs |  1.24 |    0.31 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,499.0 μs** | **12,706.4 μs** | **696.48 μs** | **1,108.7 μs** |  **1.13** |    **0.59** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,309.7 μs | 10,842.6 μs | 594.32 μs | 2,634.7 μs |  1.74 |    0.68 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **912.6 ns** | **1,157.7 ns** |  **63.46 ns** |  **1.00** |    **0.09** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,166.4 ns | 2,963.8 ns | 162.45 ns |  2.38 |    0.21 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,527.4 ns** | **2,130.4 ns** | **116.78 ns** |  **1.00** |    **0.09** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,434.0 ns |   195.5 ns |  10.71 ns |  2.26 |    0.15 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.790 μs |  4.134 μs | 0.2266 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.046 μs |  4.108 μs | 0.2252 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.286 μs |  1.552 μs | 0.0850 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.286 μs |  3.193 μs | 0.1750 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.947 μs |  5.048 μs | 0.2767 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.336 μs |  3.749 μs | 0.2055 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.560 μs | 10.807 μs | 0.5924 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.890 μs |  1.770 μs | 0.0970 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 16.639 μs |  6.118 μs | 0.3354 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 28.740 μs | 23.796 μs | 1.3043 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.693 μs | 58.160 μs | 3.1880 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 35.131 μs | 63.161 μs | 3.4621 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.159 μs | 10.600 μs | 0.5810 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.695 μs | 16.362 μs | 0.8969 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,666.11 ns | 345.726 ns | 18.950 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.81 ns |   0.100 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.81 ns |   0.491 ns |  0.027 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.50 ns |   0.055 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.73 ns |   3.982 ns |  0.218 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.99 ns |   0.144 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    124.53 ns |  22.248 ns |  1.219 ns |  1.00 |    0.01 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     80.70 ns |   0.889 ns |  0.049 ns |  0.65 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.797 μs |  20.937 μs |  1.1476 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 522.026 μs | 219.853 μs | 12.0509 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.088 μs |   4.845 μs |  0.2656 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 222.196 μs | 244.036 μs | 13.3764 μs |      80 B |


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