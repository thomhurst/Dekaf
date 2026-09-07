```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method    | KeySize | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------- |-------- |---------:|----------:|----------:|-------:|-------:|----------:|
| **ByteArray** | **8**       | **2.538 μs** | **0.0192 μs** | **0.0257 μs** | **0.1221** | **0.0407** |   **2.13 KB** |
| RawMemory | 8       | 2.608 μs | 0.0092 μs | 0.0123 μs | 0.1351 | 0.0416 |   2.22 KB |
| **ByteArray** | **1024**    | **2.772 μs** | **0.0091 μs** | **0.0121 μs** | **0.1302** | **0.0326** |   **2.13 KB** |
| RawMemory | 1024    | 2.849 μs | 0.0087 μs | 0.0117 μs | 0.1249 | 0.0341 |   2.22 KB |
| **ByteArray** | **65536**   | **8.620 μs** | **0.0341 μs** | **0.0455 μs** | **0.1302** | **0.0326** |   **2.13 KB** |
| RawMemory | 65536   | 8.623 μs | 0.0748 μs | 0.0998 μs | 0.1302 | 0.0326 |   2.22 KB |
