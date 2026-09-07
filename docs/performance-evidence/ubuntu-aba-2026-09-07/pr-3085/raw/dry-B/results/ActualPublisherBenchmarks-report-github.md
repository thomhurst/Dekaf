```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryB  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method           | Enabled | Mean        | Error | Median      | Max         | Allocated |
|----------------- |-------- |------------:|------:|------------:|------------:|----------:|
| **RelayBatch**       | **False**   | **32,366.5 μs** |    **NA** | **32,366.5 μs** | **32,366.5 μs** |  **359.4 KB** |
| PublisherControl | False   |  1,190.9 μs |    NA |  1,190.9 μs |  1,190.9 μs |  359.4 KB |
| **RelayBatch**       | **True**    |  **4,796.5 μs** |    **NA** |  **4,796.5 μs** |  **4,796.5 μs** |  **359.4 KB** |
| PublisherControl | True    |    886.8 μs |    NA |    886.8 μs |    886.8 μs |  359.4 KB |
