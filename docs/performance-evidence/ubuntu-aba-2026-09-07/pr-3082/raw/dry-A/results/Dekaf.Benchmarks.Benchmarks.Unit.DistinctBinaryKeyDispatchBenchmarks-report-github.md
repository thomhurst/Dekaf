```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method    | KeySize | Mean     | Error | Allocated |
|---------- |-------- |---------:|------:|----------:|
| **ByteArray** | **8**       | **7.654 μs** |    **NA** |   **2.16 KB** |
| RawMemory | 8       | 6.409 μs |    NA |   2.25 KB |
| **ByteArray** | **1024**    | **6.177 μs** |    **NA** |   **2.16 KB** |
| RawMemory | 1024    | 6.416 μs |    NA |   2.26 KB |
| **ByteArray** | **65536**   | **8.520 μs** |    **NA** |   **2.16 KB** |
| RawMemory | 65536   | 8.609 μs |    NA |   2.25 KB |
