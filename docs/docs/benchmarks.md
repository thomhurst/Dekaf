---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 22:56 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean       | Error        | StdDev      | Ratio | RatioSD | Gen0    | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----------:|-------------:|------------:|------:|--------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,093.0 μs** |    **472.21 μs** |    **25.88 μs** |  **1.00** |    **0.01** |       **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 2,675.0 μs |    305.55 μs |    16.75 μs |  0.44 |    0.00 |       - |       - |   35160 B |        0.32 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,211.8 μs** |    **763.25 μs** |    **41.84 μs** |  **1.00** |    **0.01** | **31.2500** | **15.6250** | **1088298 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 4,041.3 μs |  1,667.86 μs |    91.42 μs |  0.56 |    0.01 |  7.8125 |       - |  347141 B |        0.32 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,507.7 μs** |    **533.33 μs** |    **29.23 μs** |  **1.00** |    **0.01** |  **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 3,581.7 μs |  3,493.75 μs |   191.50 μs |  0.55 |    0.03 |       - |       - |   37800 B |        0.19 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,958.5 μs** |  **1,296.30 μs** |    **71.05 μs** |  **1.00** |    **0.01** | **78.1250** | **31.2500** | **1984309 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 9,093.9 μs |  5,764.88 μs |   315.99 μs |  1.02 |    0.03 | 15.6250 |       - |  468898 B |        0.24 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **116.6 μs** |     **86.20 μs** |     **4.73 μs** |  **1.00** |    **0.05** |  **1.3428** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   124.3 μs |    650.58 μs |    35.66 μs |  1.07 |    0.27 |       - |       - |    4161 B |        0.12 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,120.8 μs** |  **3,830.25 μs** |   **209.95 μs** |  **1.03** |    **0.25** | **13.6719** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      | 1,022.4 μs |  3,029.02 μs |   166.03 μs |  0.94 |    0.22 |       - |       - |   43660 B |        0.13 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **739.9 μs** |    **128.63 μs** |     **7.05 μs** |  **1.00** |    **0.01** |  **4.8828** |       **-** |  **124957 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   766.3 μs |  1,966.19 μs |   107.77 μs |  1.04 |    0.13 |       - |       - |    7775 B |        0.06 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,362.0 μs** |    **839.27 μs** |    **46.00 μs** |  **1.00** |    **0.01** | **48.8281** |       **-** | **1250096 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 6,611.1 μs | 18,478.69 μs | 1,012.88 μs |  0.90 |    0.12 |       - |       - |   65750 B |        0.05 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,465.5 μs** |    **498.45 μs** |    **27.32 μs** |  **1.00** |    **0.01** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 2,545.1 μs |    426.57 μs |    23.38 μs |  0.47 |    0.00 |       - |       - |     768 B |        0.64 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,469.1 μs** |    **233.15 μs** |    **12.78 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 2,521.6 μs |    249.12 μs |    13.65 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.64 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,453.8 μs** |    **167.07 μs** |     **9.16 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 2,546.7 μs |     25.82 μs |     1.42 μs |  0.47 |    0.00 |       - |       - |     768 B |        0.37 |
|                         |               |             |           |            |              |             |       |         |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,460.3 μs** |     **99.61 μs** |     **5.46 μs** |  **1.00** |    **0.00** |       **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 2,538.0 μs |     34.17 μs |     1.87 μs |  0.46 |    0.00 |       - |       - |     768 B |        0.37 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **165.1 μs** |   **904.3 μs** |  **49.57 μs** |  **1.07** |    **0.42** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   158.0 μs |   227.7 μs |  12.48 μs |  1.02 |    0.30 |   39.98 KB |        0.62 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **192.6 μs** | **1,049.9 μs** |  **57.55 μs** |  **1.07** |    **0.43** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   218.5 μs |   847.3 μs |  46.44 μs |  1.22 |    0.44 |  215.77 KB |        0.90 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,081.1 μs** | **1,620.1 μs** |  **88.80 μs** |  **1.00** |    **0.10** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,107.5 μs | 1,415.0 μs |  77.56 μs |  1.03 |    0.09 |  476.66 KB |        0.73 |
|                      |              |             |            |            |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,626.7 μs** | **9,917.9 μs** | **543.63 μs** |  **1.07** |    **0.42** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,762.2 μs | 6,774.8 μs | 371.35 μs |  1.16 |    0.37 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **826.8 ns** |   **898.4 ns** |  **49.25 ns** |  **1.00** |    **0.07** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,787.8 ns | 1,612.3 ns |  88.38 ns |  2.17 |    0.14 |     452 B |        0.70 |
|                      |             |            |            |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,442.2 ns** | **3,361.1 ns** | **184.23 ns** |  **1.01** |    **0.16** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,922.0 ns | 1,647.7 ns |  90.32 ns |  2.05 |    0.22 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.450 μs |  2.508 μs | 0.1375 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.742 μs | 34.992 μs | 1.9180 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 13.089 μs | 15.981 μs | 0.8760 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  9.682 μs | 48.087 μs | 2.6358 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 13.604 μs | 33.607 μs | 1.8421 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.550 μs |  2.485 μs | 0.1362 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.729 μs |  2.372 μs | 0.1300 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.334 μs |  3.902 μs | 0.2139 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 15.045 μs |  3.433 μs | 0.1881 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.331 μs |  3.940 μs | 0.2160 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.225 μs |  6.927 μs | 0.3797 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 22.683 μs |  4.606 μs | 0.2525 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.098 μs |  5.558 μs | 0.3047 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.651 μs |  2.644 μs | 0.1449 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 15,912.04 ns | 664.580 ns | 36.428 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.52 ns |   0.179 ns |  0.010 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.24 ns |   0.201 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     40.05 ns |   1.261 ns |  0.069 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     35.05 ns |  14.189 ns |  0.778 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.69 ns |   0.063 ns |  0.003 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    121.26 ns |  61.537 ns |  3.373 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.83 ns |  10.992 ns |  0.603 ns |  0.66 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.999 μs |   1.496 μs | 0.0820 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 522.310 μs | 131.655 μs | 7.2165 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.263 μs |   1.827 μs | 0.1002 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 231.401 μs | 136.207 μs | 7.4660 μs |      80 B |


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