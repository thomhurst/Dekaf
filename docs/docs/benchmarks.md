---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 20:42 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,312.4 μs** |    **110.47 μs** |     **6.06 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,884.3 μs |  1,901.53 μs |   104.23 μs |  0.30 |    0.01 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,458.7 μs** |    **272.46 μs** |    **14.93 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,416.7 μs |  2,893.75 μs |   158.62 μs |  0.46 |    0.02 |  15.6250 |       - |  345865 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,171.8 μs** |    **654.89 μs** |    **35.90 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,911.9 μs |    775.14 μs |    42.49 μs |  0.47 |    0.01 |        - |       - |   36034 B |        0.18 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,559.2 μs** |  **1,439.64 μs** |    **78.91 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 11,602.0 μs | 14,800.81 μs |   811.28 μs |  0.92 |    0.06 |        - |       - |  385287 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **131.4 μs** |     **23.12 μs** |     **1.27 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    187.8 μs |    952.34 μs |    52.20 μs |  1.43 |    0.34 |        - |       - |    4203 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,351.8 μs** |    **242.89 μs** |    **13.31 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,309.8 μs |  1,565.26 μs |    85.80 μs |  0.97 |    0.06 |        - |       - |   42046 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,085.2 μs** |     **71.07 μs** |     **3.90 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125497 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    898.6 μs |    700.83 μs |    38.41 μs |  0.83 |    0.03 |        - |       - |    7707 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,937.7 μs** | **31,081.14 μs** | **1,703.66 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1255765 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,475.0 μs |  6,718.17 μs |   368.25 μs |  0.87 |    0.15 |        - |       - |   82584 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,516.2 μs** |    **208.75 μs** |    **11.44 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,544.9 μs |     84.20 μs |     4.62 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,506.5 μs** |    **145.73 μs** |     **7.99 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,549.2 μs |    171.35 μs |     9.39 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,534.9 μs** |    **330.34 μs** |    **18.11 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,555.4 μs |    178.12 μs |     9.76 μs |  0.28 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,508.2 μs** |    **117.35 μs** |     **6.43 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,577.1 μs |     81.63 μs |     4.47 μs |  0.29 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **149.3 μs** |    **665.9 μs** |  **36.50 μs** |  **1.04** |    **0.29** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   202.5 μs |    965.5 μs |  52.92 μs |  1.41 |    0.42 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **159.0 μs** |    **800.8 μs** |  **43.90 μs** |  **1.05** |    **0.34** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   208.3 μs |    411.6 μs |  22.56 μs |  1.37 |    0.32 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **965.5 μs** |  **3,689.4 μs** | **202.23 μs** |  **1.03** |    **0.25** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,518.4 μs |  6,509.1 μs | 356.78 μs |  1.61 |    0.42 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,677.5 μs** | **11,366.6 μs** | **623.04 μs** |  **1.11** |    **0.53** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,441.5 μs |    227.6 μs |  12.47 μs |  0.95 |    0.33 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **946.4 ns** | **2,099.3 ns** | **115.07 ns** |  **1.01** |    **0.15** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,025.8 ns |   335.3 ns |  18.38 ns |  2.16 |    0.23 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,528.1 ns** | **2,208.0 ns** | **121.03 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,510.5 ns | 9,853.5 ns | 540.10 ns |  2.31 |    0.34 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 23.525 μs |   8.786 μs |  0.4816 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 13.196 μs |  14.687 μs |  0.8051 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.726 μs |  19.994 μs |  1.0959 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 10.532 μs |  19.899 μs |  1.0908 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.182 μs |  13.148 μs |  0.7207 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 11.498 μs |   8.371 μs |  0.4588 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 11.795 μs |  17.314 μs |  0.9490 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 40.548 μs |  16.766 μs |  0.9190 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 16.283 μs |   8.366 μs |  0.4586 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 12.851 μs |   7.202 μs |  0.3948 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 52.346 μs | 206.596 μs | 11.3242 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 54.728 μs | 174.678 μs |  9.5747 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.533 μs |  31.863 μs |  1.7465 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.339 μs |  27.261 μs |  1.4943 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error         | StdDev      | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|--------------:|------------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,437.569 ns | 2,249.5009 ns | 123.3027 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |               |             |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     12.303 ns |     2.8458 ns |   0.1560 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     15.121 ns |     5.4495 ns |   0.2987 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     31.486 ns |     7.2975 ns |   0.4000 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     33.568 ns |    10.8609 ns |   0.5953 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      7.632 ns |     0.2412 ns |   0.0132 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |               |             |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    111.339 ns |    75.3283 ns |   4.1290 ns |  1.00 |    0.04 | 0.0106 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     74.499 ns |    26.8347 ns |   1.4709 ns |  0.67 |    0.02 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |   8.688 μs |  38.264 μs |  2.097 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 411.714 μs | 200.731 μs | 11.003 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.710 μs |  38.205 μs |  2.094 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 177.808 μs | 240.449 μs | 13.180 μs |      80 B |


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