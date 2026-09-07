```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon 6973P-C 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method           | Enabled | Mean       | Error | Median     | Max        | Allocated |
|----------------- |-------- |-----------:|------:|-----------:|-----------:|----------:|
| **SynchronousBatch** | **False**   |   **434.5 μs** |    **NA** |   **434.5 μs** |   **434.5 μs** |     **144 B** |
| PendingBatch     | False   | 6,408.4 μs |    NA | 6,408.4 μs | 6,408.4 μs |     480 B |
| PublisherControl | False   |   828.4 μs |    NA |   828.4 μs |   828.4 μs |         - |
| **SynchronousBatch** | **True**    |   **275.7 μs** |    **NA** |   **275.7 μs** |   **275.7 μs** |     **144 B** |
| PendingBatch     | True    |   241.8 μs |    NA |   241.8 μs |   241.8 μs |     480 B |
| PublisherControl | True    |   652.3 μs |    NA |   652.3 μs |   652.3 μs |         - |
