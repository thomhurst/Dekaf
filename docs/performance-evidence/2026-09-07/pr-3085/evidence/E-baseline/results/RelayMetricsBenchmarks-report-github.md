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
| SynchronousBatch | False   | 248.261 ns | 2.0499 ns | 1.7118 ns | 247.528 ns | 251.051 ns | 0.0109 |     144 B |
| PendingBatch     | False   | 486.803 ns | 8.6640 ns | 7.6804 ns | 484.002 ns | 502.494 ns | 0.0358 |     480 B |
| PublisherControl | False   |   7.401 ns | 0.0546 ns | 0.0484 ns |   7.390 ns |   7.483 ns |      - |         - |
