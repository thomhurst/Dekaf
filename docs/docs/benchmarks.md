---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-11 07:10 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev       | Median      | Ratio | RatioSD | Gen0    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-------------:|------------:|------:|--------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,031.02 μs** |  **1,943.12 μs** |   **106.509 μs** | **6,025.53 μs** |  **1.00** |    **0.02** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 1,577.70 μs |  1,316.66 μs |    72.171 μs | 1,613.95 μs |  0.26 |    0.01 |       - |   34.75 KB |        0.33 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **7,338.79 μs** |    **897.10 μs** |    **49.173 μs** | **7,365.44 μs** |  **1.00** |    **0.01** |       **-** | **1062.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 2,367.27 μs |    798.95 μs |    43.793 μs | 2,355.73 μs |  0.32 |    0.01 |       - |  341.22 KB |        0.32 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,378.17 μs** |    **294.80 μs** |    **16.159 μs** | **6,374.80 μs** |  **1.00** |    **0.00** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 1,204.32 μs |    346.04 μs |    18.968 μs | 1,210.16 μs |  0.19 |    0.00 |       - |   38.31 KB |        0.20 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **7,958.81 μs** |    **421.61 μs** |    **23.110 μs** | **7,949.11 μs** |  **1.00** |    **0.00** | **15.6250** | **1937.79 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 7,405.80 μs | 26,443.25 μs | 1,449.444 μs | 7,116.63 μs |  0.93 |    0.16 |       - |  366.61 KB |        0.19 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |   **124.59 μs** |     **29.09 μs** |     **1.595 μs** |   **124.07 μs** |  **1.00** |    **0.02** |  **0.2441** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    65.38 μs |    103.49 μs |     5.672 μs |    66.12 μs |  0.52 |    0.04 |       - |    13.6 KB |        0.41 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      | **1,222.58 μs** |  **1,036.88 μs** |    **56.835 μs** | **1,236.39 μs** |  **1.00** |    **0.06** |  **3.9063** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   753.41 μs |  1,754.26 μs |    96.157 μs |   730.66 μs |  0.62 |    0.07 |       - |   70.86 KB |        0.21 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **777.90 μs** |  **1,999.21 μs** |   **109.583 μs** |   **720.22 μs** |  **1.01** |    **0.17** |  **1.4648** |  **122.05 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   260.47 μs |    586.49 μs |    32.148 μs |   244.26 μs |  0.34 |    0.05 |       - |   87.73 KB |        0.72 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,103.90 μs** | **35,448.42 μs** | **1,943.047 μs** | **6,069.35 μs** |  **1.05** |    **0.33** | **13.6719** | **1219.84 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 3,999.32 μs | 35,329.45 μs | 1,936.526 μs | 5,071.96 μs |  0.59 |    0.28 |       - |    92.4 KB |        0.08 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,328.23 μs** |    **267.28 μs** |    **14.651 μs** | **5,320.06 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 1,175.80 μs |  2,157.18 μs |   118.242 μs | 1,119.33 μs |  0.22 |    0.02 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,310.90 μs** |    **136.55 μs** |     **7.485 μs** | **5,314.00 μs** |  **1.00** |    **0.00** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 1,286.03 μs |    169.14 μs |     9.271 μs | 1,285.97 μs |  0.24 |    0.00 |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,307.52 μs** |     **37.28 μs** |     **2.043 μs** | **5,307.19 μs** |  **1.00** |    **0.00** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 1,141.64 μs |    602.44 μs |    33.022 μs | 1,160.58 μs |  0.22 |    0.01 |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |              |             |       |         |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,379.63 μs** |  **2,157.83 μs** |   **118.278 μs** | **5,314.67 μs** |  **1.00** |    **0.03** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 1,308.61 μs |    420.49 μs |    23.048 μs | 1,313.80 μs |  0.24 |    0.01 |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167,069.73 μs** |  **22,389.51 μs** | **1,227.244 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,783.20 μs |  61,338.74 μs | 3,362.183 μs | 0.005 |    0.00 |  627792 B |        8.22 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,163,599.62 μs** |  **26,228.01 μs** | **1,437.645 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13,803.91 μs |   9,056.05 μs |   496.392 μs | 0.004 |    0.00 |  829432 B |        3.23 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,164,473.08 μs** |  **22,124.38 μs** | **1,212.712 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17,287.92 μs | 128,058.68 μs | 7,019.328 μs | 0.005 |    0.00 | 1084216 B |        1.76 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,308.60 μs** |   **9,930.06 μs** |   **544.300 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15,864.01 μs |  57,911.52 μs | 3,174.326 μs | 0.005 |    0.00 | 2850072 B |        1.18 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **36.76 μs** |      **46.02 μs** |     **2.523 μs** |  **1.00** |    **0.08** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        43.72 μs |     116.08 μs |     6.363 μs |  1.19 |    0.17 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **36.84 μs** |     **113.37 μs** |     **6.214 μs** |  **1.02** |    **0.20** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        41.74 μs |     100.16 μs |     5.490 μs |  1.15 |    0.20 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **35.84 μs** |      **36.02 μs** |     **1.974 μs** |  **1.00** |    **0.07** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        40.39 μs |     122.62 μs |     6.721 μs |  1.13 |    0.17 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **37.16 μs** |      **49.15 μs** |     **2.694 μs** |  **1.00** |    **0.09** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        50.19 μs |     180.61 μs |     9.900 μs |  1.36 |    0.25 |    2408 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.976 μs |  2.0013 μs | 0.1097 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.227 μs |  1.6460 μs | 0.0902 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.600 μs |  1.9336 μs | 0.1060 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.483 μs |  1.9782 μs | 0.1084 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 15.129 μs |  1.1963 μs | 0.0656 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.844 μs |  3.2965 μs | 0.1807 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.202 μs |  3.2217 μs | 0.1766 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.907 μs |  3.6059 μs | 0.1977 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.984 μs |  1.8377 μs | 0.1007 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.518 μs |  0.6578 μs | 0.0361 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 31.693 μs | 24.5190 μs | 1.3440 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 34.462 μs | 28.8145 μs | 1.5794 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.978 μs |  2.0332 μs | 0.1114 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.085 μs |  4.7630 μs | 0.2611 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,424.2 ns |   379.8 ns |  20.82 ns |  0.34 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,184.7 ns | 1,062.3 ns |  58.23 ns |  0.29 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,356.3 ns |   105.3 ns |   5.77 ns |  0.33 |    0.00 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,542.0 ns | 5,825.9 ns | 319.34 ns |  0.61 |    0.07 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    786.2 ns |   489.6 ns |  26.84 ns |  0.19 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,777.0 ns | 7,288.5 ns | 399.51 ns |  9.60 |    0.15 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,144.0 ns | 1,098.7 ns |  60.22 ns |  1.00 |    0.02 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,890.3 ns | 2,098.7 ns | 115.04 ns |  0.94 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean      | Error      | StdDev    | Allocated |
|------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.31 μs |   3.856 μs |  0.211 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 511.60 μs | 434.732 μs | 23.829 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |  10.05 μs |   5.121 μs |  0.281 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 228.31 μs |  44.052 μs |  2.415 μs |      80 B |


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