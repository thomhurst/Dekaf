```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Affinity=00000000000000000100  Toolchain=InProcessEmitToolchain  IterationCount=15  
IterationTime=250ms  WarmupCount=5  

```
| Method           | Enabled | Mean       | Error      | StdDev    | Median     | Max        | Gen0   | Allocated |
|----------------- |-------- |-----------:|-----------:|----------:|-----------:|-----------:|-------:|----------:|
| **SynchronousBatch** | **False**   | **247.461 ns** |  **1.2809 ns** | **1.1355 ns** | **246.913 ns** | **249.285 ns** |      **-** |         **-** |
| PendingBatch     | False   | 413.964 ns |  4.3033 ns | 4.0253 ns | 415.733 ns | 419.641 ns |      - |         - |
| PublisherControl | False   |   6.922 ns |  0.0768 ns | 0.0681 ns |   6.925 ns |   7.076 ns |      - |         - |
| **SynchronousBatch** | **True**    | **371.763 ns** |  **5.0014 ns** | **4.4336 ns** | **369.820 ns** | **381.130 ns** |      **-** |         **-** |
| PendingBatch     | True    | 696.642 ns | 10.6111 ns | 9.9257 ns | 697.082 ns | 708.763 ns | 0.0109 |     176 B |
| PublisherControl | True    |   6.791 ns |  0.0492 ns | 0.0436 ns |   6.789 ns |   6.890 ns |      - |         - |
