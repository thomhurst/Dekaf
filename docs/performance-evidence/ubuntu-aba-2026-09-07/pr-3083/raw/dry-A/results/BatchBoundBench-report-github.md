```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method             | Batches | Mean     | Error | Allocated |
|------------------- |-------- |---------:|------:|----------:|
| **ConstructPollBatch** | **1**       | **226.7 μs** |    **NA** |     **128 B** |
| ParseFetch         | 1       | 192.5 μs |    NA |         - |
| **ConstructPollBatch** | **128**     | **204.1 μs** |    **NA** |     **128 B** |
| ParseFetch         | 128     | 195.2 μs |    NA |         - |
