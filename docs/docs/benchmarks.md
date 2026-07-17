---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-17 19:11 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,092.8 μs** |    **329.05 μs** |    **18.04 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,769.9 μs |  1,347.94 μs |    73.88 μs |  0.45 |    0.01 |        - |       - |   35184 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,222.9 μs** |  **1,434.74 μs** |    **78.64 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,830.4 μs |  2,150.30 μs |   117.87 μs |  0.53 |    0.01 |  15.6250 |       - |  347380 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,629.8 μs** |    **508.79 μs** |    **27.89 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,890.3 μs |  3,464.34 μs |   189.89 μs |  0.44 |    0.02 |        - |       - |   38024 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,196.8 μs** |  **6,067.82 μs** |   **332.60 μs** |  **1.00** |    **0.04** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,296.9 μs |  7,400.61 μs |   405.65 μs |  1.10 |    0.04 |  15.6250 |       - |  473297 B |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **121.8 μs** |      **5.05 μs** |     **0.28 μs** |  **1.00** |    **0.00** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    123.8 μs |     79.45 μs |     4.36 μs |  1.02 |    0.03 |        - |       - |    4108 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,098.8 μs** |  **2,294.85 μs** |   **125.79 μs** |  **1.01** |    **0.14** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,216.5 μs |    309.75 μs |    16.98 μs |  1.12 |    0.11 |        - |       - |   42952 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **926.6 μs** |     **84.51 μs** |     **4.63 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125274 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    983.4 μs |  1,301.65 μs |    71.35 μs |  1.06 |    0.07 |        - |       - |    6274 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,177.9 μs** | **29,383.80 μs** | **1,610.63 μs** |  **1.03** |    **0.27** |  **74.2188** |       **-** | **1253416 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,735.2 μs |  9,886.79 μs |   541.93 μs |  1.10 |    0.22 |        - |       - |   62539 B |        0.05 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,476.6 μs** |    **288.79 μs** |    **15.83 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,484.7 μs |     55.68 μs |     3.05 μs |  0.45 |    0.00 |        - |       - |     793 B |        0.66 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,469.3 μs** |    **475.55 μs** |    **26.07 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,483.1 μs |     50.22 μs |     2.75 μs |  0.45 |    0.00 |        - |       - |     792 B |        0.66 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,464.9 μs** |    **388.40 μs** |    **21.29 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,503.2 μs |    450.32 μs |    24.68 μs |  0.46 |    0.00 |        - |       - |     797 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,466.2 μs** |    **320.05 μs** |    **17.54 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,500.3 μs |    221.00 μs |    12.11 μs |  0.46 |    0.00 |        - |       - |     792 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **120.0 μs** |    **518.5 μs** |  **28.42 μs** |   **132.8 μs** |  **1.04** |    **0.33** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   176.3 μs |    483.3 μs |  26.49 μs |   183.1 μs |  1.53 |    0.42 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **161.4 μs** |    **816.7 μs** |  **44.76 μs** |   **174.7 μs** |  **1.06** |    **0.39** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   198.1 μs |    715.1 μs |  39.20 μs |   188.4 μs |  1.30 |    0.43 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **899.5 μs** |  **3,557.9 μs** | **195.02 μs** |   **878.6 μs** |  **1.03** |    **0.27** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,193.5 μs |  2,709.6 μs | 148.52 μs | 1,136.2 μs |  1.37 |    0.29 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,492.0 μs** | **16,219.5 μs** | **889.04 μs** |   **982.1 μs** |  **1.21** |    **0.81** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,665.1 μs |  7,225.5 μs | 396.06 μs | 1,456.1 μs |  1.35 |    0.60 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **707.3 ns** |    **357.8 ns** |  **19.61 ns** |  **1.00** |    **0.03** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,012.1 ns |  2,577.2 ns | 141.27 ns |  2.85 |    0.19 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,329.7 ns** |  **2,188.5 ns** | **119.96 ns** |  **1.01** |    **0.11** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,641.2 ns | 11,447.0 ns | 627.45 ns |  2.75 |    0.46 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.282 μs |  0.8480 μs | 0.0465 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.797 μs |  2.2618 μs | 0.1240 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.248 μs |  1.6994 μs | 0.0932 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.292 μs |  1.2171 μs | 0.0667 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.518 μs | 36.2530 μs | 1.9872 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.999 μs |  3.0138 μs | 0.1652 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.651 μs |  4.7716 μs | 0.2615 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.980 μs |  3.9509 μs | 0.2166 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.953 μs |  1.6040 μs | 0.0879 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.405 μs |  3.3734 μs | 0.1849 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.580 μs | 47.9234 μs | 2.6268 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.595 μs |  1.2125 μs | 0.0665 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.173 μs |  4.1118 μs | 0.2254 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 15.398 μs | 13.7187 μs | 0.7520 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 14,013.26 ns | 216.799 ns | 11.883 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.98 ns |   2.772 ns |  0.152 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.32 ns |   0.537 ns |  0.029 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.91 ns |   0.967 ns |  0.053 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     32.45 ns |  12.650 ns |  0.693 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.89 ns |   0.049 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    112.86 ns |  59.984 ns |  3.288 ns |  1.00 |    0.04 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.39 ns |   0.098 ns |  0.005 ns |  0.68 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  14.166 μs |   1.496 μs |  0.0820 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 517.015 μs | 683.375 μs | 37.4581 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.918 μs |   5.033 μs |  0.2759 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 246.931 μs | 181.611 μs |  9.9547 μs |      80 B |


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