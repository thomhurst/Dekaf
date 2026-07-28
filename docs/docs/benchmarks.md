---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-28 21:19 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.22 | 0.89–1.28 | 32% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.24 | 0.90–1.74 | 68% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.23 | 0.66–1.36 | 56% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.53 | 1.13–2.22 | 72% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 3 | 0.09 | 0.09–0.10 | 12% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 3 | 0.33 | 0.33–0.34 | 4% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.07 | 0.97–1.14 | 16% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.09 | 0.90–1.14 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.97–1.05 | 7% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.91–1.01 | 10% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.44 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.51 | 1% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.40–0.44 | 11% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.03 | 0.98–1.11 | 12% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.47 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.47–0.47 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.47–0.47 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|------------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,066.9 μs** |    **69.04 μs** |    **45.67 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,673.1 μs |    19.16 μs |    11.40 μs |  0.44 |    0.00 |        - |       - |    5576 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,405.3 μs** |    **61.55 μs** |    **40.71 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,782.4 μs |    38.85 μs |    20.32 μs |  0.51 |    0.00 |        - |       - |   51804 B |        0.05 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,517.2 μs** |    **81.31 μs** |    **48.38 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,658.5 μs |    50.56 μs |    33.44 μs |  0.41 |    0.01 |        - |       - |    6283 B |        0.03 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,366.1 μs** |   **164.80 μs** |   **109.01 μs** |  **1.00** |    **0.01** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,138.8 μs | 1,609.30 μs | 1,064.45 μs |  0.98 |    0.08 |        - |       - |   75593 B |        0.04 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **141.1 μs** |    **22.91 μs** |    **15.16 μs** |  **1.01** |    **0.16** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    136.3 μs |    17.32 μs |    10.31 μs |  0.98 |    0.13 |        - |       - |     217 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,296.6 μs** |    **65.84 μs** |    **43.55 μs** |  **1.00** |    **0.04** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,481.0 μs |    44.45 μs |    29.40 μs |  1.14 |    0.04 |        - |       - |    2192 B |       0.007 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,050.6 μs** |     **8.64 μs** |     **5.14 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121544 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,041.3 μs |    65.57 μs |    43.37 μs |  0.99 |    0.04 |        - |       - |    1955 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,486.8 μs** |    **91.36 μs** |    **60.43 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215204 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,016.9 μs |   585.75 μs |   306.36 μs |  0.96 |    0.03 |        - |       - |   18662 B |        0.02 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,487.9 μs** |     **7.68 μs** |     **4.57 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,603.8 μs |    13.47 μs |     7.04 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,493.0 μs** |     **8.11 μs** |     **5.36 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,591.7 μs |     9.19 μs |     6.08 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,499.9 μs** |     **4.85 μs** |     **3.21 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,594.1 μs |     9.13 μs |     6.04 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |             |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,497.5 μs** |     **6.93 μs** |     **4.58 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,601.1 μs |     6.71 μs |     3.99 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **131.3 μs** |    **52.43 μs** |  **27.42 μs** |   **136.1 μs** |  **1.04** |    **0.29** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   117.1 μs |    37.25 μs |  16.54 μs |   125.2 μs |  0.93 |    0.21 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **154.8 μs** |    **75.61 μs** |  **39.55 μs** |   **138.7 μs** |  **1.05** |    **0.34** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   157.4 μs |    30.91 μs |  16.16 μs |   156.2 μs |  1.07 |    0.25 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         | **1,037.9 μs** |   **508.74 μs** | **266.08 μs** |   **885.2 μs** |  **1.05** |    **0.34** | **648.59 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 100         |   749.7 μs |    88.99 μs |  39.51 μs |   731.9 μs |  0.76 |    0.16 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,393.2 μs** |   **914.21 μs** | **478.15 μs** | **1,095.5 μs** |  **1.09** |    **0.46** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,573.6 μs | 1,268.82 μs | 563.36 μs | 1,882.0 μs |  1.23 |    0.53 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,540.4 ns** |    **19.78 ns** |  **10.35 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   477.0 ns |    21.02 ns |  12.51 ns |  0.09 |    0.00 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,501.3 ns** | **1,068.67 ns** | **706.86 ns** |  **1.08** |    **0.48** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,180.8 ns |   103.74 ns |  61.73 ns |  0.36 |    0.14 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 560.42 ns | 37.157 ns | 9.650 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.28 ns |  0.280 ns | 0.043 ns |      - |         - |
| WriteDescribeGroupsV6      |  44.96 ns |  0.146 ns | 0.023 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.24 ns |  0.182 ns | 0.047 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.003 μs** | **0.0203 μs** | **0.0053 μs** |         **-** |
| **WriteRequest** | **1**       | **2.009 μs** | **0.0089 μs** | **0.0014 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.447 μs** | **0.0182 μs** | **0.0028 μs** |         **-** |
| **WriteRequest** | **9**       | **2.566 μs** | **0.0094 μs** | **0.0014 μs** |         **-** |
| **WriteRequest** | **10**      | **2.472 μs** | **0.0089 μs** | **0.0023 μs** |         **-** |
| **WriteRequest** | **11**      | **2.480 μs** | **0.0129 μs** | **0.0020 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **109.90 ns** | **0.343 ns** | **0.089 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       | 104.24 ns | 0.155 ns | 0.040 ns |         - |
| **WriteOffsetCommitRequest** | **10**      | **105.54 ns** | **0.674 ns** | **0.104 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  93.69 ns | 0.815 ns | 0.212 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,738.7 ns | 5.27 ns | 3.49 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,074.6 ns | 2.32 ns | 1.22 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,451.7 ns | 2.43 ns | 1.27 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,400.9 ns | 2.08 ns | 1.24 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,001.0 ns | 1.11 ns | 0.66 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,953.9 ns | 8.35 ns | 4.97 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,957.3 ns | 2.84 ns | 1.48 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,932.9 ns | 9.24 ns | 6.11 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,192.8 ns | 1.95 ns | 1.16 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 2,044.7 ns | 9.97 ns | 5.93 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   724.6 ns | 2.58 ns | 1.35 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   784.9 ns | 2.80 ns | 1.85 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   161.7 ns | 0.15 ns | 0.10 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,719.0 ns | 3.45 ns | 2.28 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,203.2 ns | 1.86 ns | 1.11 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,027.62 ns | 28.677 ns | 18.968 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.58 ns |  0.029 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.72 ns |  0.013 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.07 ns |  0.116 ns |  0.069 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     38.46 ns |  1.344 ns |  0.800 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.78 ns |  0.014 ns |  0.009 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    127.44 ns |  8.750 ns |  5.788 ns |  1.00 |    0.06 | 0.0534 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.02 ns |  0.093 ns |  0.062 ns |  0.43 |    0.02 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     287.0 ns |   1.19 ns |   0.79 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,308.8 ns | 197.46 ns | 117.50 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     221.1 ns |   1.25 ns |   0.83 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 123,097.3 ns | 263.40 ns | 156.74 ns |      - |      80 B |

---

## How to Read These Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of all measurements
- **Ratio**: Performance relative to that table's baseline row
  - Producer/Consumer tables: baseline is Confluent.Kafka, so `< 1.0` = Dekaf is faster, `> 1.0` = Confluent is faster
  - Unit tables (Protocol/Serializer/Compression): baseline is an internal reference implementation, not Confluent
- **RatioSD**: BenchmarkDotNet's uncertainty for the latest run's ratio
- **Confidence**: `⚠ Low` when latest `RatioSD > 0.30` or rolling run spread exceeds 30%
- **Allocated**: Heap memory allocated per operation
  - `-` = Zero allocations (ideal!)

*Benchmarks are automatically run on every push to main.*