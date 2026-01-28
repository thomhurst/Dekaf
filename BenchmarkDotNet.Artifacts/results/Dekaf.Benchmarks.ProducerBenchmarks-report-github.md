```

BenchmarkDotNet v0.14.0, Ubuntu 25.10 (Questing Quokka)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-XAIQOV : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-FNUAAK : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

IterationCount=10  RunStrategy=Throughput  WarmupCount=3  

```
| Method                               | Job        | InvocationCount | UnrollFactor | MessageSize | BatchSize | Mean | Error |
|------------------------------------- |----------- |---------------- |------------- |------------ |---------- |-----:|------:|
| **&#39;Dekaf: Single Message Produce&#39;**      | **Job-XAIQOV** | **1**               | **1**            | **100**         | **100**       |   **NA** |    **NA** |
| &#39;Confluent: Single Message Produce&#39;  | Job-XAIQOV | 1               | 1            | 100         | 100       |   NA |    NA |
| &#39;Dekaf: Batch Produce&#39;               | Job-XAIQOV | 1               | 1            | 100         | 100       |   NA |    NA |
| &#39;Confluent: Batch Produce&#39;           | Job-XAIQOV | 1               | 1            | 100         | 100       |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Send&#39;        | Job-FNUAAK | Default         | 16           | 100         | 100       |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Direct&#39;      | Job-FNUAAK | Default         | 16           | 100         | 100       |   NA |    NA |
| &#39;Confluent: Fire-and-Forget Produce&#39; | Job-FNUAAK | Default         | 16           | 100         | 100       |   NA |    NA |
| **&#39;Dekaf: Single Message Produce&#39;**      | **Job-XAIQOV** | **1**               | **1**            | **100**         | **1000**      |   **NA** |    **NA** |
| &#39;Confluent: Single Message Produce&#39;  | Job-XAIQOV | 1               | 1            | 100         | 1000      |   NA |    NA |
| &#39;Dekaf: Batch Produce&#39;               | Job-XAIQOV | 1               | 1            | 100         | 1000      |   NA |    NA |
| &#39;Confluent: Batch Produce&#39;           | Job-XAIQOV | 1               | 1            | 100         | 1000      |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Send&#39;        | Job-FNUAAK | Default         | 16           | 100         | 1000      |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Direct&#39;      | Job-FNUAAK | Default         | 16           | 100         | 1000      |   NA |    NA |
| &#39;Confluent: Fire-and-Forget Produce&#39; | Job-FNUAAK | Default         | 16           | 100         | 1000      |   NA |    NA |
| **&#39;Dekaf: Single Message Produce&#39;**      | **Job-XAIQOV** | **1**               | **1**            | **1000**        | **100**       |   **NA** |    **NA** |
| &#39;Confluent: Single Message Produce&#39;  | Job-XAIQOV | 1               | 1            | 1000        | 100       |   NA |    NA |
| &#39;Dekaf: Batch Produce&#39;               | Job-XAIQOV | 1               | 1            | 1000        | 100       |   NA |    NA |
| &#39;Confluent: Batch Produce&#39;           | Job-XAIQOV | 1               | 1            | 1000        | 100       |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Send&#39;        | Job-FNUAAK | Default         | 16           | 1000        | 100       |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Direct&#39;      | Job-FNUAAK | Default         | 16           | 1000        | 100       |   NA |    NA |
| &#39;Confluent: Fire-and-Forget Produce&#39; | Job-FNUAAK | Default         | 16           | 1000        | 100       |   NA |    NA |
| **&#39;Dekaf: Single Message Produce&#39;**      | **Job-XAIQOV** | **1**               | **1**            | **1000**        | **1000**      |   **NA** |    **NA** |
| &#39;Confluent: Single Message Produce&#39;  | Job-XAIQOV | 1               | 1            | 1000        | 1000      |   NA |    NA |
| &#39;Dekaf: Batch Produce&#39;               | Job-XAIQOV | 1               | 1            | 1000        | 1000      |   NA |    NA |
| &#39;Confluent: Batch Produce&#39;           | Job-XAIQOV | 1               | 1            | 1000        | 1000      |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Send&#39;        | Job-FNUAAK | Default         | 16           | 1000        | 1000      |   NA |    NA |
| &#39;Dekaf: Fire-and-Forget Direct&#39;      | Job-FNUAAK | Default         | 16           | 1000        | 1000      |   NA |    NA |
| &#39;Confluent: Fire-and-Forget Produce&#39; | Job-FNUAAK | Default         | 16           | 1000        | 1000      |   NA |    NA |

Benchmarks with issues:
  ProducerBenchmarks.'Dekaf: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.'Confluent: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.'Confluent: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Send': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Direct': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.'Confluent: Fire-and-Forget Produce': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.'Confluent: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.'Dekaf: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.'Confluent: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Send': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Direct': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.'Confluent: Fire-and-Forget Produce': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=100, BatchSize=1000]
  ProducerBenchmarks.'Dekaf: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.'Confluent: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.'Confluent: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Send': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Direct': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.'Confluent: Fire-and-Forget Produce': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=100]
  ProducerBenchmarks.'Dekaf: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.'Confluent: Single Message Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.'Dekaf: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.'Confluent: Batch Produce': Job-XAIQOV(InvocationCount=1, IterationCount=10, RunStrategy=Throughput, UnrollFactor=1, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Send': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.'Dekaf: Fire-and-Forget Direct': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
  ProducerBenchmarks.'Confluent: Fire-and-Forget Produce': Job-FNUAAK(IterationCount=10, RunStrategy=Throughput, WarmupCount=3) [MessageSize=1000, BatchSize=1000]
