```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method           | Enabled | Mean     | Error    | StdDev   | Median   | Max      | Gen0    | Gen1   | Allocated |
|----------------- |-------- |---------:|---------:|---------:|---------:|---------:|--------:|-------:|----------:|
| **RelayBatch**       | **False**   | **65.90 μs** | **0.632 μs** | **0.843 μs** | **65.81 μs** | **67.90 μs** | **21.8019** | **3.9401** | **359.54 KB** |
| PublisherControl | False   | 66.84 μs | 1.120 μs | 1.495 μs | 66.48 μs | 72.16 μs | 21.9665 | 3.9226 |  359.4 KB |
| **RelayBatch**       | **True**    | **67.42 μs** | **1.263 μs** | **1.686 μs** | **67.19 μs** | **72.12 μs** | **21.7760** | **3.9593** | **359.54 KB** |
| PublisherControl | True    | 66.48 μs | 0.652 μs | 0.871 μs | 66.44 μs | 68.95 μs | 21.9957 | 3.7554 |  359.4 KB |
