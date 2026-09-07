```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method    | KeySize | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------- |-------- |---------:|----------:|----------:|-------:|-------:|----------:|
| **ByteArray** | **8**       | **2.681 μs** | **0.0118 μs** | **0.0158 μs** | **0.1274** | **0.0425** |   **2.13 KB** |
| RawMemory | 8       | 2.615 μs | 0.0087 μs | 0.0116 μs | 0.1351 | 0.0416 |   2.22 KB |
| **ByteArray** | **1024**    | **2.719 μs** | **0.1207 μs** | **0.1612 μs** | **0.1247** | **0.0312** |   **2.13 KB** |
| RawMemory | 1024    | 2.642 μs | 0.0115 μs | 0.0154 μs | 0.1351 | 0.0416 |   2.22 KB |
| **ByteArray** | **65536**   | **2.655 μs** | **0.0135 μs** | **0.0180 μs** | **0.1274** | **0.0318** |   **2.13 KB** |
| RawMemory | 65536   | 2.613 μs | 0.0106 μs | 0.0142 μs | 0.1351 | 0.0416 |   2.22 KB |
