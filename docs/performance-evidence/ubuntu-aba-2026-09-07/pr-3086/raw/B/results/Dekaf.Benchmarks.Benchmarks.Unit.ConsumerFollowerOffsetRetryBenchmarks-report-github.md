```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
INTEL XEON PLATINUM 8573C 2.30GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method              | Prefetch | Mean       | Error    | StdDev   | Allocated |
|-------------------- |--------- |-----------:|---------:|---------:|----------:|
| **SuccessfulFetch**     | **False**    | **1,186.2 ns** | **28.74 ns** | **38.37 ns** |     **184 B** |
| FollowerError       | False    | 1,204.3 ns | 39.01 ns | 52.08 ns |     184 B |
| LeaderError         | False    | 1,444.3 ns | 30.45 ns | 40.65 ns |     248 B |
| ResponsePoolControl | False    |   154.5 ns |  3.92 ns |  5.23 ns |         - |
| **SuccessfulFetch**     | **True**     | **1,248.4 ns** | **42.25 ns** | **56.40 ns** |     **184 B** |
| FollowerError       | True     | 1,201.8 ns | 31.17 ns | 41.62 ns |     184 B |
| LeaderError         | True     | 1,473.3 ns | 29.87 ns | 39.87 ns |     248 B |
| ResponsePoolControl | True     |   157.6 ns |  4.32 ns |  5.77 ns |         - |
