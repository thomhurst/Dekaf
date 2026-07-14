---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 14:21 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,212.0 μs** |    **693.61 μs** |    **38.02 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,394.0 μs |    657.07 μs |    36.02 μs |  0.55 |    0.01 |        - |       - |   35193 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,400.5 μs** |    **571.51 μs** |    **31.33 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,743.8 μs |  4,236.75 μs |   232.23 μs |  0.51 |    0.03 |  15.6250 |       - |  347486 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,127.8 μs** |    **787.82 μs** |    **43.18 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,380.0 μs |  1,004.08 μs |    55.04 μs |  0.55 |    0.01 |        - |       - |   38039 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,077.2 μs** |  **4,984.28 μs** |   **273.21 μs** |  **1.00** |    **0.03** | **109.3750** | **78.1250** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,314.4 μs | 32,897.22 μs | 1,803.21 μs |  0.87 |    0.12 |  15.6250 |       - |  394665 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **137.8 μs** |     **10.81 μs** |     **0.59 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    126.4 μs |    119.30 μs |     6.54 μs |  0.92 |    0.04 |        - |       - |    4189 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,394.8 μs** |     **88.72 μs** |     **4.86 μs** |  **1.00** |    **0.00** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,836.2 μs |  7,461.28 μs |   408.98 μs |  1.32 |    0.25 |        - |       - |   43754 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,093.3 μs** |     **44.28 μs** |     **2.43 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125498 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    814.7 μs |    881.90 μs |    48.34 μs |  0.75 |    0.04 |        - |       - |    6683 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,971.4 μs** | **29,239.29 μs** | **1,602.70 μs** |  **1.02** |    **0.21** |  **74.2188** |       **-** | **1255765 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,427.6 μs |  9,737.84 μs |   533.76 μs |  0.76 |    0.13 |        - |       - |  107260 B |        0.09 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,429.9 μs** |    **217.84 μs** |    **11.94 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,441.3 μs |    188.58 μs |    10.34 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.0 μs** |     **82.14 μs** |     **4.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,435.2 μs |    321.31 μs |    17.61 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,419.3 μs** |     **84.83 μs** |     **4.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,445.1 μs |    118.09 μs |     6.47 μs |  0.64 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,434.4 μs** |    **325.32 μs** |    **17.83 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,437.5 μs |    131.63 μs |     7.21 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **149.5 μs** |    **743.4 μs** |  **40.75 μs** |   **163.6 μs** |  **1.06** |    **0.39** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   188.8 μs |    457.7 μs |  25.09 μs |   176.3 μs |  1.34 |    0.40 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **186.3 μs** |    **969.9 μs** |  **53.16 μs** |   **182.0 μs** |  **1.06** |    **0.38** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   192.5 μs |    681.6 μs |  37.36 μs |   183.3 μs |  1.09 |    0.33 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **937.4 μs** |  **3,357.8 μs** | **184.05 μs** |   **849.0 μs** |  **1.02** |    **0.24** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,245.1 μs |  2,108.9 μs | 115.60 μs | 1,191.2 μs |  1.36 |    0.24 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,631.1 μs** |  **9,577.7 μs** | **524.98 μs** | **1,554.0 μs** |  **1.07** |    **0.43** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,767.4 μs | 11,477.8 μs | 629.14 μs | 1,413.0 μs |  1.16 |    0.49 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **844.4 ns** |   **682.0 ns** |  **37.38 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,064.7 ns | 2,448.8 ns | 134.23 ns |  2.45 |    0.17 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,424.0 ns** | **1,574.4 ns** |  **86.30 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,276.2 ns | 3,587.8 ns | 196.66 ns |  2.31 |    0.17 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 40.765 μs | 236.7773 μs | 12.9786 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.063 μs |   3.1298 μs |  0.1716 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.196 μs |   4.1877 μs |  0.2295 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.911 μs |   3.2276 μs |  0.1769 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.093 μs |  17.2243 μs |  0.9441 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 15.552 μs |  49.2714 μs |  2.7007 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.351 μs |   5.1098 μs |  0.2801 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.982 μs |  13.4395 μs |  0.7367 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.402 μs |   0.5673 μs |  0.0311 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.529 μs |   8.1101 μs |  0.4445 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.731 μs |  13.5578 μs |  0.7431 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.653 μs |  37.5284 μs |  2.0571 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.803 μs |   6.2621 μs |  0.3432 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.956 μs |   5.3367 μs |  0.2925 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,033.65 ns | 110.710 ns | 6.068 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.83 ns |   0.535 ns | 0.029 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.88 ns |   3.637 ns | 0.199 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.01 ns |   2.624 ns | 0.144 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.05 ns |   4.946 ns | 0.271 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     13.06 ns |   0.623 ns | 0.034 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    103.46 ns |   9.366 ns | 0.513 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     78.14 ns |   0.873 ns | 0.048 ns |  0.76 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.019 μs |   4.351 μs |  0.2385 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 512.070 μs | 341.495 μs | 18.7185 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.822 μs |  10.999 μs |  0.6029 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 218.463 μs | 143.663 μs |  7.8746 μs |      80 B |


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