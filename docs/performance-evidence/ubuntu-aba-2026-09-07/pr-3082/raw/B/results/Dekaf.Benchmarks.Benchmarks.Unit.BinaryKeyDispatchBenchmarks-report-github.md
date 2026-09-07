```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method        | Mean     | Error   | StdDev   | Gen0   | Gen1   | Gen2   | Allocated |
|-------------- |---------:|--------:|---------:|-------:|-------:|-------:|----------:|
| ByteArray     | 651.1 ns | 9.21 ns | 12.30 ns | 0.0586 | 0.0586 | 0.0586 |     557 B |
| RawMemory     | 546.7 ns | 4.72 ns |  6.30 ns | 0.0629 | 0.0629 | 0.0629 |     597 B |
| StringControl | 657.5 ns | 5.83 ns |  7.78 ns | 0.0585 | 0.0585 | 0.0585 |     557 B |
