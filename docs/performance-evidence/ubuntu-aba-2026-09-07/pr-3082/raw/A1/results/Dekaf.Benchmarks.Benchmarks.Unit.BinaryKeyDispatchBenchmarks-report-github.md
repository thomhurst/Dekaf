```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method        | Mean       | Error    | StdDev   | Gen0   | Gen1   | Gen2   | Allocated |
|-------------- |-----------:|---------:|---------:|-------:|-------:|-------:|----------:|
| ByteArray     | 2,868.1 ns | 21.01 ns | 28.05 ns | 0.1347 | 0.1010 | 0.0224 |    2207 B |
| RawMemory     | 2,742.5 ns | 17.83 ns | 23.80 ns | 0.1302 | 0.0977 | 0.0217 |    2308 B |
| StringControl |   673.0 ns |  7.83 ns | 10.46 ns | 0.0584 | 0.0584 | 0.0584 |     557 B |
