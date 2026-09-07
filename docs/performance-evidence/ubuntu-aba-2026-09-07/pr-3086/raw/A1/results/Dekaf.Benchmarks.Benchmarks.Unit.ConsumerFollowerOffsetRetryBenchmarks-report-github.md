```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
INTEL XEON PLATINUM 8573C 2.30GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method              | Prefetch | Mean       | Error    | StdDev   | Allocated |
|-------------------- |--------- |-----------:|---------:|---------:|----------:|
| **SuccessfulFetch**     | **False**    | **1,184.3 ns** | **28.35 ns** | **37.84 ns** |     **184 B** |
| FollowerError       | False    | 1,577.6 ns | 36.69 ns | 48.98 ns |     248 B |
| LeaderError         | False    | 1,570.8 ns | 47.58 ns | 63.51 ns |     248 B |
| ResponsePoolControl | False    |   159.3 ns |  4.93 ns |  6.59 ns |         - |
| **SuccessfulFetch**     | **True**     | **1,189.7 ns** | **22.92 ns** | **30.59 ns** |     **184 B** |
| FollowerError       | True     | 1,580.4 ns | 29.11 ns | 38.86 ns |     248 B |
| LeaderError         | True     | 1,582.9 ns | 40.46 ns | 54.01 ns |     248 B |
| ResponsePoolControl | True     |   155.3 ns |  4.28 ns |  5.71 ns |         - |
