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
| RelayBatch       | False   | 45.49 μs | 1.652 μs | 1.379 μs | 45.27 μs | 48.24 μs | 28.1666 | 4.8384 | 359.54 KB |
| PublisherControl | False   | 43.65 μs | 1.236 μs | 1.095 μs | 43.55 μs | 45.47 μs | 28.0995 | 4.9387 |  359.4 KB |
