```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method             | Batches | Mean         | Error     | StdDev    | Median       | Gen0   | Allocated |
|------------------- |-------- |-------------:|----------:|----------:|-------------:|-------:|----------:|
| **ConstructPollBatch** | **1**       |     **39.04 ns** |  **0.246 ns** |  **0.329 ns** |     **38.97 ns** | **0.0075** |     **128 B** |
| ParseFetch         | 1       |    147.44 ns |  0.479 ns |  0.639 ns |    147.02 ns |      - |         - |
| **ConstructPollBatch** | **128**     |     **39.95 ns** |  **0.248 ns** |  **0.331 ns** |     **39.96 ns** | **0.0075** |     **128 B** |
| ParseFetch         | 128     | 13,294.20 ns | 31.012 ns | 41.400 ns | 13,297.13 ns |      - |         - |
