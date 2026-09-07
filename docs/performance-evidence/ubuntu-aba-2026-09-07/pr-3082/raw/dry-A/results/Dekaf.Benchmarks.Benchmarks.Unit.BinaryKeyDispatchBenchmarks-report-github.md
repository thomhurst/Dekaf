```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method        | Mean      | Error | Allocated |
|-------------- |----------:|------:|----------:|
| ByteArray     | 85.959 μs |    NA |    2212 B |
| RawMemory     | 57.790 μs |    NA |    2306 B |
| StringControl |  1.693 μs |    NA |     556 B |
