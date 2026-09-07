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
| RelayBatch       | False   | 43.66 μs | 1.023 μs | 0.907 μs | 43.62 μs | 45.38 μs | 28.0599 | 4.8201 | 359.54 KB |
| PublisherControl | False   | 41.72 μs | 0.979 μs | 0.916 μs | 41.67 μs | 43.23 μs | 28.0980 | 5.0432 |  359.4 KB |
