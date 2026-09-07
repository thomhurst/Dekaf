```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method     | Mean      | Error     | StdDev    | Allocated |
|----------- |----------:|----------:|----------:|----------:|
| Typed      | 64.573 μs | 0.8144 μs | 1.0872 μs |     128 B |
| TypedEpoch | 64.172 μs | 0.2420 μs | 0.3231 μs |     128 B |
| RawControl |  7.403 μs | 0.0592 μs | 0.0791 μs |      72 B |
