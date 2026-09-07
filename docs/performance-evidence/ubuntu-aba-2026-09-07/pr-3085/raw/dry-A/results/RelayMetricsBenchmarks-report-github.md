```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method           | Enabled | Mean       | Error | Median     | Max        | Allocated |
|----------------- |-------- |-----------:|------:|-----------:|-----------:|----------:|
| **SynchronousBatch** | **False**   |   **548.1 μs** |    **NA** |   **548.1 μs** |   **548.1 μs** |     **144 B** |
| PendingBatch     | False   | 9,138.7 μs |    NA | 9,138.7 μs | 9,138.7 μs |     480 B |
| PublisherControl | False   | 1,132.9 μs |    NA | 1,132.9 μs | 1,132.9 μs |         - |
| **SynchronousBatch** | **True**    |   **317.3 μs** |    **NA** |   **317.3 μs** |   **317.3 μs** |     **144 B** |
| PendingBatch     | True    |   293.0 μs |    NA |   293.0 μs |   293.0 μs |     480 B |
| PublisherControl | True    |   853.3 μs |    NA |   853.3 μs |   853.3 μs |         - |
