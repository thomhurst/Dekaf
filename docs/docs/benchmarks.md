---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 17:32 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,177.0 μs** |    **404.23 μs** |    **22.16 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,552.1 μs |  3,207.40 μs |   175.81 μs |  0.25 |    0.02 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,242.7 μs** |  **2,207.77 μs** |   **121.02 μs** |  **1.00** |    **0.02** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,487.3 μs |  2,462.38 μs |   134.97 μs |  0.34 |    0.02 |  15.6250 |       - |  341.45 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,607.3 μs** |    **789.13 μs** |    **43.25 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,764.4 μs |  2,850.04 μs |   156.22 μs |  0.27 |    0.02 |        - |       - |   38.13 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,571.4 μs** |  **1,173.60 μs** |    **64.33 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,257.8 μs |  5,709.69 μs |   312.97 μs |  0.71 |    0.02 |  15.6250 |       - |  433.21 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.6 μs** |    **130.99 μs** |     **7.18 μs** |  **1.00** |    **0.07** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    112.0 μs |     70.18 μs |     3.85 μs |  0.92 |    0.05 |        - |       - |     4.3 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,198.1 μs** |    **355.41 μs** |    **19.48 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,235.7 μs |  6,661.49 μs |   365.14 μs |  1.03 |    0.26 |        - |       - |   44.13 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,000.5 μs** |     **25.99 μs** |     **1.42 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.46 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    738.6 μs |    554.25 μs |    30.38 μs |  0.74 |    0.03 |        - |       - |    7.85 KB |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,711.6 μs** | **36,150.59 μs** | **1,981.54 μs** |  **1.04** |    **0.32** |  **74.2188** |       **-** | **1224.88 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,238.3 μs |  7,428.27 μs |   407.17 μs |  0.87 |    0.20 |        - |       - |   77.42 KB |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,435.9 μs** |    **273.33 μs** |    **14.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,107.2 μs |     43.16 μs |     2.37 μs |  0.20 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.9 μs** |    **221.01 μs** |    **12.11 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,114.8 μs |    209.78 μs |    11.50 μs |  0.21 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,417.9 μs** |    **221.77 μs** |    **12.16 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,215.7 μs |    613.71 μs |    33.64 μs |  0.22 |    0.01 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,445.4 μs** |    **127.43 μs** |     **6.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,298.0 μs |    286.09 μs |    15.68 μs |  0.24 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **119.5 μs** |    **421.93 μs** |  **23.13 μs** |  **1.03** |    **0.25** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   205.1 μs |    394.32 μs |  21.61 μs |  1.76 |    0.36 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **144.7 μs** |    **759.27 μs** |  **41.62 μs** |  **1.05** |    **0.35** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   202.8 μs |  1,019.74 μs |  55.90 μs |  1.47 |    0.48 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,003.4 μs** |  **3,887.75 μs** | **213.10 μs** |  **1.04** |    **0.29** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,011.7 μs |     84.82 μs |   4.65 μs |  1.04 |    0.22 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,650.2 μs** | **14,305.80 μs** | **784.15 μs** |  **1.16** |    **0.68** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,372.3 μs |    128.10 μs |   7.02 μs |  0.96 |    0.38 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **759.8 ns** |  **1,736.4 ns** |  **95.18 ns** |  **1.01** |    **0.15** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,641.1 ns |    835.7 ns |  45.81 ns |  2.18 |    0.23 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,345.0 ns** |  **2,181.3 ns** | **119.57 ns** |  **1.01** |    **0.11** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,356.1 ns | 10,729.1 ns | 588.10 ns |  2.51 |    0.43 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.300 μs |   6.5511 μs |  0.3591 μs | 26.250 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.342 μs |   0.3872 μs |  0.0212 μs | 11.336 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 13.201 μs |   1.4633 μs |  0.0802 μs | 13.194 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.753 μs |   7.8533 μs |  0.4305 μs |  8.646 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.329 μs |   4.9269 μs |  0.2701 μs | 11.322 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.612 μs |   3.6956 μs |  0.2026 μs | 12.549 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 19.409 μs | 213.0268 μs | 11.6767 μs | 12.793 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 30.738 μs | 115.0847 μs |  6.3082 μs | 27.121 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 18.472 μs | 252.0639 μs | 13.8165 μs | 10.610 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.382 μs |   2.5273 μs |  0.1385 μs | 20.328 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 38.952 μs |  86.6394 μs |  4.7490 μs | 37.700 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 41.507 μs |  91.2126 μs |  4.9997 μs | 39.443 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.461 μs |   6.2889 μs |  0.3447 μs |  4.267 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.748 μs |  36.4983 μs |  2.0006 μs | 10.680 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,387.10 ns | 133.784 ns | 7.333 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.53 ns |   0.102 ns | 0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.72 ns |   1.199 ns | 0.066 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.42 ns |   1.628 ns | 0.089 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     35.85 ns |  11.250 ns | 0.617 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.76 ns |   0.049 ns | 0.003 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    156.26 ns |  71.409 ns | 3.914 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.56 ns |   0.252 ns | 0.014 ns |  0.49 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.973 μs |   7.000 μs |  0.3837 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 511.953 μs | 150.646 μs |  8.2574 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.511 μs |  17.997 μs |  0.9865 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 243.835 μs | 227.139 μs | 12.4503 μs |      80 B |


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