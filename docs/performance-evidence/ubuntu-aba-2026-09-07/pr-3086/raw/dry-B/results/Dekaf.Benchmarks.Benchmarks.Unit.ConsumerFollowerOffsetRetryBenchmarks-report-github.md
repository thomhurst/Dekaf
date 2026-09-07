```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
INTEL XEON PLATINUM 8573C 2.30GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4

Job=DryB  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method              | Prefetch | Mean     | Error | Allocated |
|-------------------- |--------- |---------:|------:|----------:|
| **SuccessfulFetch**     | **False**    | **290.0 μs** |    **NA** |     **184 B** |
| FollowerError       | False    | 312.7 μs |    NA |     184 B |
| LeaderError         | False    | 314.9 μs |    NA |     712 B |
| ResponsePoolControl | False    | 270.6 μs |    NA |         - |
| **SuccessfulFetch**     | **True**     | **219.2 μs** |    **NA** |     **184 B** |
| FollowerError       | True     | 263.5 μs |    NA |     184 B |
| LeaderError         | True     | 254.2 μs |    NA |     712 B |
| ResponsePoolControl | True     | 179.0 μs |    NA |         - |
