---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-13 06:06 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,146.1 μs** |    **809.83 μs** |    **44.39 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  1,819.0 μs |  3,219.55 μs |   176.47 μs |  0.30 |    0.02 |        - |       - |   35224 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,223.7 μs** |  **1,168.06 μs** |    **64.03 μs** |  **1.00** |    **0.01** |  **62.5000** | **15.6250** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,673.4 μs |  7,506.79 μs |   411.47 μs |  0.51 |    0.05 |  15.6250 |       - |  347368 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,628.7 μs** |    **787.46 μs** |    **43.16 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  1,752.6 μs |  1,210.61 μs |    66.36 μs |  0.26 |    0.01 |        - |       - |   37997 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **11,340.8 μs** |  **3,500.61 μs** |   **191.88 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  8,392.9 μs |  4,802.67 μs |   263.25 μs |  0.74 |    0.02 |  15.6250 |       - |  392144 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **119.0 μs** |    **119.36 μs** |     **6.54 μs** |  **1.00** |    **0.07** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    134.3 μs |    252.15 μs |    13.82 μs |  1.13 |    0.12 |        - |       - |    4254 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,274.7 μs** |    **923.35 μs** |    **50.61 μs** |  **1.00** |    **0.05** |  **20.5078** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,347.2 μs |  2,110.15 μs |   115.66 μs |  1.06 |    0.09 |        - |       - |   42672 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **974.0 μs** |    **155.28 μs** |     **8.51 μs** |  **1.00** |    **0.01** |   **7.3242** |       **-** |  **125628 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    890.6 μs |    973.29 μs |    53.35 μs |  0.91 |    0.05 |        - |       - |    7562 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **8,798.9 μs** | **29,032.44 μs** | **1,591.37 μs** |  **1.02** |    **0.24** |  **74.2188** |       **-** | **1254880 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,627.5 μs | 16,796.01 μs |   920.65 μs |  0.89 |    0.18 |        - |       - |   93141 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,585.5 μs** |    **429.20 μs** |    **23.53 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  1,555.4 μs |    250.66 μs |    13.74 μs |  0.28 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,431.0 μs** |     **83.14 μs** |     **4.56 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  1,564.6 μs |     87.99 μs |     4.82 μs |  0.29 |    0.00 |        - |       - |     832 B |        0.69 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,433.0 μs** |    **133.42 μs** |     **7.31 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  1,578.0 μs |    608.95 μs |    33.38 μs |  0.29 |    0.01 |        - |       - |     832 B |        0.40 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,447.2 μs** |    **278.53 μs** |    **15.27 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  1,563.6 μs |    200.53 μs |    10.99 μs |  0.29 |    0.00 |        - |       - |     832 B |        0.40 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **133.0 μs** |    **588.2 μs** |  **32.24 μs** |   **150.0 μs** |  **1.05** |    **0.34** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   161.0 μs |    513.4 μs |  28.14 μs |   147.9 μs |  1.27 |    0.37 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **147.7 μs** |    **903.5 μs** |  **49.52 μs** |   **127.6 μs** |  **1.07** |    **0.42** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   216.9 μs |    622.7 μs |  34.13 μs |   229.0 μs |  1.57 |    0.45 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,031.4 μs** |  **8,223.8 μs** | **450.77 μs** |   **773.9 μs** |  **1.11** |    **0.55** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,110.7 μs |  2,961.4 μs | 162.32 μs | 1,026.0 μs |  1.20 |    0.40 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,666.2 μs** | **15,176.9 μs** | **831.90 μs** | **1,477.7 μs** |  **1.18** |    **0.73** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,601.3 μs |  7,086.3 μs | 388.42 μs | 1,397.9 μs |  1.13 |    0.53 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **822.5 ns** |   **737.4 ns** |  **40.42 ns** |  **1.00** |    **0.06** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,722.2 ns |   310.3 ns |  17.01 ns |  2.10 |    0.09 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,403.4 ns** | **1,438.7 ns** |  **78.86 ns** |  **1.00** |    **0.07** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,117.9 ns | 2,341.0 ns | 128.32 ns |  2.23 |    0.14 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 27.615 μs |  47.275 μs |  2.5913 μs | 26.149 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.240 μs |   1.206 μs |  0.0661 μs | 11.230 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.626 μs |   5.310 μs |  0.2910 μs |  8.606 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 10.302 μs |  55.212 μs |  3.0264 μs |  8.635 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.458 μs |   3.514 μs |  0.1926 μs | 11.491 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.661 μs |   1.916 μs |  0.1050 μs | 12.664 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 13.100 μs |   2.874 μs |  0.1575 μs | 13.154 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 36.999 μs | 315.487 μs | 17.2929 μs | 27.170 μs |         - |
| &#39;Read 1000 Int32s&#39;                              |  8.946 μs |   1.277 μs |  0.0700 μs |  8.916 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 22.625 μs |  77.344 μs |  4.2395 μs | 20.177 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 37.650 μs | 163.937 μs |  8.9859 μs | 34.254 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 33.624 μs |  32.193 μs |  1.7646 μs | 33.538 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.973 μs |   2.844 μs |  0.1559 μs |  5.020 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.909 μs |  23.302 μs |  1.2773 μs | 11.381 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,915.42 ns | 325.497 ns | 17.842 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.55 ns |   0.687 ns |  0.038 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.83 ns |   0.167 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     37.25 ns |   2.025 ns |  0.111 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     35.59 ns |   5.951 ns |  0.326 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.90 ns |   3.831 ns |  0.210 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    124.11 ns |  48.184 ns |  2.641 ns |  1.00 |    0.03 | 0.0534 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     76.56 ns |   1.649 ns |  0.090 ns |  0.62 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  11.421 μs |   1.567 μs | 0.0859 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 506.699 μs | 161.269 μs | 8.8397 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.257 μs |   8.468 μs | 0.4641 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 227.956 μs |   2.673 μs | 0.1465 μs |      80 B |


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