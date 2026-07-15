---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 14:06 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,888.32 μs** |    **144.76 μs** |   **7.935 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 2,550.00 μs |    474.23 μs |  25.994 μs |  0.43 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,082.07 μs** |    **375.01 μs** |  **20.556 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,730.58 μs |  2,378.28 μs | 130.361 μs |  0.53 |    0.02 |  15.6250 |       - |  347180 B |        0.32 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,389.89 μs** |    **203.84 μs** |  **11.173 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 2,805.32 μs |  1,722.96 μs |  94.441 μs |  0.44 |    0.01 |        - |       - |   37280 B |        0.19 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,922.81 μs** |  **1,094.32 μs** |  **59.984 μs** |  **1.00** |    **0.01** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 8,009.37 μs | 13,924.88 μs | 763.270 μs |  0.90 |    0.07 |  15.6250 |       - |  466564 B |        0.24 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **89.42 μs** |     **37.02 μs** |   **2.029 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    86.27 μs |    153.81 μs |   8.431 μs |  0.97 |    0.08 |   0.2441 |       - |    4224 B |        0.12 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **775.48 μs** |    **985.06 μs** |  **53.995 μs** |  **1.00** |    **0.08** |  **20.5078** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   870.73 μs |  2,479.71 μs | 135.921 μs |  1.13 |    0.17 |        - |       - |   43704 B |        0.13 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **775.62 μs** |  **1,183.52 μs** |  **64.873 μs** |  **1.00** |    **0.10** |   **7.3242** |       **-** |  **125611 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   654.24 μs |  1,105.73 μs |  60.609 μs |  0.85 |    0.09 |        - |       - |    8196 B |        0.07 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,636.68 μs** |  **2,278.72 μs** | **124.904 μs** |  **1.00** |    **0.02** |  **74.2188** |       **-** | **1251213 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 5,685.82 μs | 15,644.00 μs | 857.500 μs |  0.74 |    0.10 |        - |       - |   85629 B |        0.07 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,663.01 μs** |  **7,863.58 μs** | **431.029 μs** |  **1.00** |    **0.09** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 2,473.35 μs |    642.20 μs |  35.201 μs |  0.44 |    0.03 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,425.66 μs** |    **209.44 μs** |  **11.480 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 2,456.40 μs |    270.63 μs |  14.834 μs |  0.45 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,427.77 μs** |    **411.45 μs** |  **22.553 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 2,504.36 μs |    998.65 μs |  54.740 μs |  0.46 |    0.01 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,648.36 μs** |  **6,823.63 μs** | **374.026 μs** |  **1.00** |    **0.08** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 2,439.51 μs |    258.79 μs |  14.185 μs |  0.43 |    0.02 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **120.5 μs** |    **849.94 μs** |  **46.59 μs** |   **128.0 μs** |  **1.13** |    **0.60** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   148.8 μs |     62.01 μs |   3.40 μs |   147.6 μs |  1.39 |    0.55 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **137.8 μs** |    **369.28 μs** |  **20.24 μs** |   **131.0 μs** |  **1.01** |    **0.18** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   139.4 μs |    247.25 μs |  13.55 μs |   132.6 μs |  1.02 |    0.15 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **780.2 μs** |  **3,386.93 μs** | **185.65 μs** |   **856.6 μs** |  **1.05** |    **0.33** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,078.9 μs |  8,674.70 μs | 475.49 μs |   811.1 μs |  1.45 |    0.66 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,149.5 μs** | **12,190.44 μs** | **668.20 μs** |   **766.8 μs** |  **1.20** |    **0.79** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,463.3 μs | 10,884.26 μs | 596.60 μs | 1,126.6 μs |  1.53 |    0.81 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **581.8 ns** | **1,288.4 ns** |  **70.62 ns** |  **1.01** |    **0.15** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,474.2 ns | 2,487.2 ns | 136.33 ns |  2.56 |    0.35 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,037.9 ns** | **2,161.0 ns** | **118.45 ns** |  **1.01** |    **0.14** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,552.2 ns | 2,432.7 ns | 133.34 ns |  2.48 |    0.26 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.069 μs |  3.2241 μs | 0.1767 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.472 μs |  0.3798 μs | 0.0208 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.392 μs |  2.6157 μs | 0.1434 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.215 μs |  3.0353 μs | 0.1664 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.524 μs | 36.5739 μs | 2.0047 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.855 μs |  5.9226 μs | 0.3246 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.417 μs | 53.8606 μs | 2.9523 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.982 μs |  2.4499 μs | 0.1343 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.887 μs |  2.8672 μs | 0.1572 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 21.475 μs | 38.9423 μs | 2.1346 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.784 μs |  6.3954 μs | 0.3506 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.281 μs |  2.5345 μs | 0.1389 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.859 μs |  1.9108 μs | 0.1047 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.681 μs |  2.7386 μs | 0.1501 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,718.15 ns | 150.228 ns | 8.235 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.108 ns | 0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.30 ns |   0.369 ns | 0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     41.36 ns |   7.180 ns | 0.394 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.28 ns |   7.881 ns | 0.432 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.79 ns |   0.342 ns | 0.019 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.38 ns |  18.458 ns | 1.012 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.26 ns |   1.964 ns | 0.108 ns |  0.72 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error        | StdDev     | Median     | Allocated |
|------------------------ |----------:|-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.91 μs |     6.320 μs |   0.346 μs |  10.841 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 590.02 μs | 2,222.897 μs | 121.844 μs | 527.512 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  15.87 μs |   192.277 μs |  10.539 μs |   9.839 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 247.77 μs |   316.329 μs |  17.339 μs | 257.222 μs |      80 B |


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