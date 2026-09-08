```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

OutlierMode=DontRemove  Affinity=00000000000000000100  Toolchain=InProcessEmitToolchain  
InvocationCount=1024  IterationCount=1  UnrollFactor=1  
WarmupCount=1  

```
| Method        | Mean     | Error | Allocated |
|-------------- |---------:|------:|----------:|
| NullableInt32 | 28.22 ns |    NA |         - |
