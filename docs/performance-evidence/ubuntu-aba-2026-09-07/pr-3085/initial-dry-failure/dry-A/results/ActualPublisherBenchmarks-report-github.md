```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon 6973P-C 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method           | Enabled | Mean        | Error | Median      | Max         | Allocated |
|----------------- |-------- |------------:|------:|------------:|------------:|----------:|
| **RelayBatch**       | **False**   | **21,173.5 μs** |    **NA** | **21,173.5 μs** | **21,173.5 μs** | **359.54 KB** |
| PublisherControl | False   |    988.7 μs |    NA |    988.7 μs |    988.7 μs |  359.4 KB |
| **RelayBatch**       | **True**    |    **366.1 μs** |    **NA** |    **366.1 μs** |    **366.1 μs** | **359.54 KB** |
| PublisherControl | True    |    698.7 μs |    NA |    698.7 μs |    698.7 μs |  359.4 KB |
