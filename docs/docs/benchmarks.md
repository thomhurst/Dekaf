---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-10 20:58 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean       | Error        | StdDev      | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-----------:|-------------:|------------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,069.8 μs** |  **2,280.55 μs** |   **125.00 μs** |  **1.00** |    **0.03** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,581.5 μs |  4,943.72 μs |   270.98 μs |  0.26 |    0.04 |       - |   34.74 KB |        0.33 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,504.0 μs** |  **1,319.60 μs** |    **72.33 μs** |  **1.00** |    **0.01** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,454.9 μs |  1,273.09 μs |    69.78 μs |  0.33 |    0.01 |       - |  341.18 KB |        0.32 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,494.6 μs** |    **742.81 μs** |    **40.72 μs** |  **1.00** |    **0.01** |       **-** |  **194.03 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,450.7 μs |  3,959.00 μs |   217.01 μs |  0.22 |    0.03 |       - |   38.29 KB |        0.20 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **8,342.6 μs** |  **1,952.30 μs** |   **107.01 μs** |  **1.00** |    **0.02** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 6,435.2 μs |  6,181.64 μs |   338.84 μs |  0.77 |    0.04 |       - |  367.88 KB |        0.19 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **129.9 μs** |     **34.60 μs** |     **1.90 μs** |  **1.00** |    **0.02** |  **0.2441** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |   101.7 μs |    588.84 μs |    32.28 μs |  0.78 |    0.22 |       - |    4.89 KB |        0.15 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,326.3 μs** |    **303.76 μs** |    **16.65 μs** |  **1.00** |    **0.02** |  **3.9063** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   739.3 μs |  2,128.29 μs |   116.66 μs |  0.56 |    0.08 |       - |   92.67 KB |        0.28 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **679.7 μs** |    **634.51 μs** |    **34.78 μs** |  **1.00** |    **0.06** |  **1.4648** |  **121.79 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   215.3 μs |    599.92 μs |    32.88 μs |  0.32 |    0.04 |       - |   88.51 KB |        0.73 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,961.3 μs** | **35,822.54 μs** | **1,963.55 μs** |  **1.04** |    **0.30** | **13.6719** | **1219.25 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 2,449.9 μs |  9,935.65 μs |   544.61 μs |  0.32 |    0.09 |       - |  897.42 KB |        0.74 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,364.5 μs** |    **284.61 μs** |    **15.60 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,271.5 μs |  1,106.48 μs |    60.65 μs |  0.24 |    0.01 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,386.6 μs** |    **245.90 μs** |    **13.48 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,318.8 μs |    454.65 μs |    24.92 μs |  0.24 |    0.00 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,370.7 μs** |     **93.54 μs** |     **5.13 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,372.2 μs |  2,465.94 μs |   135.17 μs |  0.26 |    0.02 |       - |     1.1 KB |        0.54 |
|                         |               |             |           |            |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,350.9 μs** |     **66.79 μs** |     **3.66 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,101.6 μs |     37.53 μs |     2.06 μs |  0.21 |    0.00 |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev        | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|--------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,180,299.23 μs** | **365,917.34 μs** | **20,057.164 μs** | **1.000** |    **0.01** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    17,626.97 μs |  45,567.39 μs |  2,497.702 μs | 0.006 |    0.00 |  627792 B |        8.22 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,065.37 μs** |   **8,671.79 μs** |    **475.330 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,662.10 μs |  24,220.13 μs |  1,327.587 μs | 0.005 |    0.00 |  814016 B |        3.17 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,901.51 μs** |  **25,631.21 μs** |  **1,404.933 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20,648.07 μs | 102,083.40 μs |  5,595.536 μs | 0.007 |    0.00 | 1033824 B |        1.68 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166,353.81 μs** |  **18,723.34 μs** |  **1,026.290 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16,043.30 μs |  16,790.97 μs |    920.370 μs | 0.005 |    0.00 | 2847168 B |        1.17 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **40.95 μs** |      **63.74 μs** |      **3.494 μs** |  **1.00** |    **0.10** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        39.03 μs |      98.58 μs |      5.404 μs |  0.96 |    0.13 |     512 B |        0.59 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **44.86 μs** |     **119.95 μs** |      **6.575 μs** |  **1.01** |    **0.18** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        44.77 μs |      85.60 μs |      4.692 μs |  1.01 |    0.15 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **43.56 μs** |      **38.96 μs** |      **2.136 μs** |  **1.00** |    **0.06** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        40.62 μs |     101.45 μs |      5.561 μs |  0.93 |    0.12 |     512 B |        0.59 |
|                      |            |              |             |                 |               |               |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **44.02 μs** |      **45.44 μs** |      **2.490 μs** |  **1.00** |    **0.07** |    **3360 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        43.61 μs |      75.53 μs |      4.140 μs |  0.99 |    0.10 |    2408 B |        0.72 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 21.521 μs |   7.679 μs | 0.4209 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 |  8.890 μs |  13.166 μs | 0.7217 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  6.624 μs |  16.929 μs | 0.9279 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 10.308 μs |  19.781 μs | 1.0842 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 12.103 μs |  14.140 μs | 0.7750 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 10.309 μs |  16.407 μs | 0.8993 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 10.670 μs |  19.438 μs | 1.0654 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.316 μs | 108.832 μs | 5.9655 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.589 μs |   8.763 μs | 0.4803 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 13.624 μs |   8.527 μs | 0.4674 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 37.389 μs |  73.576 μs | 4.0330 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 39.056 μs |  65.005 μs | 3.5631 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  6.048 μs |  54.093 μs | 2.9650 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       |  8.583 μs |  36.796 μs | 2.0169 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-----------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |    999.2 ns |  10,418.1 ns |   571.0 ns |    762.5 ns |  0.28 |    0.15 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,290.0 ns |   6,741.2 ns |   369.5 ns |  1,287.0 ns |  0.36 |    0.12 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,829.0 ns |  11,126.3 ns |   609.9 ns |  2,030.0 ns |  0.51 |    0.19 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  1,891.7 ns |  14,992.0 ns |   821.8 ns |  1,465.0 ns |  0.52 |    0.24 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |  1,721.5 ns |  22,298.5 ns | 1,222.3 ns |  1,980.5 ns |  0.48 |    0.32 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 34,418.3 ns | 179,583.8 ns | 9,843.6 ns | 30,107.0 ns |  9.52 |    3.30 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,842.5 ns |  22,692.5 ns | 1,243.9 ns |  3,177.5 ns |  1.06 |    0.40 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,547.3 ns |  23,203.6 ns | 1,271.9 ns |  3,059.0 ns |  0.98 |    0.39 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.33 μs |  45.286 μs |  2.482 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 394.65 μs | 431.590 μs | 23.657 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  12.23 μs |   9.268 μs |  0.508 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 184.26 μs | 164.395 μs |  9.011 μs |      80 B |


---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to baseline (Confluent.Kafka)
  - `< 1.0` = Dekaf is faster
  - `> 1.0` = Confluent is faster
  - `1.0` = Same performance
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*