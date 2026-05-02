---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-05-02 07:22 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,083.51 μs** |   **101.813 μs** |    **67.343 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **106.55 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,133.29 μs |    15.563 μs |     9.261 μs |  0.19 |    0.00 |        - |       - |   32.04 KB |        0.30 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,370.73 μs** |    **82.606 μs** |    **54.639 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1062.82 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,339.47 μs |    87.523 μs |    57.891 μs |  0.32 |    0.01 |  15.6250 |       - |  309.55 KB |        0.29 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,407.29 μs** |   **109.609 μs** |    **72.500 μs** |  **1.00** |    **0.02** |   **7.8125** |       **-** |  **194.05 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,262.31 μs |    51.711 μs |    30.772 μs |  0.20 |    0.01 |        - |       - |   34.41 KB |        0.18 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **45,112.85 μs** | **6,694.846 μs** | **4,428.226 μs** |  **1.01** |    **0.14** | **109.3750** | **31.2500** | **1937.83 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,346.43 μs |   269.386 μs |   160.308 μs |  0.14 |    0.01 |  15.6250 |       - |  344.21 KB |        0.18 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **147.45 μs** |     **4.402 μs** |     **2.912 μs** |  **1.00** |    **0.03** |   **2.4414** |       **-** |   **42.07 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     58.44 μs |     5.769 μs |     3.816 μs |  0.40 |    0.03 |   0.4883 |  0.2441 |   15.08 KB |        0.36 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,475.41 μs** |    **49.939 μs** |    **33.031 μs** |  **1.00** |    **0.03** |  **23.4375** |       **-** |   **420.1 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    723.26 μs |    61.572 μs |    40.726 μs |  0.49 |    0.03 |   3.9063 |       - |  168.26 KB |        0.40 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    296.23 μs |    52.049 μs |    30.974 μs |     ? |       ? |   6.8359 |  5.8594 |  198.17 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |           **NA** |           **NA** |           **NA** |     **?** |       **?** |       **NA** |      **NA** |         **NA** |           **?** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,848.90 μs |   410.665 μs |   214.786 μs |     ? |       ? |   7.8125 |       - |  206.52 KB |           ? |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,437.52 μs** |    **10.095 μs** |     **6.677 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,313.39 μs |    28.702 μs |    15.012 μs |  0.24 |    0.00 |        - |       - |    1.29 KB |        1.09 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,453.86 μs** |    **19.821 μs** |    **13.111 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.18 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,118.94 μs |     4.814 μs |     3.184 μs |  0.21 |    0.00 |        - |       - |    1.29 KB |        1.09 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,491.68 μs** |    **51.105 μs** |    **33.802 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,116.30 μs |     3.185 μs |     1.896 μs |  0.20 |    0.00 |        - |       - |    1.29 KB |        0.63 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,491.24 μs** |    **32.041 μs** |    **21.193 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2.06 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,404.01 μs |    16.410 μs |     9.765 μs |  0.26 |    0.00 |        - |       - |    1.32 KB |        0.64 |

Benchmarks with issues:
  ProducerBenchmarks.Confluent_FireAndForget: Job-OHFVZH(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.Confluent_FireAndForget: Job-OHFVZH(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Ratio | Gen0      | Gen1      | Gen2      | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|------:|----------:|----------:|----------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.243 ms** | **16.4940 ms** | **2.5525 ms** | **1.000** |         **-** |         **-** |         **-** |    **76880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.743 ms |  9.7918 ms | 2.5429 ms | 0.005 |         - |         - |         - |  9007184 B |      117.16 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.191 ms** |  **3.6586 ms** | **0.9501 ms** | **1.000** |         **-** |         **-** |         **-** |   **256592 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    16.575 ms | 12.3221 ms | 3.2000 ms | 0.005 | 1000.0000 | 1000.0000 | 1000.0000 |  9439736 B |       36.79 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166.161 ms** |  **2.5864 ms** | **0.6717 ms** | **1.000** |         **-** |         **-** |         **-** |   **616880 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    14.873 ms | 10.6238 ms | 1.6440 ms | 0.005 | 1000.0000 | 1000.0000 | 1000.0000 |  9675520 B |       15.68 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.321 ms** |  **1.8068 ms** | **0.2796 ms** | **1.000** |         **-** |         **-** |         **-** |  **2424608 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    16.965 ms |  4.0976 ms | 1.0641 ms | 0.005 | 1000.0000 | 1000.0000 | 1000.0000 | 14430216 B |        5.95 |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,155.981 ms** |  **5.2535 ms** | **1.3643 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.254 ms |  1.2725 ms | 0.3305 ms | 0.002 |         - |         - |         - |   368528 B |          NA |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,154.621 ms** |  **5.2725 ms** | **1.3692 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.659 ms |  5.2869 ms | 1.3730 ms | 0.002 |         - |         - |         - |   827800 B |          NA |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.152 ms** |  **3.0433 ms** | **0.7903 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.362 ms |  0.9960 ms | 0.1541 ms | 0.002 |         - |         - |         - |   825816 B |          NA |
|                      |            |              |             |              |            |           |       |           |           |           |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,154.653 ms** |  **8.0430 ms** | **2.0887 ms** | **1.000** |         **-** |         **-** |         **-** |          **-** |          **NA** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.915 ms |  0.9699 ms | 0.1501 ms | 0.002 |         - |         - |         - |  4498472 B |          NA |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                    | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                       | 28.777 μs | 12.4808 μs | 7.4271 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;           | 10.353 μs |  0.1261 μs | 0.0750 μs |         - |
| &#39;Write 100 CompactStrings&#39;                | 13.732 μs |  0.2096 μs | 0.1247 μs |         - |
| &#39;Write 1000 VarInts&#39;                      | 36.201 μs | 16.4930 μs | 9.8147 μs |         - |
| &#39;Read 1000 Int32s&#39;                        | 16.967 μs |  6.1468 μs | 3.6578 μs |         - |
| &#39;Read 1000 VarInts&#39;                       | 27.436 μs | 12.3732 μs | 7.3631 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;          | 17.307 μs |  1.1439 μs | 0.6807 μs |         - |
| &#39;Read RecordBatch (10 records)&#39;           |  4.654 μs |  0.5999 μs | 0.3968 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39; | 10.043 μs |  1.2036 μs | 0.7961 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,426.8 ns |   197.89 ns |   103.50 ns |  0.36 |    0.03 |         - |          NA |
| &#39;Serialize String (100 chars)&#39;       |  1,585.3 ns |    57.97 ns |    34.50 ns |  0.40 |    0.02 |         - |          NA |
| &#39;Serialize String (1000 chars)&#39;      |  2,155.6 ns |   224.50 ns |   148.49 ns |  0.54 |    0.04 |         - |          NA |
| &#39;Deserialize String&#39;                 |  2,794.8 ns |   480.07 ns |   285.68 ns |  0.71 |    0.07 |         - |          NA |
| &#39;Serialize Int32&#39;                    |    711.0 ns |    27.87 ns |    16.58 ns |  0.18 |    0.01 |         - |          NA |
| &#39;Serialize 100 Messages (key+value)&#39; | 48,456.1 ns | 6,870.52 ns | 4,544.42 ns | 12.23 |    1.17 |         - |          NA |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,966.8 ns |   231.85 ns |   137.97 ns |  1.00 |    0.05 |         - |          NA |
| &#39;PooledBufferWriter Direct&#39;          |  3,264.6 ns |    95.76 ns |    63.34 ns |  0.82 |    0.03 |         - |          NA |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    13.833 μs |  2.1374 μs |  1.4137 μs |         - |
| &#39;Snappy Compress 1MB&#39;   |   533.200 μs | 30.8578 μs | 20.4105 μs |         - |
| &#39;Snappy Decompress 1KB&#39; |     7.683 μs |  0.1964 μs |  0.1169 μs |         - |
| &#39;Snappy Decompress 1MB&#39; | 1,627.887 μs | 27.1222 μs | 17.9396 μs |         - |


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