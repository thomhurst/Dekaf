---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 12:58 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean       | Error        | StdDev    | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----------:|-------------:|----------:|------:|--------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,269.8 μs** |    **943.49 μs** |  **51.72 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 2,693.9 μs |    451.34 μs |  24.74 μs |  0.43 |    0.00 |       - |       - |   35160 B |        0.32 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,275.6 μs** |    **769.71 μs** |  **42.19 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1088298 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,985.1 μs |  2,308.13 μs | 126.52 μs |  0.55 |    0.02 |  7.8125 |       - |  347117 B |        0.32 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,599.4 μs** |    **249.32 μs** |  **13.67 μs** |  **1.00** |    **0.00** |  **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 3,717.3 μs |  2,193.69 μs | 120.24 μs |  0.56 |    0.02 |       - |       - |   37383 B |        0.19 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,158.0 μs** |  **4,314.37 μs** | **236.48 μs** |  **1.00** |    **0.03** | **78.1250** | **46.8750** | **1984309 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 9,414.2 μs | 10,228.30 μs | 560.65 μs |  1.03 |    0.06 | 15.6250 |       - |  465944 B |        0.23 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **119.2 μs** |     **59.68 μs** |   **3.27 μs** |  **1.00** |    **0.03** |  **1.3428** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   122.5 μs |    456.64 μs |  25.03 μs |  1.03 |    0.18 |       - |       - |    4180 B |        0.12 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,211.7 μs** |  **1,634.97 μs** |  **89.62 μs** |  **1.00** |    **0.09** | **13.6719** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      | 1,142.4 μs |  1,062.70 μs |  58.25 μs |  0.95 |    0.07 |       - |       - |   41725 B |        0.12 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **742.4 μs** |    **138.84 μs** |   **7.61 μs** |  **1.00** |    **0.01** |  **4.8828** |       **-** |  **124941 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   635.1 μs |  1,200.30 μs |  65.79 μs |  0.86 |    0.08 |       - |       - |    8047 B |        0.06 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,332.3 μs** |    **955.54 μs** |  **52.38 μs** |  **1.00** |    **0.01** | **48.8281** |       **-** | **1249859 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 6,665.4 μs | 15,349.47 μs | 841.36 μs |  0.91 |    0.10 |       - |       - |   71532 B |        0.06 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,451.4 μs** |    **219.82 μs** |  **12.05 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 2,489.6 μs |    119.26 μs |   6.54 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.64 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,453.6 μs** |     **33.30 μs** |   **1.83 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 2,511.5 μs |    165.95 μs |   9.10 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.64 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,463.2 μs** |    **318.26 μs** |  **17.45 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 2,504.2 μs |     94.36 μs |   5.17 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.37 |
|                         |               |             |           |            |              |           |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,463.3 μs** |    **327.18 μs** |  **17.93 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 2,487.2 μs |    193.14 μs |  10.59 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **142.9 μs** |   **400.1 μs** |  **21.93 μs** |  **1.02** |    **0.20** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   200.4 μs |   626.6 μs |  34.35 μs |  1.43 |    0.30 |   39.98 KB |        0.62 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **195.6 μs** |   **505.8 μs** |  **27.73 μs** |  **1.01** |    **0.17** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   204.3 μs |   585.8 μs |  32.11 μs |  1.06 |    0.19 |  215.77 KB |        0.90 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,092.2 μs** | **2,961.9 μs** | **162.35 μs** |  **1.01** |    **0.18** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   995.5 μs |   282.8 μs |  15.50 μs |  0.92 |    0.11 |  476.66 KB |        0.73 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,768.0 μs** | **9,774.8 μs** | **535.79 μs** |  **1.07** |    **0.41** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,577.8 μs | 8,683.2 μs | 475.95 μs |  0.95 |    0.37 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **900.8 ns** |   **859.5 ns** |  **47.11 ns** |  **1.00** |    **0.06** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,846.2 ns | 2,378.0 ns | 130.35 ns |  2.05 |    0.16 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,568.5 ns** | **2,605.1 ns** | **142.80 ns** |  **1.01** |    **0.11** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,025.4 ns |   708.2 ns |  38.82 ns |  1.94 |    0.15 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.895 μs |   4.473 μs |  0.2452 μs | 32.904 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.368 μs |  19.014 μs |  1.0422 μs | 12.818 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.811 μs |   1.559 μs |  0.0854 μs |  7.821 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.923 μs |   3.476 μs |  0.1906 μs |  8.873 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.909 μs |   2.961 μs |  0.1623 μs | 10.826 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 20.074 μs | 228.871 μs | 12.5452 μs | 13.251 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.010 μs |   3.750 μs |  0.2055 μs | 12.014 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 33.172 μs |   8.335 μs |  0.4569 μs | 33.055 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 14.169 μs |  62.657 μs |  3.4345 μs | 15.829 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.704 μs |  97.358 μs |  5.3365 μs | 29.475 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 19.650 μs |  33.168 μs |  1.8180 μs | 19.700 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.753 μs |  10.596 μs |  0.5808 μs | 21.663 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.783 μs |   8.109 μs |  0.4445 μs |  4.617 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.726 μs |   9.748 μs |  0.5343 μs | 14.652 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,100.99 ns | 2,384.081 ns | 130.680 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.19 ns |     0.797 ns |   0.044 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.71 ns |     0.113 ns |   0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.77 ns |     1.056 ns |   0.058 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.04 ns |     1.836 ns |   0.101 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     13.71 ns |     0.101 ns |   0.006 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |              |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    107.58 ns |    17.760 ns |   0.973 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     80.53 ns |     0.123 ns |   0.007 ns |  0.75 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.886 μs |   2.105 μs | 0.1154 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 503.471 μs |  57.313 μs | 3.1415 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.008 μs |   8.450 μs | 0.4632 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 226.017 μs | 146.984 μs | 8.0567 μs |      80 B |


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