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
| SynchronousBatch | False   | 250.476 ns | 1.1155 ns | 0.9889 ns | 250.344 ns | 252.892 ns | 0.0100 |     144 B |
| PendingBatch     | False   | 493.303 ns | 8.4169 ns | 7.8732 ns | 490.300 ns | 506.557 ns | 0.0367 |     480 B |
| PublisherControl | False   |   6.809 ns | 0.0858 ns | 0.0802 ns |   6.771 ns |   6.946 ns |      - |         - |
