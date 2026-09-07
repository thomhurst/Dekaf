```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
INTEL XEON PLATINUM 8573C 2.30GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method              | Prefetch | Mean       | Error    | StdDev   | Allocated |
|-------------------- |--------- |-----------:|---------:|---------:|----------:|
| **SuccessfulFetch**     | **False**    | **1,216.4 ns** | **24.75 ns** | **33.04 ns** |     **184 B** |
| FollowerError       | False    | 1,564.1 ns | 40.40 ns | 53.93 ns |     248 B |
| LeaderError         | False    | 1,590.3 ns | 40.90 ns | 54.60 ns |     248 B |
| ResponsePoolControl | False    |   157.9 ns |  5.41 ns |  7.22 ns |         - |
| **SuccessfulFetch**     | **True**     | **1,211.2 ns** | **37.32 ns** | **49.82 ns** |     **184 B** |
| FollowerError       | True     | 1,636.1 ns | 37.04 ns | 49.45 ns |     248 B |
| LeaderError         | True     | 1,553.5 ns | 26.41 ns | 35.26 ns |     248 B |
| ResponsePoolControl | True     |   162.7 ns |  4.51 ns |  6.02 ns |         - |
