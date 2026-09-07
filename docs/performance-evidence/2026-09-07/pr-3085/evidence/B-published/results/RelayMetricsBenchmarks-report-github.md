```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Affinity=00000000000000000100  Toolchain=InProcessEmitToolchain  IterationCount=15  
IterationTime=250ms  WarmupCount=5  

```
| Method           | Enabled | Mean       | Error      | StdDev     | Median     | Max        | Gen0   | Allocated |
|----------------- |-------- |-----------:|-----------:|-----------:|-----------:|-----------:|-------:|----------:|
| **SynchronousBatch** | **False**   | **259.604 ns** |  **2.1812 ns** |  **1.7030 ns** | **259.060 ns** | **263.430 ns** | **0.0104** |     **144 B** |
| PendingBatch     | False   | 485.983 ns |  3.9210 ns |  3.2742 ns | 484.739 ns | 494.103 ns | 0.0349 |     480 B |
| PublisherControl | False   |   7.394 ns |  0.0443 ns |  0.0393 ns |   7.386 ns |   7.456 ns |      - |         - |
| **SynchronousBatch** | **True**    | **381.499 ns** |  **3.1559 ns** |  **2.6353 ns** | **380.435 ns** | **387.995 ns** | **0.0108** |     **144 B** |
| PendingBatch     | True    | 774.016 ns | 17.0872 ns | 15.9834 ns | 767.167 ns | 809.637 ns | 0.0578 |     784 B |
| PublisherControl | True    |   6.759 ns |  0.0644 ns |  0.0571 ns |   6.767 ns |   6.853 ns |      - |         - |
