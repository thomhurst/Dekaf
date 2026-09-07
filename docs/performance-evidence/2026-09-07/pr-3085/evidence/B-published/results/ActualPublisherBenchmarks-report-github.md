```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Affinity=00000000000000000100  Toolchain=InProcessEmitToolchain  IterationCount=15  
IterationTime=250ms  WarmupCount=5  

```
| Method           | Enabled | Mean     | Error    | StdDev   | Median   | Max      | Gen0    | Gen1   | Allocated |
|----------------- |-------- |---------:|---------:|---------:|---------:|---------:|--------:|-------:|----------:|
| **RelayBatch**       | **False**   | **42.75 μs** | **1.641 μs** | **1.282 μs** | **42.67 μs** | **44.67 μs** | **28.1476** | **4.8879** | **359.54 KB** |
| PublisherControl | False   | 42.20 μs | 1.658 μs | 1.384 μs | 41.72 μs | 45.62 μs | 28.0449 | 4.9679 |  359.4 KB |
| **RelayBatch**       | **True**    | **45.10 μs** | **1.967 μs** | **1.743 μs** | **44.93 μs** | **48.85 μs** | **28.0405** | **4.8986** | **359.54 KB** |
| PublisherControl | True    | 44.65 μs | 2.637 μs | 2.337 μs | 44.61 μs | 50.02 μs | 28.0749 | 5.0134 |  359.4 KB |
