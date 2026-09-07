```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method        | Mean       | Error    | StdDev   | Gen0   | Gen1   | Gen2   | Allocated |
|-------------- |-----------:|---------:|---------:|-------:|-------:|-------:|----------:|
| ByteArray     | 2,898.5 ns | 16.45 ns | 21.96 ns | 0.1395 | 0.1046 | 0.0233 |    2207 B |
| RawMemory     | 2,786.6 ns | 14.73 ns | 19.66 ns | 0.1249 | 0.0908 | 0.0227 |    2307 B |
| StringControl |   588.8 ns |  8.64 ns | 11.54 ns | 0.0587 | 0.0587 | 0.0587 |     557 B |
