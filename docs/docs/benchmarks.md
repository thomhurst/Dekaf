---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 14:48 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,262.4 μs** |    **272.72 μs** |    **14.95 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,717.3 μs |    540.91 μs |    29.65 μs |  0.43 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,399.3 μs** |    **655.00 μs** |    **35.90 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,860.5 μs |  1,915.44 μs |   104.99 μs |  0.52 |    0.01 |  15.6250 |       - |  347196 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,283.8 μs** |    **429.71 μs** |    **23.55 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,438.2 μs |  8,241.88 μs |   451.77 μs |  0.55 |    0.06 |        - |       - |   37499 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,565.5 μs** |  **2,028.75 μs** |   **111.20 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,746.3 μs | 14,747.46 μs |   808.36 μs |  1.01 |    0.06 |        - |       - |  469201 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.1 μs** |     **90.24 μs** |     **4.95 μs** |  **1.00** |    **0.05** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    150.7 μs |     53.45 μs |     2.93 μs |  1.19 |    0.04 |        - |       - |    4106 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,338.3 μs** |    **350.58 μs** |    **19.22 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,274.2 μs |  3,687.07 μs |   202.10 μs |  0.95 |    0.13 |        - |       - |   41668 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,059.3 μs** |    **123.67 μs** |     **6.78 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125465 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    867.9 μs |     29.65 μs |     1.63 μs |  0.82 |    0.00 |        - |       - |    5904 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,904.7 μs** | **24,648.37 μs** | **1,351.06 μs** |  **1.01** |    **0.18** |  **74.2188** |       **-** | **1255513 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,470.1 μs | 14,574.21 μs |   798.86 μs |  0.97 |    0.14 |        - |       - |   61144 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,485.9 μs** |     **54.00 μs** |     **2.96 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,596.9 μs |    185.16 μs |    10.15 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,723.8 μs** |  **7,188.64 μs** |   **394.03 μs** |  **1.00** |    **0.08** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,589.6 μs |    130.54 μs |     7.16 μs |  0.45 |    0.03 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,486.4 μs** |     **58.36 μs** |     **3.20 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,591.2 μs |    231.30 μs |    12.68 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,489.6 μs** |     **78.71 μs** |     **4.31 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,599.4 μs |    112.13 μs |     6.15 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **148.3 μs** |    **102.5 μs** |   **5.62 μs** |  **1.00** |    **0.05** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   192.4 μs |    231.0 μs |  12.66 μs |  1.30 |    0.09 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **172.7 μs** |    **875.7 μs** |  **48.00 μs** |  **1.06** |    **0.40** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   189.5 μs |    203.6 μs |  11.16 μs |  1.17 |    0.34 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **976.0 μs** |  **2,941.8 μs** | **161.25 μs** |  **1.02** |    **0.20** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,328.6 μs |  2,041.4 μs | 111.89 μs |  1.38 |    0.21 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,658.3 μs** | **11,005.5 μs** | **603.25 μs** |  **1.10** |    **0.52** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,530.0 μs |  1,038.2 μs |  56.91 μs |  1.02 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **870.3 ns** |   **871.3 ns** |  **47.76 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,163.8 ns | 2,413.5 ns | 132.29 ns |  2.49 |    0.18 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,460.5 ns** | **1,279.3 ns** |  **70.12 ns** |  **1.00** |    **0.06** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,886.3 ns | 1,955.2 ns | 107.17 ns |  2.66 |    0.13 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 30.500 μs |  2.6895 μs | 0.1474 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.673 μs |  3.9747 μs | 0.2179 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.349 μs |  0.7478 μs | 0.0410 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.290 μs |  3.9345 μs | 0.2157 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.258 μs |  4.3901 μs | 0.2406 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 14.934 μs | 37.4321 μs | 2.0518 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.817 μs |  3.4112 μs | 0.1870 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.052 μs |  3.6538 μs | 0.2003 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.859 μs |  1.7999 μs | 0.0987 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.284 μs |  2.1242 μs | 0.1164 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.990 μs |  6.9540 μs | 0.3812 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.678 μs |  4.4900 μs | 0.2461 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.082 μs |  1.1147 μs | 0.0611 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.249 μs |  6.0419 μs | 0.3312 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,640.37 ns | 115.254 ns | 6.317 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.81 ns |   0.045 ns | 0.002 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.35 ns |   2.953 ns | 0.162 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.91 ns |   3.466 ns | 0.190 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.86 ns |   3.993 ns | 0.219 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |   0.702 ns | 0.038 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.78 ns |   9.798 ns | 0.537 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     74.45 ns |   0.344 ns | 0.019 ns |  0.70 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.081 μs |   3.842 μs | 0.2106 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 530.834 μs |  63.150 μs | 3.4614 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.599 μs |  10.410 μs | 0.5706 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 233.654 μs | 153.847 μs | 8.4329 μs |      80 B |


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