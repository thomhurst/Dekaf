```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method           | Enabled | Mean     | Error    | StdDev   | Median   | Max      | Gen0    | Gen1   | Allocated |
|----------------- |-------- |---------:|---------:|---------:|---------:|---------:|--------:|-------:|----------:|
| **RelayBatch**       | **False**   | **67.98 μs** | **0.692 μs** | **0.923 μs** | **67.83 μs** | **69.83 μs** | **21.8801** | **3.7817** |  **359.4 KB** |
| PublisherControl | False   | 65.02 μs | 1.015 μs | 1.355 μs | 64.69 μs | 69.60 μs | 21.7842 | 3.8900 |  359.4 KB |
| **RelayBatch**       | **True**    | **66.32 μs** | **0.478 μs** | **0.638 μs** | **66.23 μs** | **67.87 μs** | **21.9957** | **3.7554** |  **359.4 KB** |
| PublisherControl | True    | 65.04 μs | 0.485 μs | 0.647 μs | 65.06 μs | 66.42 μs | 21.8621 | 3.8580 |  359.4 KB |
