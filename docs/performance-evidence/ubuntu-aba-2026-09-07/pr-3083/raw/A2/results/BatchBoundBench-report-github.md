```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method             | Batches | Mean         | Error        | StdDev       | Median       | Gen0   | Allocated |
|------------------- |-------- |-------------:|-------------:|-------------:|-------------:|-------:|----------:|
| **ConstructPollBatch** | **1**       |     **38.43 ns** |     **0.275 ns** |     **0.367 ns** |     **38.34 ns** | **0.0075** |     **128 B** |
| ParseFetch         | 1       |    163.10 ns |    10.418 ns |    13.907 ns |    174.53 ns |      - |         - |
| **ConstructPollBatch** | **128**     |     **38.72 ns** |     **0.356 ns** |     **0.476 ns** |     **38.67 ns** | **0.0076** |     **128 B** |
| ParseFetch         | 128     | 15,022.25 ns | 1,455.224 ns | 1,942.683 ns | 13,213.12 ns |      - |         - |
