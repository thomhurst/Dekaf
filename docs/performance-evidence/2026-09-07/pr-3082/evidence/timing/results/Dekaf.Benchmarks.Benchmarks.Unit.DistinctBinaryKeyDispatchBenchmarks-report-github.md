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
| Method    | Job                | Arguments                                                                                                   | KeySize | Mean      | Error      | StdDev     | Median    | Gen0   | Gen1   | Allocated |
|---------- |------------------- |------------------------------------------------------------------------------------------------------------ |-------- |----------:|-----------:|-----------:|----------:|-------:|-------:|----------:|
| **ByteArray** | **A_Baseline**         | **/p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false**  | **8**       |  **1.529 μs** |  **0.0400 μs** |  **0.0354 μs** |  **1.515 μs** | **0.1724** | **0.0675** |   **2.25 KB** |
| RawMemory | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 8       |  1.510 μs |  0.0305 μs |  0.0238 μs |  1.509 μs | 0.1786 | 0.0699 |   2.34 KB |
| ByteArray | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 8       |  1.552 μs |  0.0713 μs |  0.0596 μs |  1.529 μs | 0.1751 | 0.0609 |   2.25 KB |
| RawMemory | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 8       |  1.496 μs |  0.0128 μs |  0.0107 μs |  1.495 μs | 0.1796 | 0.0673 |   2.34 KB |
| ByteArray | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 8       |  1.539 μs |  0.0627 μs |  0.0556 μs |  1.515 μs | 0.1724 | 0.0525 |   2.25 KB |
| RawMemory | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 8       |  1.525 μs |  0.0196 μs |  0.0164 μs |  1.520 μs | 0.1805 | 0.0752 |   2.34 KB |
| ByteArray | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 8       |  1.532 μs |  0.0301 μs |  0.0282 μs |  1.527 μs | 0.1643 | 0.0730 |   2.26 KB |
| RawMemory | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 8       |  1.552 μs |  0.0956 μs |  0.0847 μs |  1.527 μs | 0.1784 | 0.0669 |   2.34 KB |
| ByteArray | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 8       |  1.546 μs |  0.0356 μs |  0.0333 μs |  1.531 μs | 0.1725 | 0.0517 |   2.25 KB |
| RawMemory | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 8       |  1.546 μs |  0.0226 μs |  0.0188 μs |  1.548 μs | 0.1779 | 0.0647 |   2.34 KB |
| **ByteArray** | **A_Baseline**         | **/p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false**  | **1024**    |  **1.505 μs** |  **0.0473 μs** |  **0.0369 μs** |  **1.503 μs** | **0.1772** | **0.0531** |   **2.25 KB** |
| RawMemory | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 1024    |  1.542 μs |  0.0492 μs |  0.0436 μs |  1.525 μs | 0.1817 | 0.0496 |   2.34 KB |
| ByteArray | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 1024    |  3.505 μs |  0.7018 μs |  0.6564 μs |  3.920 μs | 0.1002 |      - |   2.25 KB |
| RawMemory | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 1024    |  3.646 μs |  0.8627 μs |  0.8069 μs |  4.089 μs | 0.1028 |      - |   2.34 KB |
| ByteArray | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 1024    |  1.583 μs |  0.0988 μs |  0.0825 μs |  1.557 μs | 0.1682 | 0.0561 |   2.25 KB |
| RawMemory | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 1024    |  1.539 μs |  0.0090 μs |  0.0075 μs |  1.541 μs | 0.1830 | 0.0716 |   2.35 KB |
| ByteArray | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 1024    |  1.615 μs |  0.0498 μs |  0.0442 μs |  1.595 μs | 0.1726 | 0.0628 |   2.25 KB |
| RawMemory | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 1024    |  3.487 μs |  0.9856 μs |  0.9219 μs |  3.962 μs | 0.1015 |      - |   2.34 KB |
| ByteArray | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 1024    |  1.566 μs |  0.0905 μs |  0.0755 μs |  1.538 μs | 0.1739 | 0.0605 |   2.25 KB |
| RawMemory | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 1024    |  1.523 μs |  0.0142 μs |  0.0111 μs |  1.526 μs | 0.1819 | 0.0682 |   2.34 KB |
| **ByteArray** | **A_Baseline**         | **/p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false**  | **65536**   |  **1.518 μs** |  **0.0149 μs** |  **0.0125 μs** |  **1.518 μs** | **0.1695** | **0.0404** |   **2.25 KB** |
| RawMemory | A_Baseline         | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 65536   |  1.588 μs |  0.0894 μs |  0.0792 μs |  1.588 μs | 0.1820 | 0.0455 |   2.34 KB |
| ByteArray | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 38.826 μs |  1.7665 μs |  1.6524 μs | 38.282 μs |      - |      - |   2.17 KB |
| RawMemory | B_Published        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 25.770 μs | 12.5225 μs | 11.7135 μs | 18.605 μs |      - |      - |   2.35 KB |
| ByteArray | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 65536   |  6.797 μs |  0.3097 μs |  0.2745 μs |  6.774 μs | 0.1502 |      - |   2.26 KB |
| RawMemory | C_Candidate        | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/candidate,/p:UseSharedCompilation=false | 65536   | 31.309 μs |  1.2671 μs |  1.1852 μs | 31.289 μs |      - |      - |   2.34 KB |
| ByteArray | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 23.363 μs | 12.6848 μs | 11.8654 μs | 20.085 μs |      - |      - |   2.26 KB |
| RawMemory | D_PublishedControl | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/published,/p:UseSharedCompilation=false | 65536   | 42.934 μs |  7.6927 μs |  7.1958 μs | 39.193 μs |      - |      - |   2.34 KB |
| ByteArray | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 65536   |  1.534 μs |  0.0326 μs |  0.0272 μs |  1.533 μs | 0.1706 | 0.0341 |   2.25 KB |
| RawMemory | E_BaselineControl  | /p:ProductDirectory=C:/git/Dekaf-perf-improvements-20260907/pr-3082/baseline,/p:UseSharedCompilation=false  | 65536   |  1.512 μs |  0.0270 μs |  0.0225 μs |  1.518 μs | 0.1695 | 0.0339 |   2.34 KB |
