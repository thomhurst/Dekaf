```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
12th Gen Intel Core i7-12700K 3.60GHz, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.400
  [Host]             : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  A_Baseline         : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  B_Published        : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  C_Candidate        : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  D_PublishedControl : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
  E_BaselineControl  : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Affinity=00000000000000000100  EnvironmentVariables=DOTNET_TieredCompilation=0  IterationCount=15  
IterationTime=200ms  WarmupCount=5  

```
| Method        | Job                | Arguments                                                                                                   | Mean     | Error    | StdDev   | Gen0   | Gen1   | Gen2   | Allocated |
|-------------- |------------------- |------------------------------------------------------------------------------------------------------------ |---------:|---------:|---------:|-------:|-------:|-------:|----------:|
| StringControl | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 437.2 ns |  5.61 ns |  4.68 ns | 0.0597 | 0.0597 | 0.0597 |     556 B |
| StringControl | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 453.7 ns | 19.26 ns | 17.08 ns | 0.0607 | 0.0607 | 0.0607 |     556 B |
| StringControl | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 440.9 ns | 12.39 ns | 10.35 ns | 0.0594 | 0.0594 | 0.0594 |     556 B |
| StringControl | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 432.5 ns | 12.97 ns | 12.13 ns | 0.0595 | 0.0595 | 0.0595 |     556 B |
| StringControl | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 444.6 ns | 15.17 ns | 12.67 ns | 0.0604 | 0.0604 | 0.0604 |     556 B |
