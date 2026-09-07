```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryB  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method           | Enabled | Mean       | Error | Median     | Max        | Allocated |
|----------------- |-------- |-----------:|------:|-----------:|-----------:|----------:|
| **SynchronousBatch** | **False**   |   **584.8 μs** |    **NA** |   **584.8 μs** |   **584.8 μs** |         **-** |
| PendingBatch     | False   | 9,152.8 μs |    NA | 9,152.8 μs | 9,152.8 μs |         - |
| PublisherControl | False   | 1,032.4 μs |    NA | 1,032.4 μs | 1,032.4 μs |         - |
| **SynchronousBatch** | **True**    |   **614.0 μs** |    **NA** |   **614.0 μs** |   **614.0 μs** |         **-** |
| PendingBatch     | True    | 5,538.3 μs |    NA | 5,538.3 μs | 5,538.3 μs |     176 B |
| PublisherControl | True    |   804.8 μs |    NA |   804.8 μs |   804.8 μs |         - |
