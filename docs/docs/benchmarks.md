---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-12 15:27 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,168.3 μs** |    **601.81 μs** |    **32.99 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,623.9 μs |  1,677.14 μs |    91.93 μs |  0.26 |    0.01 |        - |       - |   34.71 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,376.1 μs** |  **1,413.81 μs** |    **77.50 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,459.2 μs |  1,453.71 μs |    79.68 μs |  0.33 |    0.01 |  15.6250 |       - |   341.4 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,233.9 μs** |    **740.12 μs** |    **40.57 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,882.1 μs |  1,326.92 μs |    72.73 μs |  0.30 |    0.01 |        - |       - |   38.14 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,456.2 μs** |  **5,569.34 μs** |   **305.27 μs** |  **1.00** |    **0.03** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,057.1 μs |  7,054.57 μs |   386.68 μs |  0.73 |    0.03 |  15.6250 |       - |  417.76 KB |        0.22 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.6 μs** |     **60.23 μs** |     **3.30 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    141.8 μs |    277.68 μs |    15.22 μs |  1.06 |    0.10 |        - |       - |    4.34 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,331.6 μs** |  **1,467.45 μs** |    **80.44 μs** |  **1.00** |    **0.07** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,539.6 μs |  5,978.96 μs |   327.73 μs |  1.16 |    0.22 |        - |       - |   43.35 KB |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **674.5 μs** |  **8,210.88 μs** |   **450.07 μs** |  **1.61** |    **1.71** |   **7.3242** |       **-** |  **122.56 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    776.7 μs |  1,411.69 μs |    77.38 μs |  1.86 |    1.46 |        - |       - |    8.81 KB |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,584.0 μs** | **39,232.26 μs** | **2,150.45 μs** |  **1.03** |    **0.27** |  **74.2188** |       **-** | **1226.29 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,642.0 μs |  3,407.11 μs |   186.75 μs |  0.74 |    0.15 |        - |       - |   96.58 KB |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,431.4 μs** |    **387.93 μs** |    **21.26 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,374.9 μs |     59.30 μs |     3.25 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,430.9 μs** |     **94.99 μs** |     **5.21 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,365.2 μs |    412.19 μs |    22.59 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.91 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,437.8 μs** |    **212.52 μs** |    **11.65 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,374.2 μs |    331.25 μs |    18.16 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.52 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,426.9 μs** |     **56.62 μs** |     **3.10 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,349.6 μs |    309.38 μs |    16.96 μs |  0.25 |    0.00 |        - |       - |    1.07 KB |        0.52 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **119.0 μs** |    **412.1 μs** |  **22.59 μs** |   **124.3 μs** |  **1.03** |    **0.25** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   164.1 μs |    439.7 μs |  24.10 μs |   158.8 μs |  1.42 |    0.31 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **151.0 μs** |    **412.0 μs** |  **22.58 μs** |   **140.9 μs** |  **1.01** |    **0.18** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   176.0 μs |    321.5 μs |  17.62 μs |   173.8 μs |  1.18 |    0.18 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **942.9 μs** |  **2,941.0 μs** | **161.21 μs** |   **856.5 μs** |  **1.02** |    **0.20** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,222.4 μs |  2,457.2 μs | 134.69 μs | 1,154.7 μs |  1.32 |    0.22 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,500.6 μs** | **13,322.1 μs** | **730.23 μs** | **1,087.5 μs** |  **1.14** |    **0.63** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,636.7 μs |  7,273.5 μs | 398.68 μs | 1,409.3 μs |  1.24 |    0.49 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **870.4 ns** |    **69.55 ns** |   **3.81 ns** |  **1.00** |    **0.01** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,075.4 ns | 2,633.40 ns | 144.35 ns |  2.38 |    0.14 |      - |     452 B |        0.70 |
|                      |             |            |             |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,477.9 ns** | **2,866.15 ns** | **157.10 ns** |  **1.01** |    **0.13** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,465.8 ns | 7,258.81 ns | 397.88 ns |  2.36 |    0.31 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 29.378 μs |  49.924 μs |  2.7365 μs | 30.908 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.990 μs |   3.047 μs |  0.1670 μs | 11.020 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.619 μs |   1.772 μs |  0.0971 μs |  8.596 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 18.641 μs | 250.552 μs | 13.7336 μs | 13.134 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.308 μs |   1.281 μs |  0.0702 μs | 11.301 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 19.048 μs |   3.230 μs |  0.1770 μs | 19.145 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.855 μs |   3.957 μs |  0.2169 μs | 12.915 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.690 μs | 116.917 μs |  6.4086 μs | 38.390 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.963 μs |   1.486 μs |  0.0814 μs |  8.926 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.294 μs |   1.780 μs |  0.0976 μs | 20.317 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.919 μs |  27.085 μs |  1.4846 μs | 31.448 μs |    2472 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.318 μs |  26.303 μs |  1.4418 μs | 33.984 μs |    2512 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.340 μs |   2.141 μs |  0.1173 μs |  5.370 μs |     184 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.094 μs |  18.362 μs |  1.0065 μs | 12.418 μs |     184 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,733.19 ns | 197.659 ns | 10.834 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     18.29 ns |   0.594 ns |  0.033 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.20 ns |   0.090 ns |  0.005 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.07 ns |   2.053 ns |  0.113 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     29.79 ns |   1.912 ns |  0.105 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.81 ns |   1.587 ns |  0.087 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    106.49 ns |   5.953 ns |  0.326 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     75.48 ns |   0.932 ns |  0.051 ns |  0.71 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.060 μs |   5.714 μs |  0.3132 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 513.585 μs | 422.350 μs | 23.1504 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.203 μs |   1.069 μs |  0.0586 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 244.401 μs | 242.396 μs | 13.2865 μs |      80 B |


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