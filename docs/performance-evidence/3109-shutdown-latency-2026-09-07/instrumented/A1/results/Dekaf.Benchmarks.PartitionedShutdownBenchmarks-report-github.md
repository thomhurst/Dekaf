```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=300  UnrollFactor=1  
WarmupCount=30  

```
| Method         | Mean     | Error   | StdDev   | Median   | Allocated |
|--------------- |---------:|--------:|---------:|---------:|----------:|
| DrainFullQueue | 204.3 μs | 2.36 μs | 12.29 μs | 199.1 μs |  50.47 KB |
