```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method     | Mean      | Error     | StdDev    | Allocated |
|----------- |----------:|----------:|----------:|----------:|
| Typed      | 71.014 μs | 0.5549 μs | 0.7408 μs |     128 B |
| TypedEpoch | 70.803 μs | 0.0518 μs | 0.0692 μs |     128 B |
| RawControl |  7.407 μs | 0.0615 μs | 0.0821 μs |      72 B |
