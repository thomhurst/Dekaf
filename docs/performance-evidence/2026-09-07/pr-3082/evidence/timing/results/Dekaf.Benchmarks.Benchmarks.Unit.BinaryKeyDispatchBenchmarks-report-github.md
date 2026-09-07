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

IterationCount=15  IterationTime=200ms  WarmupCount=5  

```
| Method        | Job                | Arguments                                                                                                   | Mean       | Error     | StdDev    | Gen0   | Gen1   | Gen2   | Allocated |
|-------------- |------------------- |------------------------------------------------------------------------------------------------------------ |-----------:|----------:|----------:|-------:|-------:|-------:|----------:|
| ByteArray     | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 1,451.8 ns |  39.63 ns |  33.09 ns | 0.1789 | 0.1491 | 0.0298 |    2272 B |
| RawMemory     | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 1,566.0 ns | 148.32 ns | 131.48 ns | 0.1788 | 0.1375 | 0.0275 |    2379 B |
| StringControl | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  |   311.1 ns |   3.93 ns |   3.28 ns | 0.0605 | 0.0605 | 0.0605 |     557 B |
| ByteArray     | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false |   300.5 ns |   5.26 ns |   4.66 ns | 0.0596 | 0.0596 | 0.0596 |     557 B |
| RawMemory     | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false |   328.3 ns |  15.35 ns |  13.61 ns | 0.0635 | 0.0635 | 0.0635 |     597 B |
| StringControl | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false |   312.5 ns |   5.41 ns |   4.80 ns | 0.0600 | 0.0600 | 0.0600 |     557 B |
| ByteArray     | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false |   307.6 ns |  15.23 ns |  13.50 ns | 0.0608 | 0.0608 | 0.0608 |     557 B |
| RawMemory     | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false |   312.9 ns |   6.89 ns |   6.11 ns | 0.0641 | 0.0641 | 0.0641 |     597 B |
| StringControl | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false |   305.9 ns |   7.02 ns |   6.57 ns | 0.0600 | 0.0600 | 0.0600 |     557 B |
| ByteArray     | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false |   312.2 ns |  17.98 ns |  16.82 ns | 0.0604 | 0.0604 | 0.0604 |     557 B |
| RawMemory     | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false |   304.4 ns |   4.98 ns |   4.66 ns | 0.0648 | 0.0648 | 0.0648 |     597 B |
| StringControl | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false |   308.1 ns |  11.34 ns |  10.06 ns | 0.0596 | 0.0596 | 0.0596 |     557 B |
| ByteArray     | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 1,452.5 ns |  47.15 ns |  39.37 ns | 0.1705 | 0.1395 | 0.0155 |    2276 B |
| RawMemory     | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 1,450.6 ns |  45.26 ns |  40.12 ns | 0.1924 | 0.1480 | 0.0296 |    2362 B |
| StringControl | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  |   317.2 ns |  14.46 ns |  12.07 ns | 0.0608 | 0.0608 | 0.0608 |     557 B |
