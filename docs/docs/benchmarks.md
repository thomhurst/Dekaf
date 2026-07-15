---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 02:24 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean       | Error        | StdDev      | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----------:|-------------:|------------:|------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,043.0 μs** |    **436.63 μs** |    **23.93 μs** |  **1.00** |    **0.00** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 3,468.7 μs |  3,208.15 μs |   175.85 μs |  0.57 |    0.03 |       - |   35193 B |        0.32 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,449.3 μs** |    **327.95 μs** |    **17.98 μs** |  **1.00** |    **0.00** |       **-** | **1088292 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,595.7 μs |  3,313.72 μs |   181.64 μs |  0.48 |    0.02 |       - |  347375 B |        0.32 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,784.4 μs** |  **9,727.30 μs** |   **533.19 μs** |  **1.00** |    **0.09** |       **-** |  **198690 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 3,306.6 μs |    182.23 μs |     9.99 μs |  0.49 |    0.03 |       - |   38013 B |        0.19 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,969.9 μs** |  **1,386.07 μs** |    **75.98 μs** |  **1.00** |    **0.01** | **15.6250** | **1984295 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 7,681.6 μs |  7,360.49 μs |   403.45 μs |  0.96 |    0.04 |       - |  381094 B |        0.19 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **121.7 μs** |     **39.07 μs** |     **2.14 μs** |  **1.00** |    **0.02** |  **0.2441** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   118.8 μs |    457.52 μs |    25.08 μs |  0.98 |    0.18 |       - |    4182 B |        0.12 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,231.1 μs** |    **217.80 μs** |    **11.94 μs** |  **1.00** |    **0.01** |  **3.9063** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   997.0 μs |  2,836.79 μs |   155.49 μs |  0.81 |    0.11 |       - |   42836 B |        0.12 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **668.8 μs** |    **914.31 μs** |    **50.12 μs** |  **1.00** |    **0.09** |  **1.4648** |  **124746 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   538.8 μs |    905.48 μs |    49.63 μs |  0.81 |    0.08 |       - |    6376 B |        0.05 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,604.4 μs** | **20,250.26 μs** | **1,109.99 μs** |  **1.01** |    **0.18** | **13.6719** | **1248087 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 5,735.5 μs | 34,760.27 μs | 1,905.33 μs |  0.76 |    0.24 |       - |   78670 B |        0.06 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,465.6 μs** |  **3,124.39 μs** |   **171.26 μs** |  **1.00** |    **0.04** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 3,367.3 μs |    482.01 μs |    26.42 μs |  0.62 |    0.02 |       - |     800 B |        0.67 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,379.4 μs** |    **265.26 μs** |    **14.54 μs** |  **1.00** |    **0.00** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 3,533.7 μs |  3,075.93 μs |   168.60 μs |  0.66 |    0.03 |       - |     800 B |        0.67 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,428.2 μs** |  **2,541.29 μs** |   **139.30 μs** |  **1.00** |    **0.03** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 3,412.2 μs |    259.49 μs |    14.22 μs |  0.63 |    0.01 |       - |     800 B |        0.38 |
|                         |               |             |           |            |              |             |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,541.8 μs** |  **5,715.85 μs** |   **313.30 μs** |  **1.00** |    **0.07** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 3,406.4 μs |    373.92 μs |    20.50 μs |  0.62 |    0.03 |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **134.9 μs** |   **362.9 μs** |  **19.89 μs** |  **1.01** |    **0.18** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   183.1 μs |   357.1 μs |  19.58 μs |  1.38 |    0.21 |   39.98 KB |        0.62 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **188.0 μs** |   **696.6 μs** |  **38.18 μs** |  **1.03** |    **0.27** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   209.1 μs |   614.4 μs |  33.68 μs |  1.15 |    0.27 |  215.77 KB |        0.90 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,049.1 μs** | **2,389.2 μs** | **130.96 μs** |  **1.01** |    **0.15** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,041.2 μs |   976.0 μs |  53.50 μs |  1.00 |    0.12 |  476.66 KB |        0.73 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,549.4 μs** | **7,831.1 μs** | **429.25 μs** |  **1.05** |    **0.35** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,618.2 μs | 9,163.9 μs | 502.30 μs |  1.10 |    0.39 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **808.2 ns** | **1,824.1 ns** |  **99.98 ns** |  **1.01** |    **0.15** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,782.2 ns |   435.3 ns |  23.86 ns |  2.23 |    0.23 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,500.2 ns** | **1,062.3 ns** |  **58.23 ns** |  **1.00** |    **0.05** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,512.8 ns | 1,958.7 ns | 107.36 ns |  2.34 |    0.10 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.532 μs |  49.235 μs |  2.6988 μs | 26.009 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.756 μs |   2.391 μs |  0.1311 μs | 11.712 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.973 μs |   1.882 μs |  0.1032 μs |  8.946 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.417 μs |   7.078 μs |  0.3880 μs |  9.367 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.570 μs |  34.016 μs |  1.8645 μs | 11.632 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 17.199 μs |  70.793 μs |  3.8804 μs | 19.247 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.545 μs |   5.118 μs |  0.2805 μs | 13.565 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.068 μs |   2.779 μs |  0.1523 μs | 27.041 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.229 μs |   4.311 μs |  0.2363 μs |  9.312 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.551 μs |   2.899 μs |  0.1589 μs | 20.589 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.998 μs |   9.231 μs |  0.5060 μs | 21.275 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 23.033 μs |   9.274 μs |  0.5083 μs | 23.132 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.486 μs |   4.248 μs |  0.2328 μs |  5.400 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 25.145 μs | 316.874 μs | 17.3689 μs | 15.284 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 17,056.36 ns | 2,207.794 ns | 121.017 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.51 ns |     0.037 ns |   0.002 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.64 ns |     0.161 ns |   0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.21 ns |     1.230 ns |   0.067 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     34.45 ns |     5.756 ns |   0.315 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.91 ns |     0.183 ns |   0.010 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    125.82 ns |    74.907 ns |   4.106 ns |  1.00 |    0.04 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     81.40 ns |     2.475 ns |   0.136 ns |  0.65 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error     | StdDev    | Median    | Allocated |
|------------------------ |----------:|----------:|----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.51 μs |  10.77 μs |  0.591 μs |  12.20 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 537.96 μs | 292.38 μs | 16.026 μs | 542.45 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  20.28 μs | 254.76 μs | 13.964 μs |  12.94 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 263.70 μs |  62.38 μs |  3.419 μs | 261.89 μs |      80 B |


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