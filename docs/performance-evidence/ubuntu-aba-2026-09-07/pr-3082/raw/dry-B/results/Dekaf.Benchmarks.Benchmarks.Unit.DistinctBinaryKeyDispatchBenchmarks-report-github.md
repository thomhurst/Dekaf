```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryB  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method    | KeySize | Mean      | Error | Allocated |
|---------- |-------- |----------:|------:|----------:|
| **ByteArray** | **8**       | **19.433 μs** |    **NA** |   **2.16 KB** |
| RawMemory | 8       | 11.884 μs |    NA |   2.26 KB |
| **ByteArray** | **1024**    | **31.083 μs** |    **NA** |   **2.16 KB** |
| RawMemory | 1024    |  6.145 μs |    NA |   2.26 KB |
| **ByteArray** | **65536**   | **15.593 μs** |    **NA** |   **2.16 KB** |
| RawMemory | 65536   | 16.178 μs |    NA |   2.22 KB |
