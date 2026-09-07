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
| **RelayBatch**       | **False**   | **48.56 μs** | **2.412 μs** | **2.256 μs** | **47.50 μs** | **53.10 μs** | **28.0200** | **4.8246** | **359.54 KB** |
| PublisherControl | False   | 42.88 μs | 1.118 μs | 1.046 μs | 42.95 μs | 45.17 μs | 28.0292 | 5.0487 |  359.4 KB |
| **RelayBatch**       | **True**    | **46.78 μs** | **1.115 μs** | **0.871 μs** | **47.11 μs** | **48.16 μs** | **28.1423** | **4.8343** | **359.54 KB** |
| PublisherControl | True    | 41.34 μs | 2.093 μs | 1.748 μs | 41.40 μs | 45.95 μs | 28.0774 | 4.9175 |  359.4 KB |
