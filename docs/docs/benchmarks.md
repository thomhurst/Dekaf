---
sidebar_position: 13
---

# Benchmark Results

Live benchmark comparisons between Dekaf and Confluent.Kafka, automatically updated on every commit to main.

**Last Updated:** 2026-07-29 23:53 UTC

:::info
These benchmarks run on GitHub Actions (ubuntu-latest) using BenchmarkDotNet. 
Ratio semantics differ per table — see 'How to Read These Results' below.
:::

## Rolling comparison (last 5 runs)

Each ratio pairs Dekaf and Confluent means from the same runner, then reports the median across recent comparable runs. Lower is better; `< 1.0` means Dekaf is faster.

Rows with run spread above 30% are marked low-confidence. Run spread is `(maximum ratio - minimum ratio) / median ratio`.

| Benchmark | Parameters | Runs | Median Ratio | Ratio Range | Run Spread | Confidence |
|---|---|---:|---:|---:|---:|---|
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 100 | 5 | 0.96 | 0.89–1.05 | 17% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 100, MessageSize: 1000 | 5 | 1.02 | 0.91–1.08 | 17% | Stable |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 100 | 5 | 0.69 | 0.65–0.86 | 31% | ⚠ Low |
| ConsumerBenchmarks.ConsumeAll | MessageCount: 1000, MessageSize: 1000 | 5 | 1.25 | 1.05–1.39 | 28% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 100 | 5 | 0.09 | 0.09–0.09 | 5% | Stable |
| ConsumerPollBenchmarks.PollSingle | PollsPerIteration: 400000, MessageSize: 1000 | 5 | 0.34 | 0.32–0.39 | 19% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 100 | 5 | 1.08 | 0.96–1.19 | 21% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 100, BatchSize: 1000 | 5 | 1.04 | 0.97–1.09 | 11% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 100 | 5 | 0.99 | 0.98–1.01 | 3% | Stable |
| ProducerBenchmarks.FireAndForget | MessageSize: 1000, BatchSize: 1000 | 5 | 0.96 | 0.95–0.99 | 3% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 100 | 5 | 0.44 | 0.44–0.45 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 100, BatchSize: 1000 | 5 | 0.51 | 0.51–0.52 | 2% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 100 | 5 | 0.41 | 0.40–0.43 | 8% | Stable |
| ProducerBenchmarks.ProduceBatch | MessageSize: 1000, BatchSize: 1000 | 5 | 1.05 | 1.00–1.08 | 8% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 100, BatchSize: 1000 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 100 | 5 | 0.47 | 0.47–0.48 | 1% | Stable |
| ProducerBenchmarks.ProduceSingle | MessageSize: 1000, BatchSize: 1000 | 5 | 0.48 | 0.47–0.48 | 2% | Stable |

## Latest run

Latest-run tables retain BenchmarkDotNet's within-run `RatioSD`. Rows above the confidence threshold are marked low-confidence.

### Producer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message production across different scenarios.

| Method                  | Categories    | MessageSize | BatchSize | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio | Confidence |
|------------------------ |-------------- |------------ |---------- |------------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|---|
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **100**       |  **6,067.7 μs** |  **81.07 μs** |  **53.62 μs** |  **1.00** |    **0.01** |        **-** |       **-** |  **105170 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 100       |  2,672.0 μs |  12.72 μs |   8.41 μs |  0.44 |    0.00 |        - |       - |    5504 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **100**         | **1000**      |  **7,387.6 μs** | **131.19 μs** |  **86.77 μs** |  **1.00** |    **0.02** |  **62.5000** | **31.2500** | **1048386 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 100         | 1000      |  3,783.1 μs |  36.33 μs |  21.62 μs |  0.51 |    0.01 |        - |       - |   51573 B |        0.05 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **100**       |  **6,523.3 μs** |  **50.56 μs** |  **33.44 μs** |  **1.00** |    **0.01** |   **7.8125** |       **-** |  **194772 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 100       |  2,772.8 μs |  54.29 μs |  35.91 μs |  0.43 |    0.01 |        - |       - |    6090 B |        0.03 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceBatch**  | **BatchProduce**  | **1000**        | **1000**      | **12,259.6 μs** | **396.89 μs** | **207.58 μs** |  **1.00** |    **0.02** | **109.3750** | **46.8750** | **1944396 B** |        **1.00** | Stable |
| Dekaf_ProduceBatch      | BatchProduce  | 1000        | 1000      | 12,816.5 μs | 636.94 μs | 333.13 μs |  1.05 |    0.03 |        - |       - |   68341 B |        0.04 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **100**       |    **124.6 μs** |   **2.63 μs** |   **1.74 μs** |  **1.00** |    **0.02** |   **1.7090** |       **-** |   **30400 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 100       |    134.4 μs |  22.78 μs |  15.07 μs |  1.08 |    0.12 |        - |       - |     199 B |       0.007 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **100**         | **1000**      |  **1,304.2 μs** |  **95.68 μs** |  **63.29 μs** |  **1.00** |    **0.06** |  **17.5781** |       **-** |  **304000 B** |       **1.000** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 100         | 1000      |  1,268.4 μs | 166.52 μs | 110.14 μs |  0.97 |    0.09 |        - |       - |    1983 B |       0.007 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **100**       |  **1,038.8 μs** |   **8.96 μs** |   **5.33 μs** |  **1.00** |    **0.01** |   **7.0801** |       **-** |  **121514 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 100       |  1,024.5 μs |  30.20 μs |  17.97 μs |  0.99 |    0.02 |        - |       - |    1804 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_FireAndForget** | **FireAndForget** | **1000**        | **1000**      | **10,318.3 μs** |  **69.18 μs** |  **41.17 μs** |  **1.00** |    **0.01** |  **70.3125** |       **-** | **1215166 B** |        **1.00** | Stable |
| Dekaf_FireAndForget     | FireAndForget | 1000        | 1000      |  9,867.2 μs | 490.60 μs | 291.95 μs |  0.96 |    0.03 |        - |       - |   17030 B |        0.01 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **100**       |  **5,485.6 μs** |   **7.69 μs** |   **5.09 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 100       |  2,609.4 μs |  13.70 μs |   9.06 μs |  0.48 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **100**         | **1000**      |  **5,476.7 μs** |   **9.97 μs** |   **5.93 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **1202 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 100         | 1000      |  2,595.7 μs |  15.02 μs |   8.94 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.52 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **100**       |  **5,483.0 μs** |   **6.97 μs** |   **4.61 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 100       |  2,605.0 μs |  25.72 μs |  17.01 μs |  0.48 |    0.00 |        - |       - |     624 B |        0.30 | Stable |
|                         |               |             |           |             |           |           |       |         |          |         |           |             | — |
| **Confluent_ProduceSingle** | **SingleProduce** | **1000**        | **1000**      |  **5,486.2 μs** |  **10.02 μs** |   **6.63 μs** |  **1.00** |    **0.00** |        **-** |       **-** |    **2098 B** |        **1.00** | Stable |
| Dekaf_ProduceSingle     | SingleProduce | 1000        | 1000      |  2,604.1 μs |  19.74 μs |  13.06 μs |  0.47 |    0.00 |        - |       - |     624 B |        0.30 | Stable |

### Consumer Benchmarks

Comparing Dekaf vs Confluent.Kafka for message consumption.

| Method               | MessageCount | MessageSize | Mean       | Error       | StdDev    | Median     | Ratio | RatioSD | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------- |------------ |-----------:|------------:|----------:|-----------:|------:|--------:|----------:|------------:|---|
| **Confluent_ConsumeAll** | **100**          | **100**         |   **120.5 μs** |    **50.71 μs** |  **26.52 μs** |   **108.6 μs** |  **1.04** |    **0.30** |  **64.99 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 100          | 100         |   126.7 μs |    51.10 μs |  26.72 μs |   114.7 μs |  1.09 |    0.30 |  26.45 KB |        0.41 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **100**          | **1000**        |   **160.8 μs** |    **98.66 μs** |  **51.60 μs** |   **149.6 μs** |  **1.08** |    **0.44** | **240.77 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 100          | 1000        |   160.7 μs |    27.46 μs |  12.19 μs |   155.6 μs |  1.08 |    0.29 | 202.23 KB |        0.84 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **100**         |   **852.1 μs** |    **44.53 μs** |  **15.88 μs** |   **858.1 μs** |  **1.00** |    **0.02** | **648.59 KB** |        **1.00** | Stable |
| Dekaf_ConsumeAll     | 1000         | 100         |   737.0 μs |    88.59 μs |  39.33 μs |   744.2 μs |  0.87 |    0.05 | 258.48 KB |        0.40 | Stable |
|                      |              |             |            |             |           |            |       |         |           |             | — |
| **Confluent_ConsumeAll** | **1000**         | **1000**        | **1,436.4 μs** |   **851.03 μs** | **445.11 μs** | **1,337.8 μs** |  **1.08** |    **0.43** | **2406.4 KB** |        **1.00** | ⚠ Low |
| Dekaf_ConsumeAll     | 1000         | 1000        | 1,503.8 μs | 1,337.01 μs | 593.64 μs | 1,881.0 μs |  1.13 |    0.52 | 2016.3 KB |        0.84 | ⚠ Low |

| Method               | PollsPerIteration | MessageSize | Mean       | Error       | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio | Confidence |
|--------------------- |------------------ |------------ |-----------:|------------:|----------:|------:|--------:|-------:|----------:|------------:|---|
| **Confluent_PollSingle** | **400000**            | **100**         | **5,536.7 ns** |    **17.08 ns** |   **8.93 ns** |  **1.00** |    **0.00** | **0.0375** |     **654 B** |        **1.00** | Stable |
| Dekaf_PollSingle     | 400000            | 100         |   474.6 ns |    14.37 ns |   8.55 ns |  0.09 |    0.00 | 0.0150 |     270 B |        0.41 | Stable |
|                      |                   |             |            |             |           |       |         |        |           |             | — |
| **Confluent_PollSingle** | **400000**            | **1000**        | **3,440.0 ns** | **1,514.72 ns** | **792.23 ns** |  **1.10** |    **0.53** | **0.1450** |    **2454 B** |        **1.00** | ⚠ Low |
| Dekaf_PollSingle     | 400000            | 1000        | 1,181.8 ns |   139.48 ns |  92.26 ns |  0.38 |    0.16 | 0.1225 |    2075 B |        0.85 | Stable |

## Protocol Benchmarks

Zero-allocation wire protocol serialization/deserialization.

:::tip
**Allocated = `-` means zero heap allocations** - the goal of Dekaf's design!
:::

| Method                     | Mean      | Error     | StdDev    | Gen0   | Allocated |
|--------------------------- |----------:|----------:|----------:|-------:|----------:|
| ReadDescribeGroupsV5       | 368.05 ns | 80.280 ns | 20.848 ns | 0.0143 |    1224 B |
| WriteFindCoordinatorV6     |  14.59 ns |  0.504 ns |  0.131 ns |      - |         - |
| WriteDescribeGroupsV6      |  26.60 ns |  2.670 ns |  0.693 ns |      - |         - |
| WriteListConfigResourcesV1 |  14.33 ns |  0.401 ns |  0.104 ns |      - |         - |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **0**       | **1.249 μs** | **0.0401 μs** | **0.0104 μs** |         **-** |
| **WriteRequest** | **1**       | **1.246 μs** | **0.0875 μs** | **0.0227 μs** |         **-** |

| Method       | Version | Mean     | Error     | StdDev    | Allocated |
|------------- |-------- |---------:|----------:|----------:|----------:|
| **WriteRequest** | **8**       | **2.406 μs** | **0.0992 μs** | **0.0258 μs** |         **-** |
| **WriteRequest** | **9**       | **2.400 μs** | **0.0254 μs** | **0.0066 μs** |         **-** |
| **WriteRequest** | **10**      | **2.407 μs** | **0.0231 μs** | **0.0036 μs** |         **-** |
| **WriteRequest** | **11**      | **2.405 μs** | **0.0138 μs** | **0.0036 μs** |         **-** |

| Method                   | Version | Mean      | Error    | StdDev   | Allocated |
|------------------------- |-------- |----------:|---------:|---------:|----------:|
| **WriteOffsetCommitRequest** | **9**       | **102.60 ns** | **0.376 ns** | **0.058 ns** |         **-** |
| WriteOffsetFetchRequest  | 9       |  97.81 ns | 0.499 ns | 0.077 ns |         - |
| **WriteOffsetCommitRequest** | **10**      |  **90.84 ns** | **0.199 ns** | **0.052 ns** |         **-** |
| WriteOffsetFetchRequest  | 10      |  88.96 ns | 0.570 ns | 0.088 ns |         - |

| Method                                          | Mean       | Error   | StdDev  | Gen0   | Allocated |
|------------------------------------------------ |-----------:|--------:|--------:|-------:|----------:|
| &#39;Write 1000 Int32s&#39;                             | 1,634.8 ns | 2.34 ns | 1.39 ns |      - |         - |
| &#39;Write 100 Strings (100 chars)&#39;                 | 1,933.5 ns | 1.61 ns | 0.96 ns |      - |         - |
| &#39;Write 100 Strings (300 chars)&#39;                 | 2,327.9 ns | 8.71 ns | 5.19 ns |      - |         - |
| &#39;Write 100 String spans (300 chars)&#39;            | 2,266.6 ns | 3.49 ns | 1.82 ns |      - |         - |
| &#39;Write 100 CompactStrings&#39;                      | 2,153.1 ns | 5.09 ns | 2.66 ns |      - |         - |
| &#39;Write 100 CompactStrings (300 chars)&#39;          | 3,995.5 ns | 6.83 ns | 4.07 ns |      - |         - |
| &#39;Write 100 CompactString spans (300 chars)&#39;     | 3,875.7 ns | 2.40 ns | 1.26 ns |      - |         - |
| &#39;Write 1000 VarInts&#39;                            | 2,846.4 ns | 7.46 ns | 4.44 ns |      - |         - |
| &#39;Read 1000 Int32s&#39;                              | 1,145.4 ns | 1.80 ns | 1.07 ns |      - |         - |
| &#39;Read 1000 VarInts&#39;                             | 1,816.2 ns | 7.99 ns | 4.76 ns |      - |         - |
| &#39;Write RecordBatch (10 records)&#39;                |   713.0 ns | 1.65 ns | 1.09 ns |      - |         - |
| &#39;Write RecordBatch pre-serialized (10 records)&#39; |   838.1 ns | 1.98 ns | 1.31 ns |      - |         - |
| &#39;Read RecordBatch (10 records)&#39;                 |   168.7 ns | 0.33 ns | 0.20 ns |      - |         - |
| &#39;Read Gzip RecordBatch (10 records)&#39;            | 1,719.1 ns | 7.17 ns | 4.26 ns | 0.0172 |     312 B |
| &#39;Read + Iterate RecordBatch (10 records)&#39;       | 1,312.3 ns | 1.42 ns | 0.85 ns |      - |         - |

## Serializer Benchmarks

| Method                               | Categories | Mean         | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------- |----------- |-------------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Serialize 100 Messages (key+value)&#39; | Batch      | 11,638.02 ns | 29.900 ns | 19.777 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;Serialize String (10 chars)&#39;        | Scalar     |     17.40 ns |  0.019 ns |  0.011 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (100 chars)&#39;       | Scalar     |     18.96 ns |  0.013 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
| &#39;Serialize String (1000 chars)&#39;      | Scalar     |     38.65 ns |  0.031 ns |  0.020 ns |     ? |       ? |      - |         - |           ? |
| &#39;Deserialize String&#39;                 | Scalar     |     28.69 ns |  0.079 ns |  0.047 ns |     ? |       ? | 0.0134 |     224 B |           ? |
| &#39;Serialize Int32&#39;                    | Scalar     |     11.94 ns |  0.012 ns |  0.008 ns |     ? |       ? |      - |         - |           ? |
|                                      |            |              |           |           |       |         |        |           |             |
| &#39;ArrayBufferWriter + Copy&#39;           | Writer     |    105.31 ns |  0.315 ns |  0.165 ns |  1.00 |    0.00 | 0.0535 |     896 B |        1.00 |
| &#39;ReusableBufferWriter Direct&#39;        | Writer     |     54.92 ns |  0.066 ns |  0.044 ns |  0.52 |    0.00 |      - |         - |        0.00 |

## Compression Benchmarks

| Method                  | Mean        | Error       | StdDev      | Gen0   | Allocated |
|------------------------ |------------:|------------:|------------:|-------:|----------:|
| &#39;Snappy Compress 1KB&#39;   |    195.1 ns |     2.86 ns |     1.89 ns | 0.0005 |      48 B |
| &#39;Snappy Compress 1MB&#39;   | 52,198.4 ns | 1,887.12 ns | 1,248.21 ns |      - |      48 B |
| &#39;Snappy Decompress 1KB&#39; |    136.0 ns |     4.14 ns |     2.74 ns | 0.0010 |      80 B |
| &#39;Snappy Decompress 1MB&#39; | 79,337.4 ns | 2,371.63 ns | 1,568.68 ns |      - |      80 B |

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