---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-27 22:01 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 1.39 | 1.25–1.54 | 21% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.63 | 1.15–1.99 | 51% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 1.40 | 0.99–1.69 | 50% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.67 | 1.43–2.64 | 73% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 100 | 5 | 2.58 | 1.96–2.88 | 35% | ⚠ Low |
| ConsumerPollBenchmarks.PollSingle | MessageSize: 1000 | 5 | 2.59 | 2.02–2.75 | 28% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.02 | 0.86–1.09 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.04 | 0.84–1.22 | 37% | ⚠ Low |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 1.00 | 0.98–1.20 | 22% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.83–1.26 | 45% | ⚠ Low |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.43 | 0.42–0.43 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.52 | 0.51–0.52 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.42 | 0.41–0.45 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.06 | 0.96–1.56 | 56% | ⚠ Low |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.46–0.48 | 4% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.45–0.47 | 3% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.47 | 0.46–0.47 | 2% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error       | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|------------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,135.4 μs** |   **116.57 μs** |  **77.10 μs** |  **1.00** |    **0.02** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,657.8 μs |    12.40 μs |   8.20 μs |  0.43 |    0.01 |        - |       - |    5589 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,332.5 μs** |    **62.17 μs** |  **41.12 μs** |  **1.00** |    **0.01** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,786.4 μs |    56.83 μs |  37.59 μs |  0.52 |    0.01 |        - |       - |   51802 B |        0.05 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,460.0 μs** |    **85.98 μs** |  **51.17 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,696.9 μs |   123.66 μs |  73.59 μs |  0.42 |    0.01 |        - |       - |    7535 B |        0.04 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,331.4 μs** |   **265.60 μs** | **175.68 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 13,120.7 μs | 1,144.74 μs | 757.18 μs |  1.06 |    0.06 |        - |       - |  350636 B |        0.18 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **126.4 μs** |     **4.62 μs** |   **3.06 μs** |  **1.00** |    **0.03** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    137.6 μs |    23.86 μs |  15.78 μs |  1.09 |    0.12 |        - |       - |     210 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,283.6 μs** |    **60.84 μs** |  **40.24 μs** |  **1.00** |    **0.04** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,384.1 μs |   180.84 μs | 119.61 μs |  1.08 |    0.09 |        - |       - |    2186 B |       0.007 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,061.4 μs** |    **16.54 μs** |   **9.84 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121540 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,047.7 μs |    48.40 μs |  32.01 μs |  0.99 |    0.03 |        - |       - |    2428 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,502.0 μs** |   **166.43 μs** | **110.08 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1214861 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      | 10,073.4 μs |   285.53 μs | 169.92 μs |  0.96 |    0.02 |        - |       - |   18415 B |        0.02 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,487.4 μs** |    **31.09 μs** |  **20.56 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,583.6 μs |    29.25 μs |  17.41 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,486.7 μs** |    **21.54 μs** |  **14.25 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,586.4 μs |    12.76 μs |   8.44 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.54 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,493.5 μs** |    **17.67 μs** |  **11.69 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,587.4 μs |     9.91 μs |   5.90 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |
|                         |               |             |           |             |             |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,491.2 μs** |    **33.59 μs** |  **22.22 μs** |  **1.00** |    **0.01** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,579.4 μs |    24.33 μs |  16.09 μs |  0.47 |    0.00 |        - |       - |     648 B |        0.31 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Allocated  | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|------:|--------:|-----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **120.2 μs** |    **33.30 μs** |  **17.42 μs** |  **1.02** |    **0.20** |   **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   185.3 μs |    58.55 μs |  30.62 μs |  1.57 |    0.33 |   40.18 KB |        0.62 | ⚠ Low |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **152.4 μs** |    **70.06 μs** |  **36.64 μs** |  **1.05** |    **0.33** |  **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   222.4 μs |    70.56 μs |  36.90 μs |  1.53 |    0.40 |  215.96 KB |        0.90 | ⚠ Low |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **857.2 μs** |    **67.77 μs** |  **24.17 μs** |  **1.00** |    **0.04** |  **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         | 1,372.1 μs |   319.89 μs | 142.03 μs |  1.60 |    0.16 |  476.85 KB |        0.74 | Stable |
|                      |              |             |            |             |           |       |         |            |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,060.5 μs** |    **24.31 μs** |   **8.67 μs** |  **1.00** |    **0.01** |  **2406.4 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 1000        | 2,355.5 μs | 1,317.14 μs | 584.82 μs |  2.22 |    0.52 | 2234.66 KB |        0.93 | ⚠ Low |

| Method               | MessageSize | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------ |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **100**         |   **804.3 ns** |  **47.64 ns** |  **28.35 ns** |  **1.00** |    **0.05** |      **-** |     **648 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 100         | 2,314.3 ns | 663.89 ns | 439.12 ns |  2.88 |    0.53 |      - |     452 B |        0.70 | ⚠ Low |
|                      |             |            |           |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **1000**        | **1,457.0 ns** | **156.99 ns** |  **93.42 ns** |  **1.00** |    **0.08** | **0.1000** |    **2448 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 1000        | 3,354.9 ns | 453.68 ns | 300.08 ns |  2.31 |    0.24 | 0.1000 |    2254 B |        0.92 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev   | Gen0   | Allocated |
|--------------------------- |----------:|----------:|---------:|-------:|----------:|
| ReadDescribeGroupsV5       | 561.20 ns | 17.378 ns | 4.513 ns | 0.0725 |    1224 B |
| WriteFindCoordinatorV6     |  27.29 ns |  0.193 ns | 0.050 ns |      - |         - |
| WriteDescribeGroupsV6      |  45.09 ns |  0.203 ns | 0.031 ns |      - |         - |
| WriteListConfigResourcesV1 |  20.26 ns |  0.120 ns | 0.031 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **2.006 μs** | **0.0232 μs** | **0.0060 μs** |         **-** |
| **WriteRequest** | **1**       | **1.977 μs** | **0.0390 μs** | **0.0101 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.396 μs** | **0.1050 μs** | **0.0273 μs** |         **-** |
| **WriteRequest** | **9**       | **2.494 μs** | **0.0251 μs** | **0.0039 μs** |         **-** |
| **WriteRequest** | **10**      | **2.406 μs** | **0.0150 μs** | **0.0039 μs** |         **-** |
| **WriteRequest** | **11**      | **2.399 μs** | **0.0199 μs** | **0.0031 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **103.91 ns** | **0.173 ns** | **0.045 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  96.77 ns | 0.036 ns | 0.006 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **94.58 ns** | **0.424 ns** | **0.066 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  89.05 ns | 0.135 ns | 0.035 ns |         - |

| Method                                          | Mean       | Error    | StdDev   | Gen0   | Allocated |
|------------------------------------------------ |-----------:|---------:|---------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.7 ns |  0.96 ns |  0.50 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 2,162.8 ns |  2.96 ns |  1.76 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,332.8 ns |  1.76 ns |  1.05 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,303.3 ns |  2.82 ns |  1.48 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 1,902.5 ns |  2.46 ns |  1.29 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,996.7 ns |  2.68 ns |  1.40 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,946.1 ns |  3.30 ns |  1.72 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,841.8 ns |  2.46 ns |  1.47 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,147.1 ns |  4.72 ns |  2.81 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,817.4 ns |  5.55 ns |  3.67 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   700.6 ns |  0.44 ns |  0.23 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   825.0 ns |  2.71 ns |  1.80 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.9 ns |  0.52 ns |  0.35 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,680.9 ns | 18.23 ns | 12.06 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,352.5 ns |  0.75 ns |  0.45 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,289.23 ns | 59.666 ns | 39.465 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     15.44 ns |  0.193 ns |  0.115 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     17.68 ns |  0.104 ns |  0.069 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.23 ns |  0.280 ns |  0.185 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     30.09 ns |  0.378 ns |  0.225 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.89 ns |  0.029 ns |  0.019 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    107.93 ns |  1.693 ns |  1.120 ns |  1.00 |    0.01 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     55.99 ns |  0.322 ns |  0.213 ns |  0.52 |    0.01 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------------ |-------------:|----------:|----------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |     299.5 ns |   2.86 ns |   1.89 ns | 0.0029 |      48 B |
| &#39;Snappy Compress 1MB&#39;   |  97,388.6 ns | 189.55 ns |  99.14 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |     223.2 ns |   1.15 ns |   0.76 ns | 0.0048 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 126,093.7 ns | 206.39 ns | 136.51 ns |      - |      80 B |

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