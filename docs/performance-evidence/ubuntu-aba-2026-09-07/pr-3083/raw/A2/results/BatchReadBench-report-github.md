```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method     | Mean      | Error     | StdDev    | Allocated |
|----------- |----------:|----------:|----------:|----------:|
| Typed      | 64.259 μs | 0.0417 μs | 0.0557 μs |     128 B |
| TypedEpoch | 64.513 μs | 0.1514 μs | 0.2021 μs |     128 B |
| RawControl |  7.387 μs | 0.0181 μs | 0.0242 μs |      72 B |
