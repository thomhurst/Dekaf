---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 03:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,210.8 μs** |    **836.46 μs** |    **45.85 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,377.5 μs |    237.86 μs |    13.04 μs |  0.54 |    0.00 |        - |       - |   35193 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,449.3 μs** |    **986.29 μs** |    **54.06 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,740.2 μs |  2,842.47 μs |   155.81 μs |  0.50 |    0.02 |  15.6250 |       - |  347465 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,222.2 μs** |    **957.90 μs** |    **52.51 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,367.2 μs |    659.18 μs |    36.13 μs |  0.54 |    0.01 |        - |       - |   38034 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,658.2 μs** |  **3,662.39 μs** |   **200.75 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,461.0 μs | 37,419.32 μs | 2,051.08 μs |  1.06 |    0.14 |        - |       - |  394801 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.0 μs** |     **74.55 μs** |     **4.09 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    115.3 μs |     93.27 μs |     5.11 μs |  0.87 |    0.04 |        - |       - |    4347 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,384.2 μs** |    **233.17 μs** |    **12.78 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,135.9 μs |  1,858.61 μs |   101.88 μs |  0.82 |    0.06 |        - |       - |   42710 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,082.1 μs** |    **105.87 μs** |     **5.80 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125472 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    779.0 μs |    498.28 μs |    27.31 μs |  0.72 |    0.02 |        - |       - |    8733 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,867.6 μs** | **30,658.50 μs** | **1,680.50 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1255460 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,740.8 μs | 11,055.59 μs |   605.99 μs |  0.80 |    0.14 |        - |       - |   98730 B |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,467.9 μs** |    **315.01 μs** |    **17.27 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,405.9 μs |     17.31 μs |     0.95 μs |  0.62 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,425.7 μs** |     **30.15 μs** |     **1.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,415.5 μs |    166.03 μs |     9.10 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,447.4 μs** |     **67.40 μs** |     **3.69 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,431.0 μs |    154.11 μs |     8.45 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,463.1 μs** |    **476.86 μs** |    **26.14 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,440.3 μs |    434.92 μs |    23.84 μs |  0.63 |    0.00 |        - |       - |     801 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **160.0 μs** |    **188.2 μs** |  **10.32 μs** |   **155.7 μs** |  **1.00** |    **0.08** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   160.9 μs |    382.5 μs |  20.96 μs |   154.5 μs |  1.01 |    0.13 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **145.6 μs** |    **839.9 μs** |  **46.04 μs** |   **120.8 μs** |  **1.06** |    **0.39** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   184.4 μs |    302.8 μs |  16.60 μs |   176.2 μs |  1.34 |    0.33 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **960.2 μs** |  **2,693.6 μs** | **147.65 μs** |   **890.6 μs** |  **1.01** |    **0.18** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,337.1 μs |  1,904.8 μs | 104.41 μs | 1,299.0 μs |  1.41 |    0.20 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,483.8 μs** | **12,993.5 μs** | **712.22 μs** | **1,087.6 μs** |  **1.14** |    **0.62** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,913.0 μs | 11,728.3 μs | 642.87 μs | 1,557.1 μs |  1.47 |    0.65 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **881.3 ns** |   **585.3 ns** |  **32.08 ns** |  **1.00** |    **0.04** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,151.8 ns | 2,035.7 ns | 111.58 ns |  2.44 |    0.13 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,493.2 ns** | **1,772.4 ns** |  **97.15 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,546.4 ns |   337.1 ns |  18.48 ns |  2.38 |    0.13 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.052 μs |  4.031 μs | 0.2210 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.291 μs |  1.095 μs | 0.0600 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 11.884 μs | 47.788 μs | 2.6194 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.415 μs |  1.425 μs | 0.0781 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 14.672 μs |  2.032 μs | 0.1114 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.048 μs |  5.213 μs | 0.2857 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 16.581 μs | 59.139 μs | 3.2416 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.191 μs |  2.083 μs | 0.1142 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.927 μs |  1.120 μs | 0.0614 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.162 μs |  2.588 μs | 0.1418 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.325 μs |  5.893 μs | 0.3230 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 27.488 μs |  2.955 μs | 0.1620 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.439 μs |  7.544 μs | 0.4135 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.275 μs |  3.270 μs | 0.1793 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 16,343.23 ns | 657.423 ns | 36.036 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.63 ns |   2.811 ns |  0.154 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.66 ns |   0.144 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.09 ns |   6.147 ns |  0.337 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.25 ns |   5.576 ns |  0.306 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.85 ns |   1.833 ns |  0.100 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.47 ns |  14.320 ns |  0.785 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     77.84 ns |   0.981 ns |  0.054 ns |  0.72 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Median     | Allocated |
|------------------------ |----------:|-----------:|----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.98 μs |   2.982 μs |  0.163 μs |  10.911 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 512.49 μs | 528.229 μs | 28.954 μs | 497.503 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  16.11 μs | 254.440 μs | 13.947 μs |   8.135 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 239.67 μs |  89.188 μs |  4.889 μs | 237.485 μs |      80 B |


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