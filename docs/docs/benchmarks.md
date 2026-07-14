---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-14 18:41 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error        | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------ |-------------- |------------ |---------- |------------:|-------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,138.6 μs** |    **422.29 μs** |    **23.15 μs** |  **1.00** |    **0.00** |        **-** |       **-** |  **109090 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  3,364.4 μs |    324.41 μs |    17.78 μs |  0.55 |    0.00 |        - |       - |   35194 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,287.4 μs** |    **839.75 μs** |    **46.03 μs** |  **1.00** |    **0.01** |  **62.5000** | **46.8750** | **1088566 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,696.4 μs |  2,457.90 μs |   134.73 μs |  0.51 |    0.02 |  15.6250 |       - |  347416 B |        0.32 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,530.5 μs** |    **963.62 μs** |    **52.82 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **198692 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  3,484.3 μs |  1,213.70 μs |    66.53 μs |  0.53 |    0.01 |        - |       - |   37829 B |        0.19 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,225.5 μs** |  **3,318.97 μs** |   **181.92 μs** |  **1.00** |    **0.02** | **109.3750** | **62.5000** | **1984316 B** |        **1.00** |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      |  9,598.0 μs |  3,159.95 μs |   173.21 μs |  0.79 |    0.02 |  15.6250 |       - |  400596 B |        0.20 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **153.2 μs** |    **142.37 μs** |     **7.80 μs** |  **1.00** |    **0.06** |   **1.9531** |       **-** |   **34320 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    112.9 μs |     56.94 μs |     3.12 μs |  0.74 |    0.04 |        - |       - |    4172 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,252.5 μs** |  **4,610.06 μs** |   **252.69 μs** |  **1.03** |    **0.25** |  **19.5313** |       **-** |  **343920 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,065.3 μs |  1,430.77 μs |    78.43 μs |  0.87 |    0.15 |        - |       - |   42574 B |        0.12 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,040.3 μs** |     **27.02 μs** |     **1.48 μs** |  **1.00** |    **0.00** |   **7.3242** |       **-** |  **125422 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |    817.2 μs |    552.38 μs |    30.28 μs |  0.79 |    0.03 |        - |       - |    7929 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      |  **9,480.9 μs** | **30,197.88 μs** | **1,655.25 μs** |  **1.02** |    **0.23** |  **74.2188** |       **-** | **1254956 B** |        **1.00** |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  7,329.7 μs |  6,543.43 μs |   358.67 μs |  0.79 |    0.14 |        - |       - |   79389 B |        0.06 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,407.1 μs** |    **106.78 μs** |     **5.85 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  3,532.3 μs |  4,277.52 μs |   234.47 μs |  0.65 |    0.04 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,412.7 μs** |     **33.61 μs** |     **1.84 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  3,405.8 μs |    299.94 μs |    16.44 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.67 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,410.6 μs** |     **83.16 μs** |     **4.56 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  3,426.6 μs |    447.18 μs |    24.51 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |
|                         |               |             |           |             |              |             |       |         |          |         |           |             |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,401.6 μs** |      **9.18 μs** |     **0.50 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  3,425.9 μs |    290.31 μs |    15.91 μs |  0.63 |    0.00 |        - |       - |     800 B |        0.38 |


## Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated  | Alloc Ratio |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|-----------:|------------:|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **125.5 μs** |    **568.1 μs** |  **31.14 μs** |   **123.7 μs** |  **1.04** |    **0.32** |   **64.99 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 100         |   178.0 μs |    352.6 μs |  19.33 μs |   186.1 μs |  1.48 |    0.35 |   39.98 KB |        0.62 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **178.6 μs** |    **431.3 μs** |  **23.64 μs** |   **191.5 μs** |  **1.01** |    **0.17** |  **240.77 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 100          | 1000        |   212.6 μs |    875.9 μs |  48.01 μs |   195.9 μs |  1.21 |    0.28 |  215.77 KB |        0.90 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **932.8 μs** |  **3,124.1 μs** | **171.24 μs** |   **838.0 μs** |  **1.02** |    **0.22** |  **648.59 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,357.0 μs |  5,296.0 μs | 290.29 μs | 1,210.4 μs |  1.48 |    0.35 |  476.66 KB |        0.73 |
|                      |              |             |            |             |           |            |       |         |            |             |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,458.9 μs** | **12,811.6 μs** | **702.25 μs** | **1,055.3 μs** |  **1.14** |    **0.62** |  **2406.4 KB** |        **1.00** |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,398.6 μs |    491.7 μs |  26.95 μs | 1,388.1 μs |  1.09 |    0.36 | 2234.47 KB |        0.93 |


| Method               | MessageSize | Mean       | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------------- |------------ |-----------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| **Confluent_PollSingle** | **100**         |   **865.7 ns** |   **368.0 ns** |  **20.17 ns** |  **1.00** |    **0.03** |      **-** |     **648 B** |        **1.00** |
| Dekaf_PollSingle     | 100         | 2,139.4 ns | 2,040.6 ns | 111.85 ns |  2.47 |    0.12 |      - |     452 B |        0.70 |
|                      |             |            |            |           |       |         |        |           |             |
| **Confluent_PollSingle** | **1000**        | **1,380.8 ns** | **1,608.6 ns** |  **88.17 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** |
| Dekaf_PollSingle     | 1000        | 3,253.2 ns | 1,915.8 ns | 105.01 ns |  2.36 |    0.15 | 0.1000 |    2255 B |        0.92 |


## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                                          | Mean      | Error      | StdDev     | Median    | Allocated |
|------------------------------------------------ |----------:|-----------:|-----------:|----------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 35.540 μs | 267.001 μs | 14.6353 μs | 27.221 μs |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 11.017 μs |   3.700 μs |  0.2028 μs | 10.937 μs |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 |  8.419 μs |   6.084 μs |  0.3335 μs |  8.313 μs |         - |
| &#39;Write 100 String spans (300 chars)&#39;            |  7.965 μs |   3.685 μs |  0.2020 μs |  7.933 μs |         - |
| &#39;Write 100 CompactStrings&#39;                      | 11.124 μs |   2.107 μs |  0.1155 μs | 11.057 μs |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 12.869 μs |  14.874 μs |  0.8153 μs | 12.619 μs |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 12.268 μs |   9.502 μs |  0.5208 μs | 12.068 μs |         - |
| &#39;Write 1000 VarInts&#39;                            | 32.800 μs |   9.371 μs |  0.5136 μs | 32.740 μs |         - |
| &#39;Read 1000 Int32s&#39;                              | 10.139 μs |   2.075 μs |  0.1137 μs | 10.106 μs |         - |
| &#39;Read 1000 VarInts&#39;                             | 20.575 μs |  10.116 μs |  0.5545 μs | 20.761 μs |         - |
| &#39;Write RecordBatch (10 records)&#39;                | 20.310 μs |  14.530 μs |  0.7964 μs | 20.340 μs |    2480 B |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; | 21.463 μs |  31.434 μs |  1.7230 μs | 21.192 μs |    2520 B |
| &#39;Read RecordBatch (10 records)&#39;                 |  5.519 μs |   7.524 μs |  0.4124 μs |  5.348 μs |     192 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 11.240 μs |  10.864 μs |  0.5955 μs | 11.527 μs |     192 B |


## Serializer Benchmarks

| Method                               | Categories | Mean         | Error      | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|-----------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 13,121.65 ns | 166.604 ns | 9.132 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     16.76 ns |   0.129 ns | 0.007 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     21.80 ns |   0.358 ns | 0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.79 ns |   1.073 ns | 0.059 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.92 ns |   6.875 ns | 0.377 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     12.71 ns |   1.959 ns | 0.107 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |            |          |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    108.28 ns |  23.903 ns | 1.310 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;PooledBufferWriter Direct&#39;          | Writer     |     79.85 ns |   0.259 ns | 0.014 ns |  0.74 |    0.01 |      - |         - |        0.00 |


## Compression Benchmarks

| Method                  | Mean       | Error      | StdDev     | Allocated |
|------------------------ |-----------:|-----------:|-----------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |  12.997 μs |  16.582 μs |  0.9089 μs |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 519.719 μs | 176.281 μs |  9.6626 μs |      48 B |
| &#39;Snappy Decompress 1KB&#39; |   8.969 μs |   3.325 μs |  0.1822 μs |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 226.167 μs | 235.521 μs | 12.9097 μs |      80 B |


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