---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-09 10:16 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev       | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-------------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,116.96 μs** |    **532.36 μs** |    **29.181 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,523.42 μs |  3,186.74 μs |   174.676 μs |  0.25 |    0.02 |        - |       - |   34.74 KB |        0.33 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,398.19 μs** |    **989.03 μs** |    **54.212 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,436.10 μs |    172.11 μs |     9.434 μs |  0.33 |    0.00 |  15.6250 |       - |  342.05 KB |        0.32 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,164.42 μs** |    **588.16 μs** |    **32.239 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,174.88 μs |    119.55 μs |     6.553 μs |  0.35 |    0.00 |        - |       - |   38.62 KB |        0.20 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,904.86 μs** |  **2,057.63 μs** |   **112.785 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,964.33 μs | 22,553.74 μs | 1,236.246 μs |  0.85 |    0.08 |  15.6250 |       - |  419.28 KB |        0.22 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **136.32 μs** |     **43.09 μs** |     **2.362 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     89.53 μs |    117.20 μs |     6.424 μs |  0.66 |    0.04 |        - |       - |    9.35 KB |        0.28 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,401.82 μs** |  **1,291.37 μs** |    **70.784 μs** |  **1.00** |    **0.06** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    905.37 μs |  1,406.20 μs |    77.079 μs |  0.65 |    0.06 |        - |       - |  102.57 KB |        0.31 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **645.82 μs** |  **8,005.87 μs** |   **438.829 μs** |  **1.49** |    **1.45** |   **7.3242** |       **-** |  **122.57 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    451.48 μs |  2,101.42 μs |   115.186 μs |  1.04 |    0.76 |        - |       - |  107.36 KB |        0.88 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **11,219.11 μs** | **48,056.49 μs** | **2,634.138 μs** |  **1.04** |    **0.31** |  **74.2188** |       **-** | **1226.73 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  3,630.81 μs |  4,596.07 μs |   251.926 μs |  0.34 |    0.07 |        - |       - | 1043.88 KB |        0.85 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,452.81 μs** |    **383.52 μs** |    **21.022 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,117.83 μs |     29.33 μs |     1.608 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.94 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,472.20 μs** |    **207.33 μs** |    **11.364 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.36 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,123.95 μs |     18.57 μs |     1.018 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.81 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,432.21 μs** |     **31.76 μs** |     **1.741 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,127.45 μs |    320.46 μs |    17.565 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.54 |
|                         |               |             |           |              |              |              |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,430.79 μs** |     **51.90 μs** |     **2.845 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,117.79 μs |     30.23 μs |     1.657 μs |  0.21 |    0.00 |        - |       - |     1.1 KB |        0.54 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean            | Error        | StdDev       | Median          | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |----------------:|-------------:|-------------:|----------------:|------:|--------:|----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,169,560.73 μs** | **57,365.18 μs** | **3,144.379 μs** | **3,168,106.62 μs** | **1.000** |    **0.00** |   **76408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    18,457.51 μs | 66,939.21 μs | 3,669.164 μs |    18,975.30 μs | 0.006 |    0.00 |  618416 B |        8.09 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,165,915.22 μs** | **21,557.30 μs** | **1,181.628 μs** | **3,166,024.48 μs** | **1.000** |    **0.00** |  **256408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    14,769.67 μs | 20,729.84 μs | 1,136.273 μs |    14,535.30 μs | 0.005 |    0.00 |  806488 B |        3.15 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,166,111.94 μs** | **10,236.79 μs** |   **561.113 μs** | **3,165,934.93 μs** | **1.000** |    **0.00** |  **616408 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    19,321.69 μs | 76,126.18 μs | 4,172.733 μs |    18,699.09 μs | 0.006 |    0.00 | 1031120 B |        1.67 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,166,634.57 μs** |  **8,333.89 μs** |   **456.809 μs** | **3,166,584.85 μs** | **1.000** |    **0.00** | **2424424 B** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    17,426.06 μs | 52,588.53 μs | 2,882.555 μs |    15,921.07 μs | 0.006 |    0.00 | 2838088 B |        1.17 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         |        **86.92 μs** |  **1,785.57 μs** |    **97.873 μs** |        **34.79 μs** |  **2.09** |    **2.68** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |        42.63 μs |    491.16 μs |    26.922 μs |        29.95 μs |  1.02 |    0.90 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        |        **33.29 μs** |     **72.11 μs** |     **3.953 μs** |        **33.63 μs** |  **1.01** |    **0.15** |    **2664 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |        26.41 μs |     63.75 μs |     3.494 μs |        28.35 μs |  0.80 |    0.12 |    2312 B |        0.87 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         |        **31.15 μs** |     **89.16 μs** |     **4.887 μs** |        **32.18 μs** |  **1.02** |    **0.20** |     **864 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |        25.46 μs |     81.59 μs |     4.472 μs |        23.90 μs |  0.83 |    0.18 |     512 B |        0.59 |
|                      |            |              |             |                 |              |              |                 |       |         |           |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        |        **29.60 μs** |     **59.40 μs** |     **3.256 μs** |        **27.98 μs** |  **1.01** |    **0.13** |    **2704 B** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |        27.44 μs |     23.58 μs |     1.293 μs |        27.83 μs |  0.93 |    0.09 |    2440 B |        0.90 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev    | Allocated |
|------------------------------------------------ |----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.194 μs | 11.3483 μs | 0.6220 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.157 μs |  1.7340 μs | 0.0950 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.843 μs |  5.4739 μs | 0.3000 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.486 μs |  5.2198 μs | 0.2861 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.101 μs |  1.9411 μs | 0.1064 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.577 μs |  0.9362 μs | 0.0513 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 15.832 μs | 71.0714 μs | 3.8957 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 27.339 μs |  5.2068 μs | 0.2854 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 13.535 μs | 71.8751 μs | 3.9397 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.138 μs |  0.4827 μs | 0.0265 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 30.898 μs | 40.9281 μs | 2.2434 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.321 μs | 27.0175 μs | 1.4809 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.167 μs |  6.2110 μs | 0.3404 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 10.921 μs |  4.3442 μs | 0.2381 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,229.0 ns |   938.6 ns |  51.45 ns |  0.30 |    0.01 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,368.3 ns | 1,378.6 ns |  75.57 ns |  0.33 |    0.02 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,403.0 ns | 2,867.2 ns | 157.16 ns |  0.34 |    0.03 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,572.8 ns | 1,655.4 ns |  90.74 ns |  0.62 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    724.7 ns |   288.7 ns |  15.82 ns |  0.17 |    0.00 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 40,890.3 ns | 9,464.2 ns | 518.76 ns |  9.87 |    0.12 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,143.0 ns |   364.9 ns |  20.00 ns |  1.00 |    0.01 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  3,973.7 ns | 2,607.9 ns | 142.95 ns |  0.96 |    0.03 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.062 μs |   5.004 μs |  0.2743 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 512.907 μs | 161.777 μs |  8.8675 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   6.952 μs |   1.395 μs |  0.0765 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 244.709 μs | 256.465 μs | 14.0577 μs |      80 B |


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