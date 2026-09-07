```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=300  UnrollFactor=1  
WarmupCount=30  

```
| Method         | Mean     | Error   | StdDev   | Median   | Allocated |
|--------------- |---------:|--------:|---------:|---------:|----------:|
| DrainFullQueue | 202.6 μs | 2.45 μs | 12.78 μs | 196.6 μs |   53.3 KB |
