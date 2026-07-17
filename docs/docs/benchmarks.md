---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 11:43 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,208.9 μs** |    **202.24 μs** |    **11.09 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,692.6 μs |    149.28 μs |     8.18 μs |  0.43 |    0.00 |        - |       - |   35160 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,401.0 μs** |    **937.48 μs** |    **51.39 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,864.9 μs |  2,509.62 μs |   137.56 μs |  0.52 |    0.02 |  15.6250 |       - |  347256 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,560.4 μs** |    **395.04 μs** |    **21.65 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,332.5 μs |  7,248.90 μs |   397.34 μs |  0.51 |    0.05 |        - |       - |   37394 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,753.9 μs** |  **1,916.78 μs** |   **105.07 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,869.1 μs |  3,285.71 μs |   180.10 μs |  1.09 |    0.02 |  15.6250 |       - |  468695 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **126.4 μs** |     **35.22 μs** |     **1.93 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    140.4 μs |    157.51 μs |     8.63 μs |  1.11 |    0.06 |        - |       - |    4104 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,269.1 μs** |    **376.28 μs** |    **20.63 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,293.0 μs |    523.39 μs |    28.69 μs |  1.02 |    0.02 |        - |       - |   44479 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,005.2 μs** |     **83.12 μs** |     **4.56 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125376 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,119.3 μs |    922.52 μs |    50.57 μs |  1.11 |    0.04 |        - |       - |    6260 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,044.5 μs** | **32,191.53 μs** | **1,764.53 μs** |  **1.03** |    **0.26** |  **74.2188** |       **-** | **1254552 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 11,569.3 μs | 72,697.46 μs | 3,984.79 μs |  1.32 |    0.47 |        - |       - |   60962 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,453.8 μs** |     **61.14 μs** |     **3.35 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,551.7 μs |    166.00 μs |     9.10 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,456.1 μs** |     **66.58 μs** |     **3.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,568.2 μs |    122.94 μs |     6.74 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.64 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,465.4 μs** |     **21.64 μs** |     **1.19 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,564.4 μs |    118.74 μs |     6.51 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,459.1 μs** |     **68.55 μs** |     **3.76 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,578.1 μs |    231.11 μs |    12.67 μs |  0.47 |    0.00 |        - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **120.2 μs** |    **633.75 μs** |  **34.74 μs** |  **1.05** |    **0.37** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   196.2 μs |    514.71 μs |  28.21 μs |  1.72 |    0.46 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **136.7 μs** |    **589.44 μs** |  **32.31 μs** |  **1.03** |    **0.28** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   234.0 μs |    907.95 μs |  49.77 μs |  1.77 |    0.46 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,300.4 μs** |  **8,367.24 μs** | **458.64 μs** |  **1.10** |    **0.52** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,228.4 μs |     98.19 μs |   5.38 μs |  1.04 |    0.36 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,631.7 μs** | **10,955.24 μs** | **600.49 μs** |  **1.10** |    **0.53** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,465.0 μs |    286.59 μs |  15.71 μs |  0.99 |    0.34 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **950.3 ns** | **1,009.4 ns** |  **55.33 ns** |  **1.00** |    **0.07** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,114.4 ns | 2,326.5 ns | 127.52 ns |  2.23 |    0.16 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,341.2 ns** |   **663.7 ns** |  **36.38 ns** |  **1.00** |    **0.03** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,802.7 ns | 1,628.4 ns |  89.26 ns |  2.84 |    0.09 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 38.465 μs | 272.897 μs | 14.9584 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.649 μs |  40.424 μs |  2.2158 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.748 μs |  39.738 μs |  2.1782 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.432 μs |   4.344 μs |  0.2381 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.956 μs |   3.405 μs |  0.1866 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.546 μs |   8.029 μs |  0.4401 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.181 μs |   8.114 μs |  0.4448 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.201 μs |   1.770 μs |  0.0970 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.325 μs |   7.260 μs |  0.3979 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.282 μs |   3.552 μs |  0.1947 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 21.300 μs |  19.732 μs |  1.0816 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 28.303 μs |  14.513 μs |  0.7955 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.211 μs |   4.306 μs |  0.2360 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 15.666 μs |  21.728 μs |  1.1910 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,106.60 ns | 605.586 ns | 33.194 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.234 ns |  0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.34 ns |   1.360 ns |  0.075 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.96 ns |   0.140 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     37.75 ns |  14.590 ns |  0.800 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.76 ns |   0.943 ns |  0.052 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    130.23 ns | 134.142 ns |  7.353 ns |  1.00 |    0.07 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.16 ns |   5.847 ns |  0.320 ns |  0.58 |    0.03 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.29 μs |  10.969 μs |  0.601 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 525.38 μs | 323.751 μs | 17.746 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.12 μs |   6.532 μs |  0.358 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 229.18 μs |  11.568 μs |  0.634 μs |      80 B |


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