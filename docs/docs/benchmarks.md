---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-10 11:36 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Median      | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,100.5 μs** |  **1,270.16 μs** |    **69.62 μs** |  **6,104.8 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,559.1 μs |  1,907.31 μs |   104.55 μs |  1,583.7 μs |  0.26 |    0.02 |        - |       - |   34.73 KB |        0.33 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,250.4 μs** |  **1,431.58 μs** |    **78.47 μs** |  **7,238.5 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,405.0 μs |  1,154.31 μs |    63.27 μs |  2,428.4 μs |  0.33 |    0.01 |  15.6250 |       - |  341.53 KB |        0.32 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,609.4 μs** |    **842.60 μs** |    **46.19 μs** |  **6,619.7 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,572.3 μs |  2,564.27 μs |   140.56 μs |  1,577.0 μs |  0.24 |    0.02 |        - |       - |   38.32 KB |        0.20 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,561.3 μs** |    **274.29 μs** |    **15.03 μs** | **11,563.3 μs** |  **1.00** |    **0.00** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  7,732.7 μs | 23,563.77 μs | 1,291.61 μs |  7,350.9 μs |  0.67 |    0.10 |  15.6250 |       - |  389.93 KB |        0.20 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **114.2 μs** |     **57.00 μs** |     **3.12 μs** |    **115.9 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    104.7 μs |    118.88 μs |     6.52 μs |    101.6 μs |  0.92 |    0.05 |        - |       - |    4.27 KB |        0.13 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,166.7 μs** |    **355.27 μs** |    **19.47 μs** |  **1,162.1 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,016.2 μs |    949.46 μs |    52.04 μs |  1,012.4 μs |  0.87 |    0.04 |        - |       - |   82.82 KB |        0.25 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **535.9 μs** |  **7,477.14 μs** |   **409.85 μs** |    **373.6 μs** |  **1.43** |    **1.31** |   **7.3242** |       **-** |  **122.43 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    268.4 μs |    202.41 μs |    11.09 μs |    268.6 μs |  0.71 |    0.39 |   0.9766 |       - |   92.21 KB |        0.75 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,798.1 μs** | **39,465.17 μs** | **2,163.22 μs** |  **9,758.8 μs** |  **1.05** |    **0.35** |  **74.2188** |       **-** | **1224.85 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,426.7 μs |  9,425.78 μs |   516.66 μs |  3,217.1 μs |  0.41 |    0.11 |        - |       - |  1006.2 KB |        0.82 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,413.5 μs** |    **282.37 μs** |    **15.48 μs** |  **5,414.4 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,150.6 μs |    308.03 μs |    16.88 μs |  1,149.6 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,423.6 μs** |    **798.28 μs** |    **43.76 μs** |  **5,401.1 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,255.1 μs |    305.12 μs |    16.72 μs |  1,251.9 μs |  0.23 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,408.2 μs** |    **137.62 μs** |     **7.54 μs** |  **5,410.7 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,275.5 μs |    581.97 μs |    31.90 μs |  1,263.2 μs |  0.24 |    0.01 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |             |              |             |             |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,451.7 μs** |  **1,462.92 μs** |    **80.19 μs** |  **5,406.4 μs** |  **1.00** |    **0.02** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,344.9 μs |    364.53 μs |    19.98 μs |  1,355.0 μs |  0.25 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,170,753.86 μs** | **79,220.74 μs** | **4,342.356 μs** | **3,169,016.58 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    16,925.81 μs | 28,871.69 μs | 1,582.555 μs |    16,200.48 μs | 0.005 |    0.00 |  625208 B |        8.18 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,972.00 μs** |  **4,224.57 μs** |   **231.563 μs** | **3,165,927.59 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15,683.65 μs | 17,518.21 μs |   960.232 μs |    15,957.14 μs | 0.005 |    0.00 |  810784 B |        3.16 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,646.06 μs** | **20,012.32 μs** | **1,096.943 μs** | **3,166,058.13 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    17,583.17 μs | 17,765.39 μs |   973.781 μs |    17,846.69 μs | 0.006 |    0.00 | 1031976 B |        1.67 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,167,848.03 μs** | **14,566.92 μs** |   **798.462 μs** | **3,167,705.92 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18,100.96 μs | 60,721.39 μs | 3,328.344 μs |    18,405.88 μs | 0.006 |    0.00 | 2862480 B |        1.18 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **37.20 μs** |     **57.70 μs** |     **3.163 μs** |        **35.79 μs** |  **1.00** |    **0.10** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        32.48 μs |    281.01 μs |    15.403 μs |        24.48 μs |  0.88 |    0.37 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **40.75 μs** |     **24.77 μs** |     **1.357 μs** |        **40.10 μs** |  **1.00** |    **0.04** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        30.63 μs |     60.95 μs |     3.341 μs |        29.31 μs |  0.75 |    0.07 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **41.84 μs** |    **130.98 μs** |     **7.179 μs** |        **38.43 μs** |  **1.02** |    **0.21** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        31.41 μs |     71.17 μs |     3.901 μs |        31.65 μs |  0.76 |    0.13 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |       **200.78 μs** |  **5,064.40 μs** |   **277.597 μs** |        **45.73 μs** |  **3.49** |    **5.52** |    **2672 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        26.31 μs |     38.97 μs |     2.136 μs |        27.05 μs |  0.46 |    0.32 |    3096 B |        1.16 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|------------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.030 μs |  29.5558 μs |  1.6201 μs | 24.276 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 12.541 μs |  44.2292 μs |  2.4244 μs | 12.364 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 16.534 μs | 249.2777 μs | 13.6637 μs |  8.857 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.844 μs |   1.9835 μs |  0.1087 μs |  7.814 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 18.860 μs | 263.2477 μs | 14.4295 μs | 10.573 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.406 μs |  24.6028 μs |  1.3486 μs | 11.842 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.133 μs |   3.8225 μs |  0.2095 μs | 12.083 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.222 μs |  78.8770 μs |  4.3235 μs | 24.741 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.189 μs |   0.3798 μs |  0.0208 μs |  8.196 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 19.804 μs |  29.9769 μs |  1.6431 μs | 18.886 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 29.194 μs |  22.8090 μs |  1.2502 μs | 28.863 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.909 μs |  24.7891 μs |  1.3588 μs | 33.572 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.602 μs |   3.0655 μs |  0.1680 μs |  4.639 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.725 μs |  18.5113 μs |  1.0147 μs | 10.334 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,244.2 ns |  1,319.8 ns |  72.34 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,326.0 ns |  3,327.5 ns | 182.39 ns |  0.36 |    0.04 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,253.8 ns |  1,427.3 ns |  78.23 ns |  0.34 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,370.8 ns |    865.4 ns |  47.44 ns |  0.64 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    750.7 ns |  1,005.9 ns |  55.14 ns |  0.20 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 36,361.3 ns | 10,328.2 ns | 566.12 ns |  9.85 |    0.13 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  3,689.7 ns |    100.5 ns |   5.51 ns |  1.00 |    0.00 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,540.2 ns |  4,227.7 ns | 231.73 ns |  0.96 |    0.05 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.106 μs |  25.872 μs |  1.4181 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 463.841 μs | 142.478 μs |  7.8097 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.433 μs |   2.187 μs |  0.1199 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 217.939 μs | 283.167 μs | 15.5213 μs |      80 B |


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