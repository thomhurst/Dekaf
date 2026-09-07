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
| **RelayBatch**       | **False**   | **42.39 μs** | **0.991 μs** | **0.827 μs** | **42.37 μs** | **44.03 μs** | **28.1272** | **5.0664** |  **359.4 KB** |
| PublisherControl | False   | 45.41 μs | 1.345 μs | 1.123 μs | 45.31 μs | 47.74 μs | 28.1334 | 5.0539 |  359.4 KB |
| **RelayBatch**       | **True**    | **47.14 μs** | **0.848 μs** | **0.793 μs** | **47.29 μs** | **48.47 μs** | **28.0540** | **4.9716** |  **359.4 KB** |
| PublisherControl | True    | 43.87 μs | 4.551 μs | 4.034 μs | 41.60 μs | 52.48 μs | 28.1250 | 5.0347 |  359.4 KB |
