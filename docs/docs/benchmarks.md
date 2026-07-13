---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 09:54 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,185.0 μs** |    **197.18 μs** |    **10.81 μs** |  **6,182.5 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,890.5 μs |  2,963.32 μs |   162.43 μs |  1,856.6 μs |  0.31 |    0.02 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,392.3 μs** |    **743.35 μs** |    **40.75 μs** |  **7,406.0 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,361.8 μs |  2,452.46 μs |   134.43 μs |  3,394.9 μs |  0.45 |    0.02 |  15.6250 |       - |  346726 B |        0.32 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,634.9 μs** |    **756.12 μs** |    **41.45 μs** |  **6,618.7 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,730.9 μs |    172.72 μs |     9.47 μs |  2,726.8 μs |  0.41 |    0.00 |        - |       - |   36028 B |        0.18 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,773.4 μs** |  **3,845.71 μs** |   **210.80 μs** | **11,787.6 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,873.8 μs | 16,216.50 μs |   888.88 μs | 11,629.1 μs |  1.01 |    0.07 |        - |       - |  366983 B |        0.18 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **125.9 μs** |     **58.67 μs** |     **3.22 μs** |    **125.4 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    222.8 μs |  3,266.46 μs |   179.05 μs |    126.0 μs |  1.77 |    1.23 |        - |       - |    4121 B |        0.12 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,244.8 μs** |    **393.54 μs** |    **21.57 μs** |  **1,236.9 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,540.6 μs |  4,834.93 μs |   265.02 μs |  1,676.4 μs |  1.24 |    0.19 |        - |       - |   42244 B |        0.12 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **988.5 μs** |    **207.76 μs** |    **11.39 μs** |    **982.9 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125332 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    853.7 μs |  1,741.31 μs |    95.45 μs |    805.0 μs |  0.86 |    0.08 |        - |       - |    6721 B |        0.05 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,376.8 μs** | **16,036.24 μs** |   **879.00 μs** |  **9,868.3 μs** |  **1.01** |    **0.12** |  **74.2188** |       **-** | **1254186 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,061.1 μs | 19,888.27 μs | 1,090.14 μs |  8,607.3 μs |  0.87 |    0.13 |        - |       - |   85550 B |        0.07 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,460.3 μs** |    **118.68 μs** |     **6.51 μs** |  **5,462.0 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,461.9 μs |    332.64 μs |    18.23 μs |  2,454.1 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,465.7 μs** |     **16.63 μs** |     **0.91 μs** |  **5,465.8 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,450.0 μs |     91.56 μs |     5.02 μs |  2,449.1 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,466.0 μs** |    **116.41 μs** |     **6.38 μs** |  **5,467.3 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,462.3 μs |     53.67 μs |     2.94 μs |  2,461.8 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,471.8 μs** |     **88.72 μs** |     **4.86 μs** |  **5,469.6 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,456.9 μs |     88.28 μs |     4.84 μs |  2,459.0 μs |  0.45 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **125.9 μs** |    **505.5 μs** |  **27.71 μs** |   **140.5 μs** |  **1.04** |    **0.30** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   181.2 μs |    778.8 μs |  42.69 μs |   189.1 μs |  1.49 |    0.45 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **160.8 μs** |    **726.3 μs** |  **39.81 μs** |   **177.1 μs** |  **1.05** |    **0.35** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   176.8 μs |    264.1 μs |  14.48 μs |   170.1 μs |  1.15 |    0.30 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **942.3 μs** |  **2,811.2 μs** | **154.09 μs** |   **862.6 μs** |  **1.02** |    **0.20** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,216.9 μs |  1,991.2 μs | 109.15 μs | 1,156.6 μs |  1.31 |    0.20 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,469.5 μs** | **13,184.1 μs** | **722.67 μs** | **1,072.4 μs** |  **1.14** |    **0.64** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,777.2 μs | 12,073.4 μs | 661.78 μs | 1,397.7 μs |  1.38 |    0.66 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|---------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **829.2 ns** |   **767.19 ns** | **42.05 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,020.5 ns |    67.98 ns |  3.73 ns |  2.44 |    0.11 |      - |     452 B |        0.70 |
|                      |             |            |             |          |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,467.5 ns** | **1,765.10 ns** | **96.75 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,277.7 ns | 1,455.92 ns | 79.80 ns |  2.24 |    0.14 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 28.224 μs |  47.917 μs |  2.6265 μs | 26.965 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.161 μs |   3.652 μs |  0.2002 μs | 11.111 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.820 μs |   3.604 μs |  0.1976 μs |  8.857 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.659 μs |   4.814 μs |  0.2639 μs |  8.786 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.418 μs |   2.659 μs |  0.1458 μs | 11.401 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.138 μs |   9.120 μs |  0.4999 μs | 12.945 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 17.163 μs |  63.749 μs |  3.4943 μs | 18.919 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.048 μs |   6.709 μs |  0.3677 μs | 26.921 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.896 μs |   2.631 μs |  0.1442 μs |  8.856 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 23.006 μs |  79.358 μs |  4.3499 μs | 20.718 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 36.398 μs |  50.700 μs |  2.7790 μs | 36.898 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 37.226 μs |  94.322 μs |  5.1701 μs | 36.148 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.166 μs |   4.515 μs |  0.2475 μs |  5.130 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 25.678 μs | 415.311 μs | 22.7646 μs | 13.767 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,210.44 ns | 460.926 ns | 25.265 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |   0.222 ns |  0.012 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.51 ns |   8.866 ns |  0.486 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.83 ns |   6.653 ns |  0.365 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     31.31 ns |   2.818 ns |  0.154 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.134 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    118.74 ns | 110.799 ns |  6.073 ns |  1.00 |    0.06 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.39 ns |   0.480 ns |  0.026 ns |  0.65 |    0.03 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error        | StdDev    | Allocated |
|------------------------ |-----------:|-------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.477 μs |    25.688 μs |  1.408 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 556.247 μs | 1,033.568 μs | 56.653 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.507 μs |    19.952 μs |  1.094 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 240.983 μs |    93.406 μs |  5.120 μs |      80 B |


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