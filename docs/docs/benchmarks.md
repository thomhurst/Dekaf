---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 03:07 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,195.7 μs** |    **175.26 μs** |     **9.61 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,816.0 μs |  2,947.83 μs |   161.58 μs |  0.29 |    0.02 |        - |       - |   35224 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,388.0 μs** |    **899.89 μs** |    **49.33 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,664.2 μs |  5,371.30 μs |   294.42 μs |  0.50 |    0.03 |  15.6250 |       - |  346741 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,528.6 μs** |    **731.20 μs** |    **40.08 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,837.4 μs |  2,141.05 μs |   117.36 μs |  0.28 |    0.02 |        - |       - |   38075 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,268.3 μs** |  **2,236.17 μs** |   **122.57 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,266.6 μs |  7,411.74 μs |   406.26 μs |  0.84 |    0.03 |        - |       - |  367595 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **119.7 μs** |    **163.04 μs** |     **8.94 μs** |  **1.00** |    **0.09** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    235.1 μs |  1,993.28 μs |   109.26 μs |  1.97 |    0.80 |        - |       - |    4209 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,287.7 μs** |    **372.64 μs** |    **20.43 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,556.7 μs |  3,273.29 μs |   179.42 μs |  1.21 |    0.12 |        - |       - |   43233 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,006.6 μs** |      **9.20 μs** |     **0.50 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125384 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    933.8 μs |    575.28 μs |    31.53 μs |  0.93 |    0.03 |        - |       - |    9343 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,220.0 μs** | **27,554.71 μs** | **1,510.37 μs** |  **1.02** |    **0.22** |  **74.2188** |       **-** | **1254446 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  8,847.9 μs | 27,612.00 μs | 1,513.51 μs |  0.98 |    0.21 |        - |       - |   87103 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,491.4 μs** |     **53.81 μs** |     **2.95 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,553.2 μs |     72.74 μs |     3.99 μs |  0.28 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,469.2 μs** |     **32.75 μs** |     **1.80 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,566.7 μs |    130.81 μs |     7.17 μs |  0.29 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,474.0 μs** |     **79.54 μs** |     **4.36 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,571.6 μs |    180.92 μs |     9.92 μs |  0.29 |    0.00 |        - |       - |     832 B |        0.40 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,478.3 μs** |     **23.82 μs** |     **1.31 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2290 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,577.0 μs |     75.41 μs |     4.13 μs |  0.29 |    0.00 |        - |       - |     832 B |        0.36 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **115.0 μs** |    **470.5 μs** |  **25.79 μs** |   **107.3 μs** |  **1.03** |    **0.28** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   169.6 μs |    405.6 μs |  22.23 μs |   168.0 μs |  1.52 |    0.33 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **137.8 μs** |    **637.0 μs** |  **34.91 μs** |   **119.9 μs** |  **1.04** |    **0.31** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   180.2 μs |    307.0 μs |  16.83 μs |   179.9 μs |  1.36 |    0.28 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **970.7 μs** |  **2,755.1 μs** | **151.02 μs** |   **895.4 μs** |  **1.02** |    **0.19** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,297.2 μs |  2,297.4 μs | 125.93 μs | 1,338.2 μs |  1.36 |    0.20 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,459.9 μs** | **12,435.5 μs** | **681.63 μs** | **1,100.0 μs** |  **1.13** |    **0.60** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,723.3 μs | 11,626.9 μs | 637.31 μs | 1,367.5 μs |  1.33 |    0.62 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **866.3 ns** | **1,762.9 ns** |  **96.63 ns** |  **1.01** |    **0.14** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,054.5 ns | 2,109.8 ns | 115.65 ns |  2.39 |    0.26 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,408.0 ns** | **2,089.4 ns** | **114.53 ns** |  **1.00** |    **0.10** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,398.8 ns | 5,710.6 ns | 313.02 ns |  2.42 |    0.25 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.474 μs |   6.023 μs |  0.3301 μs | 32.418 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 10.653 μs |   6.858 μs |  0.3759 μs | 10.584 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 14.520 μs |  15.257 μs |  0.8363 μs | 14.059 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  8.321 μs |   9.742 μs |  0.5340 μs |  8.077 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 15.162 μs | 113.733 μs |  6.2341 μs | 11.870 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.373 μs |  11.106 μs |  0.6087 μs | 12.418 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.355 μs |   9.024 μs |  0.4946 μs | 12.269 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.247 μs |   7.830 μs |  0.4292 μs | 26.020 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.877 μs |   5.139 μs |  0.2817 μs | 11.788 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 25.499 μs | 276.766 μs | 15.1705 μs | 17.258 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 44.948 μs |  61.765 μs |  3.3856 μs | 43.935 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 46.655 μs |  46.868 μs |  2.5690 μs | 45.875 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.614 μs |   8.210 μs |  0.4500 μs |  5.745 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.508 μs |  13.246 μs |  0.7261 μs | 11.383 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,103.10 ns | 61.714 ns | 3.383 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     14.34 ns |  0.701 ns | 0.038 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     19.92 ns |  0.106 ns | 0.006 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     35.95 ns |  0.859 ns | 0.047 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     40.62 ns | 16.240 ns | 0.890 ns |     ? |       ? | 0.0089 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     10.28 ns |  0.277 ns | 0.015 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    128.83 ns | 54.283 ns | 2.975 ns |  1.00 |    0.03 | 0.0355 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     80.69 ns |  0.647 ns | 0.035 ns |  0.63 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  13.097 μs |  21.137 μs |  1.159 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 463.152 μs | 293.197 μs | 16.071 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   9.511 μs |  20.147 μs |  1.104 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 210.252 μs | 231.342 μs | 12.681 μs |      80 B |


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