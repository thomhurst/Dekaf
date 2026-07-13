---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 12:36 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,193.2 μs** |    **484.24 μs** |    **26.54 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,900.0 μs |  3,439.43 μs |   188.53 μs |  0.31 |    0.03 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,465.7 μs** |    **364.39 μs** |    **19.97 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,283.4 μs |  3,634.53 μs |   199.22 μs |  0.44 |    0.02 |  15.6250 |       - |  345998 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,647.5 μs** |    **358.08 μs** |    **19.63 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,735.5 μs |    226.74 μs |    12.43 μs |  0.41 |    0.00 |        - |       - |   36022 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,726.5 μs** |  **3,979.10 μs** |   **218.11 μs** |  **1.00** |    **0.02** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,402.0 μs | 10,885.43 μs |   596.67 μs |  0.89 |    0.05 |  15.6250 |       - |  379398 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **127.8 μs** |     **69.47 μs** |     **3.81 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    128.7 μs |     84.23 μs |     4.62 μs |  1.01 |    0.04 |        - |       - |    4149 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,326.5 μs** |    **424.32 μs** |    **23.26 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,643.9 μs |  2,611.75 μs |   143.16 μs |  1.24 |    0.10 |        - |       - |   42222 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **987.3 μs** |     **13.89 μs** |     **0.76 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125347 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    890.1 μs |    656.66 μs |    35.99 μs |  0.90 |    0.03 |        - |       - |    7998 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,821.0 μs** | **32,726.47 μs** | **1,793.85 μs** |  **1.03** |    **0.28** |  **74.2188** |       **-** | **1254171 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,143.2 μs |  9,517.43 μs |   521.68 μs |  0.95 |    0.20 |        - |       - |   79423 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,439.6 μs** |     **70.60 μs** |     **3.87 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,499.5 μs |    142.05 μs |     7.79 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,439.2 μs** |     **90.19 μs** |     **4.94 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,486.9 μs |    152.13 μs |     8.34 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,462.0 μs** |    **355.38 μs** |    **19.48 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,489.9 μs |    137.86 μs |     7.56 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,450.7 μs** |     **63.89 μs** |     **3.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,477.0 μs |     51.54 μs |     2.82 μs |  0.27 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **142.8 μs** |    **718.1 μs** |  **39.36 μs** |  **1.06** |    **0.40** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   178.2 μs |    759.1 μs |  41.61 μs |  1.33 |    0.47 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **158.4 μs** |    **374.9 μs** |  **20.55 μs** |  **1.01** |    **0.16** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   184.1 μs |    149.4 μs |   8.19 μs |  1.18 |    0.14 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **942.3 μs** |  **3,015.5 μs** | **165.29 μs** |  **1.02** |    **0.21** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,182.7 μs |    375.2 μs |  20.57 μs |  1.28 |    0.18 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,636.6 μs** | **11,222.3 μs** | **615.13 μs** |  **1.10** |    **0.52** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,141.4 μs | 11,457.9 μs | 628.05 μs |  1.44 |    0.61 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **910.4 ns** |   **744.9 ns** |  **40.83 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,249.9 ns | 8,591.2 ns | 470.91 ns |  2.47 |    0.46 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,453.7 ns** | **2,650.8 ns** | **145.30 ns** |  **1.01** |    **0.12** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,177.5 ns | 2,540.3 ns | 139.24 ns |  2.20 |    0.21 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.061 μs |  41.4280 μs | 2.2708 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.116 μs |   1.0946 μs | 0.0600 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.720 μs |   1.8462 μs | 0.1012 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.727 μs |   7.3708 μs | 0.4040 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.160 μs |   2.1984 μs | 0.1205 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.058 μs |   0.7011 μs | 0.0384 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.108 μs |   8.8766 μs | 0.4866 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.014 μs |   0.4605 μs | 0.0252 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.373 μs |  62.2779 μs | 3.4137 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.706 μs |   5.4921 μs | 0.3010 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 38.746 μs | 179.6254 μs | 9.8459 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.639 μs |  23.4505 μs | 1.2854 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  6.368 μs |   5.2742 μs | 0.2891 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.110 μs |  27.0284 μs | 1.4815 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,555.79 ns | 297.596 ns | 16.312 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.56 ns |   0.355 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.26 ns |   1.730 ns |  0.095 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.87 ns |   2.898 ns |  0.159 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     37.53 ns |   3.221 ns |  0.177 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.81 ns |   0.087 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    130.48 ns |  96.965 ns |  5.315 ns |  1.00 |    0.05 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.01 ns |   0.906 ns |  0.050 ns |  0.58 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  17.953 μs |  62.690 μs | 3.4362 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 538.663 μs | 159.458 μs | 8.7404 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.451 μs |  15.735 μs | 0.8625 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 250.460 μs |  81.414 μs | 4.4626 μs |      80 B |


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