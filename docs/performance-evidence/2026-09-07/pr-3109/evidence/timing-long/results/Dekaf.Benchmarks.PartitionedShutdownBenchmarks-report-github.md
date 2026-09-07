```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host]             : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  A_Published        : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  B_Candidate        : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  C_PublishedControl : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

OutlierMode=DontRemove  Affinity=00000000000000000100  EnvironmentVariables=DOTNET_TieredCompilation=0  
InvocationCount=1  IterationCount=300  LaunchCount=2  
UnrollFactor=1  WarmupCount=30  

```
| Method         | Job                | Arguments                                                                                                   | Mean     | Error   | StdDev   | Median   | Allocated |
|--------------- |------------------- |------------------------------------------------------------------------------------------------------------ |---------:|--------:|---------:|---------:|----------:|
| DrainFullQueue | A_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3109/published,/p:UseSharedCompilation=false | 103.1 μs | 1.86 μs | 13.74 μs | 99.80 μs |  50.51 KB |
| DrainFullQueue | B_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3109/candidate,/p:UseSharedCompilation=false | 106.8 μs | 2.93 μs | 21.72 μs | 99.30 μs |  50.39 KB |
| DrainFullQueue | C_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3109/published,/p:UseSharedCompilation=false | 107.1 μs | 4.59 μs | 33.99 μs | 99.97 μs |  50.51 KB |
