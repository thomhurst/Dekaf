---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-15 08:58 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|-----------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       | **6,022.60 μs** |  **2,217.80 μs** | **121.565 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       | 3,437.39 μs |  4,341.30 μs | 237.961 μs |  0.57 |    0.04 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      | **6,978.46 μs** |  **1,271.10 μs** |  **69.673 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      | 3,414.82 μs |    889.07 μs |  48.733 μs |  0.49 |    0.01 |  15.6250 |       - |  347443 B |        0.32 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       | **6,636.46 μs** |  **7,972.90 μs** | **437.022 μs** |  **1.00** |    **0.08** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       | 3,260.88 μs |    147.66 μs |   8.094 μs |  0.49 |    0.03 |        - |       - |   37469 B |        0.19 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **9,297.39 μs** |  **4,800.89 μs** | **263.153 μs** |  **1.00** |    **0.03** | **109.3750** | **46.8750** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 7,639.60 μs |  3,581.10 μs | 196.292 μs |  0.82 |    0.03 |  15.6250 |       - |  378855 B |        0.19 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **90.05 μs** |     **19.34 μs** |   **1.060 μs** |  **1.00** |    **0.01** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    86.66 μs |     77.93 μs |   4.272 μs |  0.96 |    0.04 |   0.2441 |       - |    4374 B |        0.13 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |   **848.82 μs** |    **736.68 μs** |  **40.380 μs** |  **1.00** |    **0.06** |  **20.5078** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |   873.41 μs |    519.74 μs |  28.489 μs |  1.03 |    0.05 |        - |       - |   42685 B |        0.12 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |   **807.55 μs** |  **1,200.23 μs** |  **65.788 μs** |  **1.00** |    **0.10** |   **7.3242** |       **-** |  **125540 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |   581.60 μs |    640.81 μs |  35.125 μs |  0.72 |    0.06 |        - |       - |    8845 B |        0.07 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **7,887.23 μs** |  **4,683.09 μs** | **256.696 μs** |  **1.00** |    **0.04** |  **74.2188** |       **-** | **1251788 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 5,305.26 μs |  5,697.14 μs | 312.280 μs |  0.67 |    0.04 |        - |       - |  115178 B |        0.09 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       | **5,637.19 μs** |  **8,615.97 μs** | **472.271 μs** |  **1.00** |    **0.10** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       | 3,470.27 μs |  3,537.72 μs | 193.914 μs |  0.62 |    0.05 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      | **5,733.70 μs** | **10,529.12 μs** | **577.137 μs** |  **1.01** |    **0.12** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      | 3,585.45 μs |  6,596.31 μs | 361.566 μs |  0.63 |    0.08 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       | **5,447.52 μs** |  **1,093.39 μs** |  **59.932 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       | 3,411.67 μs |    475.55 μs |  26.067 μs |  0.63 |    0.01 |        - |       - |     808 B |        0.39 |
|                         |               |             |           |             |              |            |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      | **5,361.32 μs** |    **685.20 μs** |  **37.558 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      | 3,376.52 μs |    263.79 μs |  14.459 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median      | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|------------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **100.5 μs** |    **505.73 μs** |  **27.72 μs** |   **114.23 μs** |  **1.06** |    **0.40** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   118.2 μs |    291.35 μs |  15.97 μs |   109.33 μs |  1.25 |    0.39 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **120.2 μs** |    **915.94 μs** |  **50.21 μs** |    **92.19 μs** |  **1.10** |    **0.52** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   139.5 μs |     45.07 μs |   2.47 μs |   138.91 μs |  1.28 |    0.37 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **679.4 μs** |  **2,838.39 μs** | **155.58 μs** |   **595.74 μs** |  **1.03** |    **0.28** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         |   937.9 μs |  2,042.58 μs | 111.96 μs |   879.01 μs |  1.42 |    0.29 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |             |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,323.8 μs** | **11,460.41 μs** | **628.18 μs** | **1,172.35 μs** |  **1.16** |    **0.67** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,858.2 μs | 11,399.46 μs | 624.84 μs | 2,193.32 μs |  1.62 |    0.80 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **601.8 ns** | **2,089.8 ns** | **114.55 ns** |  **1.02** |    **0.23** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 1,539.2 ns | 2,093.0 ns | 114.73 ns |  2.62 |    0.43 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,068.9 ns** |   **624.1 ns** |  **34.21 ns** |  **1.00** |    **0.04** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 2,695.3 ns | 2,255.3 ns | 123.62 ns |  2.52 |    0.12 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 32.516 μs |   6.627 μs |  0.3633 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.020 μs |   3.117 μs |  0.1709 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.750 μs |   4.675 μs |  0.2563 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.494 μs |   2.898 μs |  0.1589 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 10.807 μs |   9.392 μs |  0.5148 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.849 μs |  15.642 μs |  0.8574 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 14.815 μs |  36.472 μs |  1.9992 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 39.511 μs | 212.617 μs | 11.6543 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.183 μs |   6.136 μs |  0.3363 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.183 μs |   4.702 μs |  0.2577 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.779 μs |  29.640 μs |  1.6247 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 25.577 μs |  38.493 μs |  2.1099 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.941 μs |   9.658 μs |  0.5294 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 14.478 μs |   7.497 μs |  0.4109 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,942.80 ns | 104.171 ns | 5.710 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.71 ns |   0.167 ns | 0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     20.72 ns |   0.232 ns | 0.013 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     39.73 ns |   0.254 ns | 0.014 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.74 ns |   5.601 ns | 0.307 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.04 ns |   2.191 ns | 0.120 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    110.07 ns |  11.186 ns | 0.613 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     81.72 ns |   1.057 ns | 0.058 ns |  0.74 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  10.643 μs |   8.683 μs |  0.4759 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 505.647 μs |  73.631 μs |  4.0360 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.248 μs |   8.810 μs |  0.4829 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 243.375 μs | 228.542 μs | 12.5271 μs |      80 B |


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