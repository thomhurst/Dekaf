---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 02:52 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,151.80 μs** |    **546.12 μs** |    **29.935 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,340.96 μs |  1,050.81 μs |    57.598 μs |  0.22 |    0.01 |        - |       - |   34.69 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,445.88 μs** |  **1,356.51 μs** |    **74.355 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,373.51 μs |    274.82 μs |    15.064 μs |  0.32 |    0.00 |  15.6250 |       - |  339.49 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,196.81 μs** |    **678.07 μs** |    **37.167 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,379.99 μs |  3,011.86 μs |   165.090 μs |  0.22 |    0.02 |        - |       - |   36.31 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,756.39 μs** |  **5,195.49 μs** |   **284.782 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,626.65 μs |  2,939.69 μs |   161.134 μs |  0.52 |    0.01 |  15.6250 |       - |  361.69 KB |        0.19 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **138.18 μs** |     **32.38 μs** |     **1.775 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     74.51 μs |     83.75 μs |     4.591 μs |  0.54 |    0.03 |        - |       - |    7.48 KB |        0.22 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,423.33 μs** |    **185.19 μs** |    **10.151 μs** |  **1.00** |    **0.01** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    604.53 μs |  1,225.11 μs |    67.152 μs |  0.42 |    0.04 |        - |       - |  111.52 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **646.05 μs** |  **7,929.60 μs** |   **434.648 μs** |  **1.49** |    **1.46** |   **7.3242** |       **-** |  **122.57 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    225.44 μs |    661.63 μs |    36.266 μs |  0.52 |    0.37 |   0.9766 |       - |   97.89 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,275.86 μs** | **28,227.23 μs** | **1,547.230 μs** |  **1.02** |    **0.20** |  **74.2188** |       **-** | **1226.43 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,101.97 μs |  3,504.62 μs |   192.100 μs |  0.21 |    0.03 |   7.8125 |       - |     983 KB |        0.80 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,423.24 μs** |     **80.71 μs** |     **4.424 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,119.31 μs |    109.53 μs |     6.003 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,423.33 μs** |     **66.44 μs** |     **3.642 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,117.66 μs |     47.77 μs |     2.619 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,427.46 μs** |    **104.65 μs** |     **5.736 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,120.88 μs |     88.20 μs |     4.834 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,453.46 μs** |    **130.07 μs** |     **7.130 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,117.19 μs |     20.55 μs |     1.127 μs |  0.20 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error      | StdDev    | Median       | Ratio | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-----------:|----------:|-------------:|------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,168.671 ms** |  **22.486 ms** | **1.2325 ms** | **3,168.216 ms** | **1.000** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    14.759 ms |  26.224 ms | 1.4374 ms |    14.249 ms | 0.005 |  600.35 KB |        8.05 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165.043 ms** |  **14.127 ms** | **0.7743 ms** | **3,165.070 ms** | **1.000** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    15.769 ms |  42.563 ms | 2.3330 ms |    17.017 ms | 0.005 |  803.86 KB |        3.21 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.149 ms** |   **9.952 ms** | **0.5455 ms** | **3,165.160 ms** | **1.000** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.339 ms | 124.058 ms | 6.8000 ms |    14.424 ms | 0.006 | 1003.54 KB |        1.67 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,165.676 ms** |  **10.475 ms** | **0.5742 ms** | **3,165.760 ms** | **1.000** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.410 ms |  32.783 ms | 1.7969 ms |    14.688 ms | 0.005 | 2843.88 KB |        1.20 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,156.343 ms** |   **5.457 ms** | **0.2991 ms** | **3,156.411 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     6.629 ms |  40.391 ms | 2.2140 ms |     5.663 ms | 0.002 |  184.54 KB |       76.69 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,156.104 ms** |  **26.517 ms** | **1.4535 ms** | **3,155.784 ms** | **1.000** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     5.489 ms |  10.711 ms | 0.5871 ms |     5.431 ms | 0.002 |  187.66 KB |       45.07 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.691 ms** |  **23.834 ms** | **1.3064 ms** | **3,156.359 ms** | **1.000** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.560 ms |  10.441 ms | 0.5723 ms |     6.409 ms | 0.002 |  184.72 KB |       76.77 |
|                      |            |              |             |              |            |           |              |       |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,155.550 ms** |   **9.482 ms** | **0.5198 ms** | **3,155.690 ms** | **1.000** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     6.682 ms |  15.641 ms | 0.8574 ms |     6.382 ms | 0.002 |  188.63 KB |       45.13 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 25.932 μs |   2.828 μs |  0.1550 μs | 25.869 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 20.058 μs | 222.733 μs | 12.2087 μs | 13.184 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.213 μs |   3.063 μs |  0.1679 μs | 10.249 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.790 μs |   1.680 μs |  0.0921 μs | 26.810 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.946 μs |   2.534 μs |  0.1389 μs |  8.876 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.657 μs | 200.291 μs | 10.9786 μs | 20.328 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 17.793 μs |   2.659 μs |  0.1458 μs | 17.803 μs |    2416 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 20.856 μs |   9.575 μs |  0.5249 μs | 20.929 μs |    2456 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.635 μs |   1.899 μs |  0.1041 μs |  4.668 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.927 μs |   4.399 μs |  0.2411 μs | 10.900 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|------------:|------------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,840.7 ns |  4,688.1 ns |   256.97 ns |  0.44 |    0.06 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,238.8 ns |    450.7 ns |    24.70 ns |  0.30 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,615.7 ns |  2,312.0 ns |   126.73 ns |  0.39 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,949.3 ns |  3,103.2 ns |   170.10 ns |  0.71 |    0.05 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    769.8 ns |    759.5 ns |    41.63 ns |  0.18 |    0.01 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 43,597.7 ns | 98,540.4 ns | 5,401.33 ns | 10.46 |    1.25 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,177.3 ns |  4,495.9 ns |   246.44 ns |  1.00 |    0.07 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,874.0 ns |  3,808.0 ns |   208.73 ns |  0.93 |    0.06 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    11.111 μs |   8.980 μs |  0.4922 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   541.572 μs | 426.467 μs | 23.3761 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     9.190 μs |  12.310 μs |  0.6747 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,647.665 μs |  72.095 μs |  3.9518 μs |    1280 B |


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