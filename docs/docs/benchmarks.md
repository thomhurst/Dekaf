---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 12:27 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,213.5 μs** |    **438.77 μs** |    **24.05 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,392.4 μs |    242.33 μs |    13.28 μs |  0.55 |    0.00 |        - |       - |   35192 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,347.4 μs** |    **999.61 μs** |    **54.79 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1088306 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,819.2 μs |  4,787.13 μs |   262.40 μs |  0.52 |    0.03 |  15.6250 |       - |  347605 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,193.2 μs** |  **1,000.06 μs** |    **54.82 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,344.1 μs |    194.52 μs |    10.66 μs |  0.54 |    0.00 |        - |       - |   38039 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,874.5 μs** |  **2,910.39 μs** |   **159.53 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 10,643.3 μs | 16,141.45 μs |   884.77 μs |  0.83 |    0.06 |  15.6250 |       - |  400942 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.7 μs** |     **39.16 μs** |     **2.15 μs** |  **1.00** |    **0.02** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    126.7 μs |    196.22 μs |    10.76 μs |  0.89 |    0.07 |        - |       - |    4278 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,336.4 μs** |    **309.02 μs** |    **16.94 μs** |  **1.00** |    **0.02** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  2,358.3 μs | 10,320.53 μs |   565.70 μs |  1.76 |    0.37 |        - |       - |   44714 B |        0.13 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |    **667.1 μs** |  **7,991.84 μs** |   **438.06 μs** |  **1.47** |    **1.40** |   **7.3242** |       **-** |  **125545 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    859.2 μs |  2,359.55 μs |   129.33 μs |  1.89 |    1.32 |        - |       - |   10444 B |        0.08 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,901.1 μs** | **42,925.44 μs** | **2,352.89 μs** |  **1.03** |    **0.29** |  **74.2188** |       **-** | **1256298 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,610.9 μs |  8,505.72 μs |   466.23 μs |  0.72 |    0.15 |        - |       - |   93121 B |        0.07 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,442.5 μs** |     **56.58 μs** |     **3.10 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,429.2 μs |    267.27 μs |    14.65 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,450.2 μs** |    **313.28 μs** |    **17.17 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,412.2 μs |    230.73 μs |    12.65 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,441.6 μs** |    **173.49 μs** |     **9.51 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,405.1 μs |     66.36 μs |     3.64 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,442.8 μs** |     **84.30 μs** |     **4.62 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,416.2 μs |    320.88 μs |    17.59 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error        | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|-------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **150.5 μs** |    **606.90 μs** |  **33.27 μs** |   **169.5 μs** |  **1.04** |    **0.31** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   179.3 μs |    209.52 μs |  11.48 μs |   177.3 μs |  1.24 |    0.28 |   39.98 KB |        0.62 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.6 μs** |    **698.23 μs** |  **38.27 μs** |   **138.5 μs** |  **1.04** |    **0.31** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   221.5 μs |    565.59 μs |  31.00 μs |   223.8 μs |  1.51 |    0.35 |  215.77 KB |        0.90 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **936.1 μs** |  **2,992.41 μs** | **164.02 μs** |   **875.5 μs** |  **1.02** |    **0.21** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,224.7 μs |     98.87 μs |   5.42 μs | 1,224.5 μs |  1.33 |    0.19 |  476.66 KB |        0.73 |
|                      |              |             |            |              |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,457.0 μs** | **12,511.76 μs** | **685.81 μs** | **1,072.8 μs** |  **1.13** |    **0.60** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,218.6 μs | 11,566.83 μs | 634.02 μs | 2,564.9 μs |  1.72 |    0.71 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **848.6 ns** |   **628.9 ns** |  **34.47 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,081.5 ns | 2,816.7 ns | 154.39 ns |  2.46 |    0.18 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,480.8 ns** | **1,874.2 ns** | **102.73 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,154.2 ns | 3,813.1 ns | 209.01 ns |  2.14 |    0.17 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 36.127 μs | 61.440 μs | 3.3677 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 14.189 μs | 20.690 μs | 1.1341 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  7.687 μs | 25.867 μs | 1.4179 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.668 μs | 20.144 μs | 1.1041 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 14.237 μs | 17.698 μs | 0.9701 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.252 μs | 21.670 μs | 1.1878 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.272 μs | 26.018 μs | 1.4261 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 26.958 μs | 11.043 μs | 0.6053 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 11.968 μs |  9.438 μs | 0.5173 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 14.716 μs |  9.871 μs | 0.5411 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 23.802 μs | 62.281 μs | 3.4138 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 31.463 μs | 85.544 μs | 4.6890 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  4.906 μs | 27.094 μs | 1.4851 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 13.293 μs | 45.664 μs | 2.5030 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean          | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |--------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 12,658.487 ns | 43.6853 ns | 2.3945 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     13.534 ns |  1.0663 ns | 0.0584 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     16.927 ns |  2.6022 ns | 0.1426 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     34.906 ns |  3.8292 ns | 0.2099 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     36.396 ns |  5.9207 ns | 0.3245 ns |     ? |       ? | 0.0026 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |      8.643 ns |  0.2528 ns | 0.0139 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |               |            |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    116.252 ns |  4.1325 ns | 0.2265 ns |  1.00 |    0.00 | 0.0105 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     83.445 ns |  0.9990 ns | 0.0548 ns |  0.72 |    0.00 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev    | Allocated |
|------------------------ |-----------:|-----------:|----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.266 μs |  34.832 μs |  1.909 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 426.427 μs | 127.931 μs |  7.012 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.452 μs |  51.177 μs |  2.805 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 200.679 μs | 207.039 μs | 11.348 μs |      80 B |


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