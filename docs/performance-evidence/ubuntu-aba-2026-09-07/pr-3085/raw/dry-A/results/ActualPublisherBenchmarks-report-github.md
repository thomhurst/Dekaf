```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method           | Enabled | Mean        | Error | Median      | Max         | Allocated |
|----------------- |-------- |------------:|------:|------------:|------------:|----------:|
| **RelayBatch**       | **False**   | **30,229.0 μs** |    **NA** | **30,229.0 μs** | **30,229.0 μs** | **359.54 KB** |
| PublisherControl | False   |  1,247.6 μs |    NA |  1,247.6 μs |  1,247.6 μs |  359.4 KB |
| **RelayBatch**       | **True**    |    **436.7 μs** |    **NA** |    **436.7 μs** |    **436.7 μs** | **359.54 KB** |
| PublisherControl | True    |    917.7 μs |    NA |    917.7 μs |    917.7 μs |  359.4 KB |
