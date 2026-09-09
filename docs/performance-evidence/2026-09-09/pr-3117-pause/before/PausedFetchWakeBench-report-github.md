```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  IterationCount=10  
IterationTime=200ms  WarmupCount=3  

```
| Method            | Mean     | Error     | StdDev    | Gen0   | Allocated |
|------------------ |---------:|----------:|----------:|-------:|----------:|
| ResumePausedFetch | 2.985 μs | 0.0422 μs | 0.0279 μs | 0.0886 |   1.18 KB |
