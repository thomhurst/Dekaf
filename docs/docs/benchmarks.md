---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 23:38 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,330.2 μs** |    **497.75 μs** |    **27.28 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,778.4 μs |    824.67 μs |    45.20 μs |  0.28 |    0.01 |       - |       - |   35224 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,486.6 μs** |    **589.72 μs** |    **32.32 μs** |  **1.00** |    **0.01** | **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  4,083.9 μs |  5,483.18 μs |   300.55 μs |  0.55 |    0.03 | 15.6250 |       - |  348280 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,702.4 μs** | **16,391.41 μs** |   **898.47 μs** |  **1.01** |    **0.16** |  **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,755.3 μs |    562.86 μs |    30.85 μs |  0.26 |    0.03 |       - |       - |   38006 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **13,156.5 μs** |  **3,282.30 μs** |   **179.91 μs** |  **1.00** |    **0.02** | **93.7500** | **31.2500** | **1984317 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,511.5 μs | 10,735.90 μs |   588.47 μs |  0.80 |    0.04 |       - |       - |  372804 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.8 μs** |     **38.85 μs** |     **2.13 μs** |  **1.00** |    **0.02** |  **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    122.6 μs |    221.83 μs |    12.16 μs |  0.92 |    0.08 |       - |       - |    4259 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,463.7 μs** |  **2,066.97 μs** |   **113.30 μs** |  **1.00** |    **0.10** | **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,357.9 μs |    592.80 μs |    32.49 μs |  0.93 |    0.07 |       - |       - |   42624 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,097.1 μs** |     **88.96 μs** |     **4.88 μs** |  **1.00** |    **0.01** |  **7.3242** |       **-** |  **125534 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    882.4 μs |    987.92 μs |    54.15 μs |  0.80 |    0.04 |       - |       - |    8130 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,064.2 μs** | **24,766.84 μs** | **1,357.55 μs** |  **1.01** |    **0.18** | **74.2188** |       **-** | **1255711 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,344.6 μs |  9,718.29 μs |   532.69 μs |  0.84 |    0.12 |       - |       - |   80844 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,544.2 μs** |     **80.05 μs** |     **4.39 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,560.4 μs |    516.92 μs |    28.33 μs |  0.28 |    0.00 |       - |       - |     837 B |        0.70 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,547.9 μs** |    **116.24 μs** |     **6.37 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,545.7 μs |     56.19 μs |     3.08 μs |  0.28 |    0.00 |       - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,573.0 μs** |     **61.55 μs** |     **3.37 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,561.9 μs |    314.06 μs |    17.21 μs |  0.28 |    0.00 |       - |       - |     832 B |        0.40 |
|                         |               |             |           |             |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,625.3 μs** |  **1,731.48 μs** |    **94.91 μs** |  **1.00** |    **0.02** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,561.6 μs |    519.59 μs |    28.48 μs |  0.28 |    0.01 |       - |       - |     832 B |        0.40 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev      | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|------------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **138.7 μs** |    **872.6 μs** |    **47.83 μs** |   **119.6 μs** |  **1.07** |    **0.43** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   175.3 μs |    268.5 μs |    14.72 μs |   180.0 μs |  1.36 |    0.37 |   39.98 KB |        0.62 |
|                      |              |             |            |             |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **160.3 μs** |  **1,006.5 μs** |    **55.17 μs** |   **138.6 μs** |  **1.07** |    **0.43** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   193.8 μs |    532.9 μs |    29.21 μs |   182.9 μs |  1.30 |    0.38 |  215.77 KB |        0.90 |
|                      |              |             |            |             |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,128.5 μs** |  **4,785.6 μs** |   **262.32 μs** | **1,128.4 μs** |  **1.04** |    **0.30** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,204.4 μs |    193.0 μs |    10.58 μs | 1,203.5 μs |  1.11 |    0.23 |  476.66 KB |        0.73 |
|                      |              |             |            |             |             |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,813.9 μs** | **23,385.2 μs** | **1,281.82 μs** | **1,097.9 μs** |  **1.31** |    **1.04** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,810.9 μs | 12,086.8 μs |   662.52 μs | 1,446.6 μs |  1.31 |    0.72 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **857.4 ns** |   **688.1 ns** |  **37.72 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,041.3 ns |   546.4 ns |  29.95 ns |  2.38 |    0.10 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,436.9 ns** | **1,788.2 ns** |  **98.02 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,238.9 ns | 2,290.0 ns | 125.52 ns |  2.26 |    0.15 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 33.086 μs |  2.381 μs | 0.1305 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.305 μs |  3.802 μs | 0.2084 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  9.428 μs | 37.700 μs | 2.0665 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.962 μs |  3.809 μs | 0.2088 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.840 μs |  2.099 μs | 0.1151 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.325 μs |  3.296 μs | 0.1807 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 17.747 μs |  9.368 μs | 0.5135 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 37.446 μs |  1.860 μs | 0.1019 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.376 μs |  1.580 μs | 0.0866 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.401 μs |  5.620 μs | 0.3081 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 32.169 μs | 64.845 μs | 3.5543 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.662 μs | 13.938 μs | 0.7640 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.506 μs |  7.275 μs | 0.3988 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.613 μs |  5.431 μs | 0.2977 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,036.28 ns | 189.980 ns | 10.413 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.08 ns |   0.473 ns |  0.026 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.80 ns |   0.288 ns |  0.016 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.55 ns |   1.040 ns |  0.057 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.49 ns |   2.820 ns |  0.155 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     14.66 ns |   5.854 ns |  0.321 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    105.51 ns |  20.208 ns |  1.108 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.81 ns |   2.564 ns |  0.141 ns |  0.76 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.680 μs |   9.898 μs | 0.5425 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 507.820 μs |  27.241 μs | 1.4932 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.094 μs |  21.768 μs | 1.1932 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 220.113 μs | 170.945 μs | 9.3701 μs |      80 B |


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