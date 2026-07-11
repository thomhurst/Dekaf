---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 08:43 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,177.5 μs** |    **906.67 μs** |    **49.70 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.54 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,674.2 μs |  3,633.14 μs |   199.14 μs |  0.27 |    0.03 |        - |       - |   34.73 KB |        0.33 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,471.5 μs** |  **1,602.21 μs** |    **87.82 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,537.9 μs |  1,539.11 μs |    84.36 μs |  0.34 |    0.01 |  15.6250 |       - |  341.47 KB |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,130.6 μs** |    **793.66 μs** |    **43.50 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,860.0 μs |  1,646.81 μs |    90.27 μs |  0.30 |    0.01 |        - |       - |   38.39 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,964.2 μs** |  **2,570.37 μs** |   **140.89 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,633.1 μs |  7,556.64 μs |   414.20 μs |  0.59 |    0.03 |  15.6250 |       - |  391.71 KB |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.9 μs** |     **16.09 μs** |     **0.88 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    106.6 μs |    293.38 μs |    16.08 μs |  0.78 |    0.10 |        - |       - |    8.21 KB |        0.24 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,371.7 μs** |    **138.77 μs** |     **7.61 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,032.8 μs |  3,905.13 μs |   214.05 μs |  0.75 |    0.14 |        - |       - |   47.13 KB |        0.14 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,142.0 μs** |    **821.30 μs** |    **45.02 μs** |  **1.00** |    **0.05** |   **7.3242** |       **-** |   **122.6 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    300.8 μs |     43.01 μs |     2.36 μs |  0.26 |    0.01 |   0.9766 |       - |  109.31 KB |        0.89 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,173.2 μs** | **29,846.22 μs** | **1,635.97 μs** |  **1.02** |    **0.21** |  **74.2188** |       **-** | **1226.58 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,973.6 μs |  3,515.42 μs |   192.69 μs |  0.40 |    0.06 |        - |       - |  963.66 KB |        0.79 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,438.1 μs** |     **89.27 μs** |     **4.89 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,157.2 μs |     81.90 μs |     4.49 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,475.7 μs** |    **420.18 μs** |    **23.03 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,162.8 μs |     45.60 μs |     2.50 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,433.4 μs** |     **86.96 μs** |     **4.77 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,167.3 μs |    102.57 μs |     5.62 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,453.1 μs** |     **44.85 μs** |     **2.46 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,211.6 μs |    259.89 μs |    14.25 μs |  0.22 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167,852.31 μs** |  **15,477.87 μs** |   **848.394 μs** | **3,167,367.56 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16,086.16 μs |  28,853.91 μs | 1,581.580 μs |    16,261.37 μs | 0.005 |    0.00 |  635256 B |        8.31 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167,641.24 μs** |  **17,502.70 μs** |   **959.382 μs** | **3,167,955.62 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,564.97 μs |  37,680.44 μs | 2,065.392 μs |    14,814.46 μs | 0.005 |    0.00 |  822872 B |        3.21 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,939.23 μs** |  **21,256.69 μs** | **1,165.151 μs** | **3,166,343.10 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    22,277.15 μs | 103,918.73 μs | 5,696.136 μs |    21,996.14 μs | 0.007 |    0.00 | 1055896 B |        1.71 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165,627.01 μs** |  **27,411.37 μs** | **1,502.510 μs** | **3,166,352.75 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18,750.79 μs |  19,621.41 μs | 1,075.515 μs |    19,333.32 μs | 0.006 |    0.00 | 2934648 B |        1.21 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **41.09 μs** |      **75.73 μs** |     **4.151 μs** |        **39.27 μs** |  **1.01** |    **0.12** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        41.14 μs |     225.22 μs |    12.345 μs |        44.88 μs |  1.01 |    0.28 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **37.27 μs** |      **63.67 μs** |     **3.490 μs** |        **35.50 μs** |  **1.01** |    **0.11** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        36.40 μs |     188.04 μs |    10.307 μs |        31.54 μs |  0.98 |    0.25 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **32.85 μs** |      **16.97 μs** |     **0.930 μs** |        **33.14 μs** |  **1.00** |    **0.03** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        32.83 μs |     155.29 μs |     8.512 μs |        32.66 μs |  1.00 |    0.23 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **40.31 μs** |     **146.03 μs** |     **8.004 μs** |        **43.48 μs** |  **1.03** |    **0.27** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        46.47 μs |     452.96 μs |    24.828 μs |        35.00 μs |  1.19 |    0.60 |    2408 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.072 μs |  1.6352 μs | 0.0896 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.104 μs |  2.4499 μs | 0.1343 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 11.798 μs | 42.0556 μs | 2.3052 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.449 μs |  1.1083 μs | 0.0607 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 14.899 μs |  2.8808 μs | 0.1579 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 18.782 μs | 29.8699 μs | 1.6373 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.001 μs |  2.4169 μs | 0.1325 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 29.800 μs | 81.3246 μs | 4.4577 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.863 μs |  0.8227 μs | 0.0451 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.919 μs | 24.0574 μs | 1.3187 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.855 μs | 34.4323 μs | 1.8874 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.891 μs | 22.3178 μs | 1.2233 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.985 μs |  1.2047 μs | 0.0660 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.802 μs | 23.7271 μs | 1.3006 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,365.2 ns |  2,008.5 ns | 110.09 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,268.7 ns |  1,982.1 ns | 108.65 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,443.3 ns |  3,505.5 ns | 192.15 ns |  0.35 |    0.04 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,625.3 ns |  1,206.1 ns |  66.11 ns |  0.63 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    710.3 ns |    791.1 ns |  43.36 ns |  0.17 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 41,477.7 ns | 10,644.4 ns | 583.46 ns | 10.00 |    0.14 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,147.3 ns |    547.4 ns |  30.01 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,913.3 ns |  1,925.6 ns | 105.55 ns |  0.94 |    0.02 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.076 μs |  31.473 μs |  1.7251 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 503.140 μs |  96.134 μs |  5.2694 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.260 μs |   2.235 μs |  0.1225 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 247.860 μs | 189.739 μs | 10.4002 μs |      80 B |


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