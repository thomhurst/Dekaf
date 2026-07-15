---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 15:07 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,326.7 μs** |    **508.85 μs** |    **27.89 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109098 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,782.0 μs |    651.98 μs |    35.74 μs |  0.44 |    0.01 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,483.8 μs** |  **1,108.38 μs** |    **60.75 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,954.5 μs |    615.84 μs |    33.76 μs |  0.53 |    0.01 |  15.6250 |       - |  347188 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,206.9 μs** |    **885.46 μs** |    **48.54 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198699 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,220.8 μs |  7,611.32 μs |   417.20 μs |  0.52 |    0.06 |        - |       - |   37314 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,814.0 μs** |  **2,378.00 μs** |   **130.35 μs** |  **1.00** |    **0.01** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,163.1 μs |  7,226.85 μs |   396.13 μs |  0.95 |    0.03 |        - |       - |  467402 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.1 μs** |     **13.20 μs** |     **0.72 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    121.9 μs |     86.77 μs |     4.76 μs |  0.92 |    0.03 |        - |       - |    4173 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,328.8 μs** |    **330.25 μs** |    **18.10 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,185.1 μs |  3,352.80 μs |   183.78 μs |  0.89 |    0.12 |        - |       - |   41792 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,080.9 μs** |     **60.42 μs** |     **3.31 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125502 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    845.1 μs |    847.31 μs |    46.44 μs |  0.78 |    0.04 |        - |       - |    7293 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,974.9 μs** | **29,421.16 μs** | **1,612.67 μs** |  **1.02** |    **0.21** |  **74.2188** |       **-** | **1255879 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,308.5 μs | 14,729.49 μs |   807.37 μs |  0.75 |    0.14 |        - |       - |   78068 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,505.9 μs** |    **331.57 μs** |    **18.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,587.0 μs |    109.88 μs |     6.02 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,506.2 μs** |     **50.73 μs** |     **2.78 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,733.1 μs |  4,212.49 μs |   230.90 μs |  0.50 |    0.04 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,497.0 μs** |    **125.16 μs** |     **6.86 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,597.1 μs |    125.51 μs |     6.88 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,501.6 μs** |    **176.28 μs** |     **9.66 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,627.4 μs |    279.04 μs |    15.30 μs |  0.48 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **108.9 μs** |    **281.6 μs** |  **15.43 μs** |   **103.7 μs** |  **1.01** |    **0.17** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   194.8 μs |    309.0 μs |  16.93 μs |   185.6 μs |  1.81 |    0.25 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **156.7 μs** |    **736.6 μs** |  **40.37 μs** |   **144.3 μs** |  **1.04** |    **0.32** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   225.8 μs |  1,188.3 μs |  65.13 μs |   188.2 μs |  1.50 |    0.49 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **946.6 μs** |  **3,280.5 μs** | **179.81 μs** |   **861.2 μs** |  **1.02** |    **0.23** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,293.9 μs |  2,033.1 μs | 111.44 μs | 1,242.6 μs |  1.40 |    0.23 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,591.5 μs** | **14,736.5 μs** | **807.76 μs** | **1,154.4 μs** |  **1.15** |    **0.66** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,881.7 μs | 13,037.9 μs | 714.65 μs | 1,491.3 μs |  1.36 |    0.66 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **861.8 ns** |    **612.2 ns** |  **33.56 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,158.5 ns |    187.2 ns |  10.26 ns |  2.51 |    0.09 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,598.3 ns** |  **2,221.8 ns** | **121.78 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,870.2 ns | 10,854.8 ns | 594.99 ns |  2.43 |    0.36 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.206 μs |   2.356 μs |  0.1291 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.146 μs |  48.541 μs |  2.6607 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 10.966 μs |  48.868 μs |  2.6786 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.235 μs |   2.159 μs |  0.1183 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.923 μs |   2.640 μs |  0.1447 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.061 μs |  12.109 μs |  0.6637 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.222 μs |   8.873 μs |  0.4864 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 42.092 μs | 279.984 μs | 15.3469 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.930 μs |   2.018 μs |  0.1106 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.218 μs |   1.746 μs |  0.0957 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.544 μs |  42.947 μs |  2.3541 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 24.246 μs |  53.592 μs |  2.9375 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.896 μs |   4.081 μs |  0.2237 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.121 μs |   6.989 μs |  0.3831 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,946.22 ns | 614.372 ns | 33.676 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.82 ns |   0.164 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.26 ns |   0.270 ns |  0.015 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.94 ns |   2.409 ns |  0.132 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.46 ns |   6.491 ns |  0.356 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.088 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    115.31 ns |  64.900 ns |  3.557 ns |  1.00 |    0.04 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.46 ns |   0.259 ns |  0.014 ns |  0.69 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.324 μs |   3.568 μs |  0.1956 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 510.685 μs | 291.508 μs | 15.9785 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.310 μs |  16.879 μs |  0.9252 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 246.856 μs | 254.712 μs | 13.9616 μs |      80 B |


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