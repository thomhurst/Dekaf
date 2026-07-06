---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 09:33 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,072.52 μs** |     **91.97 μs** |     **5.041 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,534.11 μs |  2,518.28 μs |   138.035 μs |  0.25 |    0.02 |        - |       - |   34.82 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,392.86 μs** |    **485.57 μs** |    **26.616 μs** |  **1.00** |    **0.00** |  **62.5000** | **31.2500** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,348.91 μs |    614.93 μs |    33.706 μs |  0.32 |    0.00 |  15.6250 |       - |  340.02 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,283.44 μs** |    **592.65 μs** |    **32.485 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,559.39 μs |  1,295.49 μs |    71.010 μs |  0.25 |    0.01 |        - |       - |   36.85 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,364.85 μs** |  **6,489.24 μs** |   **355.697 μs** |  **1.00** |    **0.04** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,816.45 μs |  4,366.43 μs |   239.339 μs |  0.55 |    0.02 |  15.6250 |       - |  364.32 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **133.55 μs** |     **52.36 μs** |     **2.870 μs** |  **1.00** |    **0.03** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     85.85 μs |     34.40 μs |     1.885 μs |  0.64 |    0.02 |        - |       - |     4.3 KB |        0.13 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,325.00 μs** |    **207.44 μs** |    **11.371 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    735.16 μs |    790.66 μs |    43.339 μs |  0.55 |    0.03 |        - |       - |   97.75 KB |        0.29 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **599.26 μs** |  **7,822.33 μs** |   **428.768 μs** |  **1.46** |    **1.40** |   **7.3242** |       **-** |  **122.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    517.69 μs |  2,344.05 μs |   128.485 μs |  1.26 |    0.86 |        - |       - |   81.85 KB |        0.67 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,060.94 μs** | **34,587.35 μs** | **1,895.849 μs** |  **1.03** |    **0.25** |  **74.2188** |       **-** | **1226.25 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,985.67 μs |  1,370.66 μs |    75.130 μs |  0.30 |    0.06 |        - |       - |  724.03 KB |        0.59 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,418.49 μs** |     **64.38 μs** |     **3.529 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,322.03 μs |  1,427.83 μs |    78.264 μs |  0.24 |    0.01 |        - |       - |    1.19 KB |        1.01 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,424.94 μs** |    **395.18 μs** |    **21.661 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,389.27 μs |    171.46 μs |     9.398 μs |  0.26 |    0.00 |        - |       - |    1.19 KB |        1.01 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,425.90 μs** |     **72.92 μs** |     **3.997 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,354.56 μs |    178.96 μs |     9.809 μs |  0.25 |    0.00 |        - |       - |    1.19 KB |        0.58 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,416.98 μs** |     **70.07 μs** |     **3.841 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,259.06 μs |    171.06 μs |     9.376 μs |  0.23 |    0.00 |        - |       - |    1.19 KB |        0.58 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error         | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|--------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169,193.33 μs** |  **78,701.70 μs** | **4,313.906 μs** | **3,167,967.73 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15,148.74 μs |  31,402.72 μs | 1,721.289 μs |    14,178.48 μs | 0.005 |    0.00 |  616792 B |        8.07 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,167,631.03 μs** |  **34,193.47 μs** | **1,874.259 μs** | **3,168,070.60 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,868.67 μs |  52,518.62 μs | 2,878.723 μs |    14,063.06 μs | 0.005 |    0.00 |  808824 B |        3.15 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165,513.19 μs** |   **3,517.36 μs** |   **192.798 μs** | **3,165,434.38 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    20,452.03 μs | 178,904.15 μs | 9,806.340 μs |    16,197.67 μs | 0.006 |    0.00 | 1075480 B |        1.74 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164,921.75 μs** |   **8,542.60 μs** |   **468.249 μs** | **3,165,043.32 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    18,049.35 μs |  71,812.88 μs | 3,936.307 μs |    17,687.87 μs | 0.006 |    0.00 | 2864808 B |        1.18 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **30.03 μs** |     **141.22 μs** |     **7.741 μs** |        **26.05 μs** |  **1.04** |    **0.31** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        23.25 μs |      50.77 μs |     2.783 μs |        22.62 μs |  0.81 |    0.18 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **24.49 μs** |      **51.72 μs** |     **2.835 μs** |        **23.66 μs** |  **1.01** |    **0.14** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        23.68 μs |      49.56 μs |     2.717 μs |        22.35 μs |  0.98 |    0.14 |    2312 B |        0.87 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **25.98 μs** |      **34.48 μs** |     **1.890 μs** |        **26.44 μs** |  **1.00** |    **0.09** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        30.57 μs |     200.86 μs |    11.010 μs |        26.03 μs |  1.18 |    0.38 |     512 B |        0.59 |
|                      |            |              |             |                 |               |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **29.32 μs** |      **82.05 μs** |     **4.497 μs** |        **28.64 μs** |  **1.02** |    **0.19** |    **3784 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        30.48 μs |     149.52 μs |     8.196 μs |        27.57 μs |  1.06 |    0.28 |    3312 B |        0.88 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error       | StdDev    | Allocated |
|------------------------------------------------ |----------:|------------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.153 μs |   2.6438 μs | 0.1449 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.078 μs |   4.2261 μs | 0.2316 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.728 μs |   5.1520 μs | 0.2824 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.879 μs |   0.8360 μs | 0.0458 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.487 μs |  72.5210 μs | 3.9751 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.312 μs |   1.2450 μs | 0.0682 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 37.493 μs |  73.8357 μs | 4.0472 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 38.839 μs | 123.8997 μs | 6.7914 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.588 μs |   1.4686 μs | 0.0805 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.947 μs |   3.8037 μs | 0.2085 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,463.0 ns |   912.2 ns |  50.00 ns |  0.35 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,336.7 ns | 1,079.4 ns |  59.16 ns |  0.32 |    0.01 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,309.3 ns | 1,769.8 ns |  97.01 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,648.7 ns | 1,163.9 ns |  63.80 ns |  0.63 |    0.01 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    777.7 ns | 1,214.7 ns |  66.58 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 38,906.3 ns | 7,245.6 ns | 397.16 ns |  9.25 |    0.09 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,206.2 ns |   416.2 ns |  22.81 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,108.3 ns | 1,114.3 ns |  61.08 ns |  0.98 |    0.01 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.566 μs |  3.2276 μs | 0.1769 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 499.299 μs | 81.7889 μs | 4.4831 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.979 μs |  0.5594 μs | 0.0307 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 239.521 μs | 55.0541 μs | 3.0177 μs |      80 B |


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