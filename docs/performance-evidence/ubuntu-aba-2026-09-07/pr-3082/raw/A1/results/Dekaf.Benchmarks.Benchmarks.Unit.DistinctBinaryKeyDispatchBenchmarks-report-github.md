```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A1  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method    | KeySize | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------- |-------- |---------:|----------:|----------:|-------:|-------:|----------:|
| **ByteArray** | **8**       | **2.588 μs** | **0.0101 μs** | **0.0135 μs** | **0.1221** | **0.0407** |   **2.13 KB** |
| RawMemory | 8       | 2.590 μs | 0.0111 μs | 0.0148 μs | 0.1322 | 0.0407 |   2.22 KB |
| **ByteArray** | **1024**    | **2.605 μs** | **0.0229 μs** | **0.0306 μs** | **0.1247** | **0.0312** |   **2.13 KB** |
| RawMemory | 1024    | 2.559 μs | 0.0061 μs | 0.0082 μs | 0.1322 | 0.0407 |   2.22 KB |
| **ByteArray** | **65536**   | **2.604 μs** | **0.0078 μs** | **0.0104 μs** | **0.1274** | **0.0318** |   **2.13 KB** |
| RawMemory | 65536   | 2.575 μs | 0.0095 μs | 0.0127 μs | 0.1322 | 0.0407 |   2.22 KB |
