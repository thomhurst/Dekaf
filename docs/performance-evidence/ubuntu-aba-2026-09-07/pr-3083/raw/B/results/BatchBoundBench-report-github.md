```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method             | Batches | Mean         | Error     | StdDev    | Gen0   | Allocated |
|------------------- |-------- |-------------:|----------:|----------:|-------:|----------:|
| **ConstructPollBatch** | **1**       |     **39.37 ns** |  **0.365 ns** |  **0.487 ns** | **0.0075** |     **128 B** |
| ParseFetch         | 1       |    154.01 ns |  1.670 ns |  2.229 ns |      - |         - |
| **ConstructPollBatch** | **128**     |     **38.44 ns** |  **0.286 ns** |  **0.381 ns** | **0.0076** |     **128 B** |
| ParseFetch         | 128     | 13,802.76 ns | 53.077 ns | 70.856 ns |      - |         - |
