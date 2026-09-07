```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method           | Enabled | Mean     | Error    | StdDev   | Median   | Max      | Gen0    | Gen1   | Allocated |
|----------------- |-------- |---------:|---------:|---------:|---------:|---------:|--------:|-------:|----------:|
| **RelayBatch**       | **False**   | **68.17 μs** | **1.786 μs** | **2.384 μs** | **68.22 μs** | **73.31 μs** | **21.8060** | **3.9139** | **359.54 KB** |
| PublisherControl | False   | 65.17 μs | 1.131 μs | 1.510 μs | 64.48 μs | 69.45 μs | 21.8085 | 3.7234 |  359.4 KB |
| **RelayBatch**       | **True**    | **70.05 μs** | **1.055 μs** | **1.409 μs** | **69.94 μs** | **75.35 μs** | **21.9298** | **3.8377** | **359.54 KB** |
| PublisherControl | True    | 65.00 μs | 0.791 μs | 1.055 μs | 64.92 μs | 67.49 μs | 21.9809 | 3.7076 |  359.4 KB |
