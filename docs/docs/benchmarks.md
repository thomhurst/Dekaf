---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 13:20 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,045.60 μs** |    **481.61 μs** |    **26.398 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,580.48 μs |  3,030.86 μs |   166.131 μs |  0.26 |    0.02 |        - |       - |   34.76 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,215.25 μs** |  **1,071.97 μs** |    **58.758 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,352.00 μs |  1,008.71 μs |    55.291 μs |  0.33 |    0.01 |  15.6250 |       - |  339.51 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,615.95 μs** |  **1,084.97 μs** |    **59.471 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,521.70 μs |  1,161.82 μs |    63.683 μs |  0.23 |    0.01 |        - |       - |   36.56 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,587.88 μs** |  **2,301.97 μs** |   **126.179 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,330.77 μs |  3,354.61 μs |   183.877 μs |  0.55 |    0.01 |  15.6250 |       - |  359.82 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **116.25 μs** |     **50.20 μs** |     **2.751 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     79.96 μs |    293.57 μs |    16.091 μs |  0.69 |    0.12 |        - |       - |    6.62 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,116.08 μs** |  **3,969.79 μs** |   **217.597 μs** |  **1.03** |    **0.25** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    770.42 μs |  4,411.99 μs |   241.836 μs |  0.71 |    0.23 |        - |       - |   44.71 KB |        0.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **996.93 μs** |     **48.65 μs** |     **2.667 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **122.43 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    214.14 μs |     81.33 μs |     4.458 μs |  0.21 |    0.00 |        - |       - |    93.8 KB |        0.77 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,826.00 μs** | **32,243.85 μs** | **1,767.394 μs** |  **1.03** |    **0.27** |  **74.2188** |       **-** |  **1226.3 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,582.45 μs |  8,468.17 μs |   464.169 μs |  0.30 |    0.08 |   7.8125 |       - |  968.91 KB |        0.79 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,416.12 μs** |    **150.16 μs** |     **8.231 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,103.26 μs |     22.74 μs |     1.246 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,423.66 μs** |    **450.64 μs** |    **24.701 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,106.58 μs |     78.99 μs |     4.330 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.96 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,414.19 μs** |     **55.61 μs** |     **3.048 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,105.74 μs |     45.63 μs |     2.501 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.55 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,410.26 μs** |    **147.62 μs** |     **8.091 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,105.51 μs |     48.01 μs |     2.632 μs |  0.20 |    0.00 |        - |       - |    1.13 KB |        0.55 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170,511.16 μs** |  **20,741.92 μs** | **1,136.935 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16,647.49 μs |  41,117.78 μs | 2,253.804 μs | 0.005 |    0.00 |  620592 B |        8.12 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,170,757.31 μs** | **115,738.41 μs** | **6,344.013 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,539.08 μs |  11,968.90 μs |   656.056 μs | 0.005 |    0.00 |  806584 B |        3.15 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,167,566.44 μs** |  **14,709.89 μs** |   **806.299 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19,172.90 μs |  62,735.08 μs | 3,438.722 μs | 0.006 |    0.00 | 1032248 B |        1.67 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166,970.38 μs** |  **30,069.02 μs** | **1,648.185 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18,481.81 μs |  27,771.17 μs | 1,522.231 μs | 0.006 |    0.00 | 2859536 B |        1.18 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **38.24 μs** |     **141.53 μs** |     **7.758 μs** |  **1.03** |    **0.24** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        29.27 μs |     117.57 μs |     6.444 μs |  0.78 |    0.20 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **44.01 μs** |      **86.06 μs** |     **4.717 μs** |  **1.01** |    **0.13** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        30.60 μs |      63.12 μs |     3.460 μs |  0.70 |    0.09 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **45.23 μs** |      **35.82 μs** |     **1.963 μs** |  **1.00** |    **0.05** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        30.66 μs |     146.51 μs |     8.030 μs |  0.68 |    0.16 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **47.02 μs** |     **203.90 μs** |    **11.177 μs** |  **1.04** |    **0.31** |    **2736 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        28.90 μs |      53.25 μs |     2.919 μs |  0.64 |    0.15 |    2744 B |        1.00 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 28.111 μs |  0.5473 μs | 0.0300 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.769 μs |  3.4048 μs | 0.1866 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 11.317 μs | 10.2215 μs | 0.5603 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.712 μs |  3.7031 μs | 0.2030 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.976 μs |  7.8170 μs | 0.4285 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 13.704 μs | 48.7892 μs | 2.6743 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.123 μs |  6.2531 μs | 0.3428 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.755 μs | 16.7183 μs | 0.9164 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 12.088 μs | 61.2689 μs | 3.3584 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.421 μs |  1.0158 μs | 0.0557 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.848 μs | 77.3745 μs | 4.2412 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 32.180 μs | 50.0108 μs | 2.7413 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.975 μs |  3.5203 μs | 0.1930 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.883 μs | 12.3673 μs | 0.6779 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|---------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,275.7 ns |  2,208.9 ns | 121.1 ns |  0.31 |    0.04 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,348.0 ns |  3,659.5 ns | 200.6 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,370.2 ns |  3,591.8 ns | 196.9 ns |  0.33 |    0.05 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,516.3 ns |  2,281.1 ns | 125.0 ns |  0.61 |    0.06 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    663.7 ns |  4,259.1 ns | 233.5 ns |  0.16 |    0.05 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 35,179.3 ns | 11,049.5 ns | 605.7 ns |  8.53 |    0.69 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,149.2 ns |  7,066.7 ns | 387.4 ns |  1.01 |    0.11 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,769.0 ns |  9,320.0 ns | 510.9 ns |  0.91 |    0.13 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.971 μs |   4.970 μs |  0.2724 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 505.579 μs | 222.873 μs | 12.2164 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   7.916 μs |   9.187 μs |  0.5036 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 221.544 μs |  68.062 μs |  3.7307 μs |      80 B |


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