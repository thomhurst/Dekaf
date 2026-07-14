---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 17:20 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **5,956.14 μs** |    **121.57 μs** |     **6.664 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 3,300.20 μs |    330.91 μs |    18.139 μs |  0.55 |    0.00 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,912.43 μs** |  **1,142.93 μs** |    **62.648 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,467.75 μs |  1,466.16 μs |    80.365 μs |  0.50 |    0.01 |  15.6250 |       - |  347466 B |        0.32 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,975.23 μs** | **12,341.75 μs** |   **676.493 μs** |  **1.01** |    **0.12** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 3,277.16 μs |    250.29 μs |    13.719 μs |  0.47 |    0.04 |        - |       - |   38018 B |        0.19 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,438.87 μs** |  **2,222.20 μs** |   **121.806 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 8,167.32 μs |  7,514.03 μs |   411.869 μs |  0.87 |    0.04 |  15.6250 |       - |  389266 B |        0.20 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **91.66 μs** |     **48.10 μs** |     **2.636 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   106.58 μs |    391.07 μs |    21.436 μs |  1.16 |    0.20 |        - |       - |    4301 B |        0.13 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **844.26 μs** |    **775.32 μs** |    **42.498 μs** |  **1.00** |    **0.06** |  **20.5078** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   888.99 μs |  1,011.18 μs |    55.426 μs |  1.05 |    0.07 |        - |       - |   42734 B |        0.12 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **785.08 μs** |    **420.94 μs** |    **23.073 μs** |  **1.00** |    **0.04** |   **7.3242** |       **-** |  **126143 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   551.01 μs |  1,224.84 μs |    67.138 μs |  0.70 |    0.08 |        - |       - |    8777 B |        0.07 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,966.71 μs** |  **7,475.04 μs** |   **409.732 μs** |  **1.00** |    **0.06** |  **74.2188** |       **-** | **1251819 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 5,937.09 μs | 21,775.77 μs | 1,193.604 μs |  0.75 |    0.13 |        - |       - |   76967 B |        0.06 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,332.39 μs** |     **14.66 μs** |     **0.803 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 3,471.72 μs |  4,991.57 μs |   273.605 μs |  0.65 |    0.04 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,669.65 μs** |  **7,688.23 μs** |   **421.418 μs** |  **1.00** |    **0.09** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 3,403.44 μs |    835.25 μs |    45.783 μs |  0.60 |    0.04 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,349.78 μs** |    **557.51 μs** |    **30.559 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 3,386.95 μs |    202.28 μs |    11.088 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |              |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,340.37 μs** |     **54.62 μs** |     **2.994 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 3,379.48 μs |    966.42 μs |    52.973 μs |  0.63 |    0.01 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **125.7 μs** |    **207.4 μs** |  **11.37 μs** |   **122.2 μs** |  **1.01** |    **0.11** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   122.9 μs |    322.2 μs |  17.66 μs |   113.8 μs |  0.98 |    0.14 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **159.7 μs** |    **838.4 μs** |  **45.96 μs** |   **152.5 μs** |  **1.06** |    **0.37** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   165.6 μs |    689.0 μs |  37.76 μs |   150.9 μs |  1.10 |    0.35 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **679.4 μs** |  **3,431.1 μs** | **188.07 μs** |   **578.5 μs** |  **1.05** |    **0.33** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   932.9 μs |  2,105.0 μs | 115.38 μs |   867.9 μs |  1.44 |    0.34 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,196.0 μs** | **11,821.8 μs** | **647.99 μs** |   **833.6 μs** |  **1.18** |    **0.72** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,139.2 μs |    438.0 μs |  24.01 μs | 1,127.2 μs |  1.12 |    0.40 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **646.0 ns** | **1,052.9 ns** |  **57.72 ns** |  **1.01** |    **0.11** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,448.0 ns | 2,892.8 ns | 158.56 ns |  2.25 |    0.28 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,079.7 ns** |   **727.7 ns** |  **39.89 ns** |  **1.00** |    **0.05** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,577.2 ns | 5,267.0 ns | 288.70 ns |  2.39 |    0.24 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.744 μs |   5.8720 μs |  0.3219 μs | 32.627 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 18.768 μs | 239.2514 μs | 13.1142 μs | 11.368 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.278 μs |   0.8360 μs |  0.0458 μs |  8.288 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.061 μs |   3.3945 μs |  0.1861 μs |  8.148 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.772 μs |   3.2915 μs |  0.1804 μs | 11.722 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 19.127 μs |  49.3200 μs |  2.7034 μs | 18.533 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.165 μs |   9.5986 μs |  0.5261 μs | 12.018 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.862 μs |   8.5211 μs |  0.4671 μs | 32.949 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 14.094 μs |  59.2378 μs |  3.2470 μs | 15.964 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.209 μs |   5.3501 μs |  0.2933 μs | 20.108 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 29.129 μs | 204.1965 μs | 11.1927 μs | 23.334 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.916 μs |  20.2088 μs |  1.1077 μs | 21.482 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.837 μs |   8.8184 μs |  0.4834 μs |  4.687 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 18.024 μs | 228.0662 μs | 12.5011 μs | 10.927 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,866.25 ns | 183.574 ns | 10.062 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.82 ns |   0.837 ns |  0.046 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.79 ns |   0.125 ns |  0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.58 ns |   1.178 ns |  0.065 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     27.77 ns |   1.713 ns |  0.094 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.97 ns |   0.354 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    107.47 ns |  17.201 ns |  0.943 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.15 ns |   1.114 ns |  0.061 ns |  0.74 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.143 μs |   7.838 μs | 0.4296 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 502.756 μs |  71.244 μs | 3.9051 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.833 μs |  31.819 μs | 1.7441 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 224.369 μs | 171.232 μs | 9.3858 μs |      80 B |


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