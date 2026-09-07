```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Affinity=00000000000000000100  Toolchain=InProcessEmitToolchain  IterationCount=15  
IterationTime=250ms  WarmupCount=5  

```
| Method           | Enabled | Mean       | Error     | StdDev    | Median     | Max        | Gen0   | Allocated |
|----------------- |-------- |-----------:|----------:|----------:|-----------:|-----------:|-------:|----------:|
| **SynchronousBatch** | **False**   | **260.177 ns** | **4.5038 ns** | **3.9925 ns** | **259.668 ns** | **268.532 ns** | **0.0102** |     **144 B** |
| PendingBatch     | False   | 489.525 ns | 5.3220 ns | 4.9782 ns | 490.665 ns | 496.482 ns | 0.0366 |     480 B |
| PublisherControl | False   |   6.722 ns | 0.0615 ns | 0.0513 ns |   6.724 ns |   6.813 ns |      - |         - |
| **SynchronousBatch** | **True**    | **385.878 ns** | **6.4661 ns** | **5.7320 ns** | **383.451 ns** | **398.822 ns** | **0.0107** |     **144 B** |
| PendingBatch     | True    | 778.516 ns | 8.0954 ns | 6.7600 ns | 777.779 ns | 789.842 ns | 0.0587 |     784 B |
| PublisherControl | True    |   6.763 ns | 0.0495 ns | 0.0414 ns |   6.758 ns |   6.867 ns |      - |         - |
