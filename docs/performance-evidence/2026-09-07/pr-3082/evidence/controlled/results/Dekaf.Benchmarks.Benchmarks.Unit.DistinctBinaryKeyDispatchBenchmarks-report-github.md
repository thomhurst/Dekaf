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
| Method    | Job                | Arguments                                                                                                   | KeySize | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------- |------------------- |------------------------------------------------------------------------------------------------------------ |-------- |---------:|----------:|----------:|-------:|-------:|----------:|
| ByteArray | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 65536   | 1.283 μs | 0.0115 μs | 0.0096 μs | 0.1626 | 0.0455 |   2.13 KB |
| RawMemory | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 65536   | 1.303 μs | 0.0406 μs | 0.0360 μs | 0.1693 | 0.0439 |   2.22 KB |
| ByteArray | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 6.838 μs | 0.1070 μs | 0.0949 μs | 0.1502 | 0.0376 |   2.13 KB |
| RawMemory | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 7.300 μs | 0.2214 μs | 0.1849 μs | 0.1474 | 0.0369 |   2.22 KB |
| ByteArray | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 65536   | 5.775 μs | 0.1705 μs | 0.1331 μs | 0.1469 | 0.0294 |   2.13 KB |
| RawMemory | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 65536   | 5.833 μs | 0.1310 μs | 0.1023 μs | 0.1427 | 0.0357 |   2.22 KB |
| ByteArray | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 7.088 μs | 0.3204 μs | 0.2501 μs | 0.1539 | 0.0385 |   2.13 KB |
| RawMemory | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 6.830 μs | 0.0807 μs | 0.0755 μs | 0.1728 | 0.0346 |   2.22 KB |
| ByteArray | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 65536   | 1.294 μs | 0.0829 μs | 0.0735 μs | 0.1652 | 0.0463 |   2.13 KB |
| RawMemory | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 65536   | 1.301 μs | 0.0514 μs | 0.0456 μs | 0.1687 | 0.0454 |   2.22 KB |
