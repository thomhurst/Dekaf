---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-06 06:56 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
**Ratio < 1.0 means Dekaf is faster than Confluent.Kafka**
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean         | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |-------------:|-------------:|-----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,082.19 μs** |    **297.81 μs** |  **16.324 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **106.53 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,443.24 μs |  1,574.11 μs |  86.282 μs |  0.24 |    0.01 |        - |       - |   34.68 KB |        0.33 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,324.55 μs** |    **911.63 μs** |  **49.970 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** |  **1062.8 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  2,344.41 μs |    427.10 μs |  23.411 μs |  0.32 |    0.00 |  15.6250 |       - |  339.62 KB |        0.32 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,614.82 μs** |    **569.08 μs** |  **31.193 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194.04 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,586.76 μs |  2,583.11 μs | 141.589 μs |  0.24 |    0.02 |        - |       - |   36.38 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,157.32 μs** |  **1,373.84 μs** |  **75.305 μs** |  **1.00** |    **0.01** | **109.3750** | **31.2500** | **1937.81 KB** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  6,468.91 μs |  4,809.66 μs | 263.634 μs |  0.53 |    0.02 |  15.6250 |       - |  361.21 KB |        0.19 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **132.95 μs** |     **69.97 μs** |   **3.835 μs** |  **1.00** |    **0.04** |   **1.9531** |       **-** |   **33.52 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |     76.91 μs |    118.03 μs |   6.470 μs |  0.58 |    0.04 |        - |       - |    4.09 KB |        0.12 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,320.62 μs** |    **614.03 μs** |  **33.657 μs** |  **1.00** |    **0.03** |  **19.5313** |       **-** |  **335.86 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |    777.35 μs |  1,719.77 μs |  94.266 μs |  0.59 |    0.06 |        - |       - |   67.07 KB |        0.20 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **594.68 μs** |  **7,213.94 μs** | **395.420 μs** |  **1.42** |    **1.31** |   **7.3242** |       **-** |  **122.44 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    248.99 μs |    444.67 μs |  24.374 μs |  0.60 |    0.38 |   0.9766 |       - |   95.38 KB |        0.78 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,958.05 μs** | **17,994.32 μs** | **986.329 μs** |  **1.01** |    **0.12** |  **74.2188** |       **-** | **1225.15 KB** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  2,209.48 μs |  2,133.07 μs | 116.921 μs |  0.22 |    0.02 |   7.8125 |       - |  961.12 KB |        0.78 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,394.53 μs** |     **27.51 μs** |   **1.508 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,381.85 μs |    558.07 μs |  30.590 μs |  0.26 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,394.59 μs** |     **90.04 μs** |   **4.936 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1.17 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,352.69 μs |    379.01 μs |  20.775 μs |  0.25 |    0.00 |        - |       - |    1.14 KB |        0.97 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,399.30 μs** |     **49.61 μs** |   **2.719 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,283.29 μs |    350.33 μs |  19.203 μs |  0.24 |    0.00 |        - |       - |    1.14 KB |        0.56 |
|                         |               |             |           |              |              |            |       |         |          |         |            |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,391.97 μs** |     **36.48 μs** |   **1.999 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2.05 KB** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,109.45 μs |     11.16 μs |   0.612 μs |  0.21 |    0.00 |        - |       - |    1.14 KB |        0.56 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | Categories | MessageCount | MessageSize | Mean         | Error        | StdDev      | Median       | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |----------- |------------- |------------ |-------------:|-------------:|------------:|-------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **100**         | **3,167.056 ms** |    **12.500 ms** |   **0.6852 ms** | **3,167.045 ms** | **1.000** |    **0.00** |   **74.62 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 100         |    15.070 ms |    20.632 ms |   1.1309 ms |    15.573 ms | 0.005 |    0.00 |  607.76 KB |        8.15 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **100**          | **1000**        | **3,166.422 ms** |    **63.294 ms** |   **3.4694 ms** | **3,164.526 ms** | **1.000** |    **0.00** |   **250.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 100          | 1000        |    13.166 ms |    19.960 ms |   1.0941 ms |    12.798 ms | 0.004 |    0.00 |  787.88 KB |        3.15 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **100**         | **3,165.089 ms** |    **13.596 ms** |   **0.7453 ms** | **3,165.137 ms** | **1.000** |    **0.00** |  **601.96 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 100         |    18.968 ms |    12.896 ms |   0.7068 ms |    18.628 ms | 0.006 |    0.00 |  1023.3 KB |        1.70 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_ConsumeAll** | **ConsumeAll** | **1000**         | **1000**        | **3,164.815 ms** |    **19.424 ms** |   **1.0647 ms** | **3,164.538 ms** | **1.000** |    **0.00** |  **2367.6 KB** |        **1.00** |
| Dekaf_ConsumeAll     | ConsumeAll | 1000         | 1000        |    15.012 ms |    13.418 ms |   0.7355 ms |    15.381 ms | 0.005 |    0.00 | 2845.98 KB |        1.20 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **100**         | **3,471.673 ms** | **9,966.551 ms** | **546.3003 ms** | **3,157.696 ms** | **1.015** |    **0.19** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 100         |     5.776 ms |    12.925 ms |   0.7085 ms |     6.021 ms | 0.002 |    0.00 |  184.98 KB |       76.87 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **100**          | **1000**        | **3,155.852 ms** |    **10.058 ms** |   **0.5513 ms** | **3,155.701 ms** | **1.000** |    **0.00** |    **4.16 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 100          | 1000        |     7.290 ms |    66.421 ms |   3.6407 ms |     5.400 ms | 0.002 |    0.00 |  189.16 KB |       45.43 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **100**         | **3,156.513 ms** |    **17.869 ms** |   **0.9794 ms** | **3,156.191 ms** | **1.000** |    **0.00** |    **2.41 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 100         |     6.590 ms |    15.012 ms |   0.8229 ms |     6.511 ms | 0.002 |    0.00 |  203.28 KB |       84.48 |
|                      |            |              |             |              |              |             |              |       |         |            |             |
| **Confluent_PollSingle** | **PollSingle** | **1000**         | **1000**        | **3,156.684 ms** |    **27.839 ms** |   **1.5259 ms** | **3,157.032 ms** | **1.000** |    **0.00** |    **4.18 KB** |        **1.00** |
| Dekaf_PollSingle     | PollSingle | 1000         | 1000        |     5.892 ms |    10.420 ms |   0.5711 ms |     5.812 ms | 0.002 |    0.00 |   187.7 KB |       44.91 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 26.115 μs |   2.281 μs |  0.1250 μs | 26.058 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.150 μs |   2.465 μs |  0.1351 μs | 10.190 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.390 μs |   1.095 μs |  0.0600 μs | 10.390 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 34.094 μs | 117.060 μs |  6.4164 μs | 36.559 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.992 μs |   2.844 μs |  0.1559 μs |  8.902 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 26.571 μs | 202.181 μs | 11.0822 μs | 20.183 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 40.242 μs | 252.074 μs | 13.8170 μs | 33.282 μs |    2424 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 36.361 μs |  12.469 μs |  0.6835 μs | 36.508 μs |    2464 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.826 μs |   1.045 μs |  0.0573 μs |  4.809 μs |         - |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 12.587 μs |   2.621 μs |  0.1436 μs | 12.643 μs |         - |


## Serializer Benchmarks

| Method                               | Mean        | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------- |------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Serialize String (10 chars)&#39;        |  1,322.0 ns | 1,277.1 ns |  70.00 ns |  0.31 |    0.02 |         - |        0.00 |
| &#39;Serialize String (100 chars)&#39;       |  1,374.0 ns | 2,592.7 ns | 142.12 ns |  0.33 |    0.03 |         - |        0.00 |
| &#39;Serialize String (1000 chars)&#39;      |  1,682.0 ns | 5,423.8 ns | 297.30 ns |  0.40 |    0.06 |         - |        0.00 |
| &#39;Deserialize String&#39;                 |  2,585.0 ns | 1,651.1 ns |  90.50 ns |  0.61 |    0.02 |     224 B |        0.21 |
| &#39;Serialize Int32&#39;                    |    803.7 ns | 2,313.7 ns | 126.82 ns |  0.19 |    0.03 |         - |        0.00 |
| &#39;Serialize 100 Messages (key+value)&#39; | 39,348.2 ns | 3,956.4 ns | 216.86 ns |  9.35 |    0.19 |    3920 B |        3.74 |
| &#39;ArrayBufferWriter + Copy&#39;           |  4,212.0 ns | 1,705.3 ns |  93.47 ns |  1.00 |    0.03 |    1048 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          |  4,204.0 ns | 9,129.5 ns | 500.42 ns |  1.00 |    0.10 |     536 B |        0.51 |


## Compression Benchmarks

| Method                  | Mean         | Error      | StdDev     | Allocated |
|------------------------ |-------------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    10.920 μs |   2.337 μs |  0.1281 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   |   554.739 μs | 149.807 μs |  8.2114 μs |     768 B |
| &#39;Snappy Decompress 1KB&#39; |     8.205 μs |   3.523 μs |  0.1931 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 1,674.422 μs | 311.625 μs | 17.0812 μs |    1280 B |


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