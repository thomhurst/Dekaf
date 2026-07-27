---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-27 13:55 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **5,775.06 μs** |   **116.921 μs** |    **69.578 μs** |  **1.00** |    **0.02** |       **-** |  **105170 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,476.37 μs |   113.893 μs |    59.568 μs |  0.43 |    0.01 |       - |    5587 B |        0.05 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,261.10 μs** |   **665.569 μs** |   **396.069 μs** |  **1.00** |    **0.07** |       **-** | **1048756 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,639.11 μs |   135.510 μs |    89.632 μs |  0.50 |    0.03 |       - |   50972 B |        0.05 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,068.91 μs** |    **27.039 μs** |    **14.142 μs** |  **1.00** |    **0.00** |       **-** |  **194770 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,673.62 μs |   171.637 μs |   102.138 μs |  0.44 |    0.02 |       - |    6268 B |        0.03 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      |  **7,522.02 μs** |   **232.260 μs** |   **121.476 μs** |  **1.00** |    **0.02** | **15.6250** | **1944375 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,778.68 μs | 4,360.526 μs | 2,884.218 μs |  1.70 |    0.37 |       - |  350180 B |        0.18 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **105.41 μs** |     **9.273 μs** |     **6.133 μs** |  **1.00** |    **0.08** |  **0.2441** |   **30400 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     99.76 μs |    18.464 μs |    12.213 μs |  0.95 |    0.12 |       - |     159 B |       0.005 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |    **961.06 μs** |   **101.623 μs** |    **67.217 μs** |  **1.00** |    **0.10** |  **1.9531** |  **304000 B** |       **1.000** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,001.22 μs |    76.362 μs |    39.939 μs |  1.05 |    0.09 |       - |    2103 B |       0.007 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **735.38 μs** |   **175.160 μs** |   **115.857 μs** |  **1.02** |    **0.22** |  **1.2207** |  **120695 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    756.11 μs |    40.466 μs |    21.164 μs |  1.05 |    0.16 |       - |    1979 B |        0.02 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **6,207.23 μs** |   **612.184 μs** |   **364.301 μs** |  **1.00** |    **0.08** | **13.6719** | **1208231 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,254.91 μs |   462.069 μs |   305.630 μs |  1.17 |    0.08 |       - |   17993 B |        0.01 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,378.15 μs** |   **306.083 μs** |   **182.145 μs** |  **1.00** |    **0.04** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,343.90 μs |     8.819 μs |     5.248 μs |  0.44 |    0.01 |       - |     648 B |        0.54 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,469.07 μs** |   **659.109 μs** |   **392.225 μs** |  **1.00** |    **0.09** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,382.96 μs |    15.633 μs |    10.340 μs |  0.44 |    0.03 |       - |     648 B |        0.54 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,323.45 μs** |   **204.449 μs** |   **121.664 μs** |  **1.00** |    **0.03** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,405.70 μs |    20.414 μs |    13.502 μs |  0.45 |    0.01 |       - |     648 B |        0.31 |
|                         |               |             |           |              |              |              |       |         |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,279.71 μs** |    **10.804 μs** |     **7.146 μs** |  **1.00** |    **0.00** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,391.25 μs |    41.361 μs |    24.613 μs |  0.45 |    0.00 |       - |     648 B |        0.31 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|----------:|----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **121.7 μs** |  **27.80 μs** |  **14.54 μs** |  **1.01** |    **0.16** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   128.0 μs |  29.75 μs |  15.56 μs |  1.06 |    0.17 |   40.18 KB |        0.62 |
|                      |              |             |            |           |           |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **154.1 μs** |  **52.59 μs** |  **27.51 μs** |  **1.03** |    **0.24** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   159.4 μs |  38.66 μs |  20.22 μs |  1.06 |    0.22 |  215.96 KB |        0.90 |
|                      |              |             |            |           |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **860.3 μs** | **275.93 μs** | **122.52 μs** |  **1.02** |    **0.19** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,034.2 μs | 414.65 μs | 184.11 μs |  1.22 |    0.26 |  476.85 KB |        0.74 |
|                      |              |             |            |           |           |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,302.7 μs** | **595.33 μs** | **311.37 μs** |  **1.05** |    **0.33** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,661.6 μs | 937.29 μs | 416.16 μs |  1.34 |    0.42 | 2234.66 KB |        0.93 |


| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **757.7 ns** |  **43.54 ns** |  **22.77 ns** |  **1.00** |    **0.04** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,493.8 ns | 187.28 ns | 111.45 ns |  1.97 |    0.15 |     452 B |        0.70 |
|                      |             |            |           |           |       |         |           |             |
| **Confluent_PollSingle** | **1000**        | **1,289.9 ns** |  **93.51 ns** |  **61.85 ns** |  **1.00** |    **0.06** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,585.4 ns | 147.73 ns |  97.72 ns |  2.01 |    0.12 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 577.09 ns | 35.820 ns | 5.543 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  28.42 ns |  0.183 ns | 0.028 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.79 ns |  0.379 ns | 0.059 ns |      - |         - |
| WriteListConfigResourcesV1 |  21.13 ns |  0.118 ns | 0.031 ns |      - |         - |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.985 μs** | **0.0535 μs** | **0.0139 μs** |         **-** |
| **WriteRequest** | **1**       | **2.002 μs** | **0.0017 μs** | **0.0004 μs** |         **-** |


| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.461 μs** | **0.0094 μs** | **0.0025 μs** |         **-** |
| **WriteRequest** | **9**       | **2.444 μs** | **0.0093 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **10**      | **2.455 μs** | **0.0095 μs** | **0.0025 μs** |         **-** |
| **WriteRequest** | **11**      | **2.457 μs** | **0.0106 μs** | **0.0027 μs** |         **-** |


| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **108.38 ns** | **0.314 ns** | **0.082 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  96.34 ns | 1.829 ns | 0.283 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **98.30 ns** | **0.838 ns** | **0.218 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  90.99 ns | 0.844 ns | 0.131 ns |         - |


| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,745.8 ns | 7.10 ns | 4.22 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,244.8 ns | 4.46 ns | 2.65 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,407.5 ns | 2.19 ns | 1.15 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,395.2 ns | 6.69 ns | 3.98 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,008.4 ns | 3.12 ns | 1.86 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,976.2 ns | 4.91 ns | 3.25 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,961.5 ns | 7.71 ns | 4.59 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,895.5 ns | 7.43 ns | 4.42 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,193.6 ns | 1.56 ns | 1.03 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,040.8 ns | 1.72 ns | 0.90 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   707.1 ns | 1.33 ns | 0.79 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   778.4 ns | 1.31 ns | 0.69 ns | 0.0019 |      40 B |
| &#39;Read RecordBatch (10 records)&#39;                 |   162.5 ns | 0.13 ns | 0.08 ns |      - |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,187.4 ns | 1.50 ns | 0.99 ns |      - |         - |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error       | StdDev     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|------------:|-----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 9,553.271 ns | 158.2805 ns | 94.1902 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |            |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |    10.626 ns |   0.1101 ns |  0.0655 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |    14.041 ns |   0.2225 ns |  0.1472 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |    25.062 ns |   0.6700 ns |  0.4431 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |    30.777 ns |   0.7426 ns |  0.3884 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     7.291 ns |   0.1064 ns |  0.0704 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |             |            |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    93.500 ns |   1.2520 ns |  0.7450 ns |  1.00 |    0.01 | 0.0106 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |    50.454 ns |   0.6915 ns |  0.4574 ns |  0.54 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean         | Error       | StdDev      | Gen0   | Allocated |
|------------------------ |-------------:|------------:|------------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     295.7 ns |     0.48 ns |     0.25 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,544.0 ns |    87.91 ns |    58.15 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.1 ns |     0.77 ns |     0.46 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 124,190.6 ns | 1,909.70 ns | 1,263.15 ns |      - |      80 B |


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