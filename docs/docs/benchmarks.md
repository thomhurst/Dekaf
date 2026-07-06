---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 03:37 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Median       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,172.29 μs** |    **175.52 μs** |     **9.621 μs** |  **6,174.97 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,359.65 μs |  2,999.87 μs |   164.433 μs |  1,351.89 μs |  0.22 |    0.02 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,424.05 μs** |    **514.19 μs** |    **28.185 μs** |  **7,412.60 μs** |  **1.00** |    **0.00** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,353.79 μs |  1,268.06 μs |    69.507 μs |  2,373.65 μs |  0.32 |    0.01 |  15.6250 |       - |  339.33 KB |        0.32 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,151.54 μs** |    **225.83 μs** |    **12.378 μs** |  **6,150.56 μs** |  **1.00** |    **0.00** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,411.15 μs |  2,461.18 μs |   134.905 μs |  1,386.59 μs |  0.23 |    0.02 |        - |       - |   36.27 KB |        0.19 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,771.68 μs** |  **1,753.98 μs** |    **96.141 μs** | **12,752.57 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,529.57 μs |  3,121.01 μs |   171.073 μs |  6,563.42 μs |  0.51 |    0.01 |  15.6250 |       - |  361.41 KB |        0.19 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **135.73 μs** |     **66.58 μs** |     **3.650 μs** |    **136.02 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     68.30 μs |    236.46 μs |    12.961 μs |     70.87 μs |  0.50 |    0.08 |        - |       - |   11.41 KB |        0.34 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,329.82 μs** |  **1,100.24 μs** |    **60.308 μs** |  **1,354.78 μs** |  **1.00** |    **0.06** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    637.21 μs |    655.89 μs |    35.952 μs |    654.93 μs |  0.48 |    0.03 |        - |       - |   92.82 KB |        0.28 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,120.17 μs** |    **252.49 μs** |    **13.840 μs** |  **1,116.00 μs** |  **1.00** |    **0.02** |   **7.3242** |       **-** |  **122.62 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    204.85 μs |    126.33 μs |     6.924 μs |    203.00 μs |  0.18 |    0.01 |   0.4883 |       - |   29.78 KB |        0.24 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,607.72 μs** | **30,080.02 μs** | **1,648.787 μs** | **11,499.71 μs** |  **1.02** |    **0.20** |  **74.2188** |       **-** | **1227.02 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,164.85 μs |  2,154.01 μs |   118.069 μs |  2,100.50 μs |  0.21 |    0.03 |   7.8125 |       - | 1002.43 KB |        0.82 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,465.39 μs** |    **934.82 μs** |    **51.241 μs** |  **5,440.47 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,367.22 μs |    944.25 μs |    51.758 μs |  1,379.05 μs |  0.25 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,432.72 μs** |    **137.69 μs** |     **7.547 μs** |  **5,429.98 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,394.30 μs |    590.21 μs |    32.352 μs |  1,401.98 μs |  0.26 |    0.01 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **6,829.86 μs** | **43,944.67 μs** | **2,408.756 μs** |  **5,445.25 μs** |  **1.07** |    **0.43** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,379.76 μs |    317.67 μs |    17.413 μs |  1,372.65 μs |  0.22 |    0.06 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,436.18 μs** |     **31.07 μs** |     **1.703 μs** |  **5,436.00 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,366.14 μs |    447.25 μs |    24.515 μs |  1,374.00 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error     | StdDev    | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|----------:|----------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.967 ms** |  **9.421 ms** | **0.5164 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16.051 ms | 17.143 ms | 0.9397 ms | 0.005 |  601.01 KB |        8.05 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.074 ms** | **13.989 ms** | **0.7668 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14.445 ms | 24.629 ms | 1.3500 ms | 0.005 |   784.4 KB |        3.13 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.791 ms** | **13.841 ms** | **0.7587 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    15.332 ms | 33.661 ms | 1.8451 ms | 0.005 | 1005.17 KB |        1.67 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.927 ms** | **18.483 ms** | **1.0131 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.510 ms | 12.886 ms | 0.7063 ms | 0.005 | 2769.07 KB |        1.17 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.840 ms** | **42.200 ms** | **2.3131 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     7.135 ms | 10.162 ms | 0.5570 ms | 0.002 |  184.54 KB |       76.69 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.153 ms** |  **7.326 ms** | **0.4016 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     6.073 ms | 13.999 ms | 0.7673 ms | 0.002 |   195.5 KB |       46.95 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.422 ms** | **45.581 ms** | **2.4985 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.823 ms |  8.098 ms | 0.4439 ms | 0.002 |  184.72 KB |       76.77 |
|                      |            |              |             |              |           |           |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,158.698 ms** | **58.965 ms** | **3.2321 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.108 ms | 13.164 ms | 0.7216 ms | 0.002 |  187.13 KB |       44.77 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.963 μs |   4.1442 μs |  0.2272 μs | 26.064 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.303 μs |   2.0201 μs |  0.1107 μs | 10.249 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.320 μs |   3.1223 μs |  0.1711 μs | 10.270 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.701 μs |   0.8381 μs |  0.0459 μs | 26.690 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  9.018 μs |   1.4504 μs |  0.0795 μs |  9.018 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.208 μs |   2.4134 μs |  0.1323 μs | 20.158 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.587 μs |   2.4903 μs |  0.1365 μs | 17.524 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 28.079 μs | 173.4398 μs |  9.5068 μs | 24.366 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.303 μs |  10.4465 μs |  0.5726 μs |  5.399 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 20.975 μs | 254.7841 μs | 13.9656 μs | 13.154 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error        | StdDev       | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-------------:|-------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,195.0 ns |     593.1 ns |     32.51 ns |  0.28 |    0.03 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,523.3 ns |   8,396.7 ns |    460.25 ns |  0.36 |    0.10 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,829.3 ns |   4,768.9 ns |    261.40 ns |  0.43 |    0.07 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,476.5 ns |     855.1 ns |     46.87 ns |  0.59 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    749.0 ns |   1,290.9 ns |     70.76 ns |  0.18 |    0.02 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 50,107.5 ns | 315,757.0 ns | 17,307.71 ns | 11.87 |    3.71 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,254.3 ns |   8,455.4 ns |    463.47 ns |  1.01 |    0.13 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,680.3 ns |   3,213.9 ns |    176.16 ns |  0.87 |    0.09 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean        | Error        | StdDev    | Allocated |
|------------------------ |------------:|-------------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.13 μs |     3.696 μs |  0.203 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   545.74 μs |   236.784 μs | 12.979 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |    10.00 μs |     2.700 μs |  0.148 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,774.88 μs | 1,101.994 μs | 60.404 μs |    1280 B |


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