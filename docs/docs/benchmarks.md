---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 15:26 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,251.9 μs** |    **657.41 μs** |    **36.03 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,743.6 μs |    113.42 μs |     6.22 μs |  0.44 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,399.1 μs** |    **615.09 μs** |    **33.71 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,906.1 μs |  1,905.90 μs |   104.47 μs |  0.53 |    0.01 |  15.6250 |       - |  347170 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,297.9 μs** |    **747.38 μs** |    **40.97 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198700 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,508.7 μs |  6,966.44 μs |   381.85 μs |  0.56 |    0.05 |        - |       - |   37339 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,480.3 μs** |  **4,461.58 μs** |   **244.55 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,140.0 μs | 34,433.06 μs | 1,887.39 μs |  0.89 |    0.13 |        - |       - |  468901 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.6 μs** |    **249.56 μs** |    **13.68 μs** |  **1.01** |    **0.14** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    150.5 μs |     86.80 μs |     4.76 μs |  1.22 |    0.13 |        - |       - |    4090 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,331.6 μs** |    **381.27 μs** |    **20.90 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,187.0 μs |  2,456.61 μs |   134.65 μs |  0.89 |    0.09 |        - |       - |   41681 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,068.0 μs** |    **118.00 μs** |     **6.47 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125465 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    880.1 μs |    310.68 μs |    17.03 μs |  0.82 |    0.01 |        - |       - |    7202 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,765.6 μs** | **26,526.62 μs** | **1,454.01 μs** |  **1.02** |    **0.20** |  **74.2188** |       **-** | **1255330 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,313.7 μs | 14,533.57 μs |   796.63 μs |  0.97 |    0.15 |        - |       - |   61083 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,479.4 μs** |     **78.21 μs** |     **4.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,577.7 μs |     65.51 μs |     3.59 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,473.7 μs** |    **253.12 μs** |    **13.87 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,597.5 μs |    568.83 μs |    31.18 μs |  0.47 |    0.01 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,480.4 μs** |     **51.11 μs** |     **2.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,579.1 μs |     52.64 μs |     2.89 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,482.8 μs** |    **107.35 μs** |     **5.88 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,608.5 μs |    290.05 μs |    15.90 μs |  0.48 |    0.00 |        - |       - |     769 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **147.6 μs** |  **1,534.0 μs** |  **84.08 μs** |   **104.1 μs** |  **1.20** |    **0.77** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   149.7 μs |    226.6 μs |  12.42 μs |   142.9 μs |  1.21 |    0.46 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **178.0 μs** |  **1,291.5 μs** |  **70.79 μs** |   **139.4 μs** |  **1.09** |    **0.50** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   193.5 μs |    246.2 μs |  13.50 μs |   190.1 μs |  1.19 |    0.34 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,095.2 μs** |  **5,281.8 μs** | **289.52 μs** | **1,080.9 μs** |  **1.05** |    **0.35** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,430.2 μs |  2,414.5 μs | 132.35 μs | 1,495.2 μs |  1.37 |    0.34 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,507.7 μs** | **14,291.2 μs** | **783.35 μs** | **1,075.2 μs** |  **1.16** |    **0.68** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,477.6 μs |    405.0 μs |  22.20 μs | 1,465.8 μs |  1.14 |    0.39 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **922.3 ns** | **1,382.5 ns** |  **75.78 ns** |  **1.00** |    **0.10** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,182.6 ns | 2,531.0 ns | 138.73 ns |  2.38 |    0.22 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,442.2 ns** | **1,877.9 ns** | **102.94 ns** |  **1.00** |    **0.09** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,579.2 ns | 1,792.6 ns |  98.26 ns |  2.49 |    0.17 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.036 μs | 163.318 μs | 8.9520 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.240 μs |  10.982 μs | 0.6019 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 12.753 μs |   7.883 μs | 0.4321 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.331 μs |   7.193 μs | 0.3943 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.649 μs |  16.525 μs | 0.9058 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 20.372 μs |  27.909 μs | 1.5298 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 19.213 μs |  11.172 μs | 0.6124 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.093 μs |  10.033 μs | 0.5499 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.781 μs |   7.548 μs | 0.4137 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.907 μs |  30.237 μs | 1.6574 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.275 μs |  14.343 μs | 0.7862 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.496 μs |  28.992 μs | 1.5891 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  7.596 μs |  11.235 μs | 0.6158 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.501 μs |  22.445 μs | 1.2303 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,531.46 ns | 271.472 ns | 14.880 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.15 ns |   3.838 ns |  0.210 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.93 ns |   0.601 ns |  0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.14 ns |   0.318 ns |  0.017 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     40.80 ns |  19.106 ns |  1.047 ns |     ? |       ? | 0.0089 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     10.75 ns |   0.624 ns |  0.034 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    146.05 ns |  40.666 ns |  2.229 ns |  1.00 |    0.02 | 0.0355 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     84.10 ns |   0.522 ns |  0.029 ns |  0.58 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.359 μs |  27.886 μs |  1.5285 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 468.825 μs | 243.066 μs | 13.3232 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.942 μs |  14.983 μs |  0.8213 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 194.093 μs | 120.720 μs |  6.6171 μs |      80 B |


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