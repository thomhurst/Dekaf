```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 9V74 2.60GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=A2  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method           | Enabled | Mean       | Error     | StdDev    | Median     | Max        | Gen0   | Allocated |
|----------------- |-------- |-----------:|----------:|----------:|-----------:|-----------:|-------:|----------:|
| **SynchronousBatch** | **False**   | **521.076 ns** | **1.1342 ns** | **1.5142 ns** | **520.675 ns** | **527.202 ns** | **0.0084** |     **144 B** |
| PendingBatch     | False   | 948.786 ns | 3.9940 ns | 5.3318 ns | 947.910 ns | 969.906 ns | 0.0265 |     480 B |
| PublisherControl | False   |   5.559 ns | 0.0068 ns | 0.0091 ns |   5.558 ns |   5.589 ns |      - |         - |
| **SynchronousBatch** | **True**    | **517.182 ns** | **1.2267 ns** | **1.6376 ns** | **516.762 ns** | **524.047 ns** | **0.0083** |     **144 B** |
| PendingBatch     | True    | 959.202 ns | 3.0806 ns | 4.1125 ns | 958.730 ns | 971.803 ns | 0.0270 |     480 B |
| PublisherControl | True    |   5.559 ns | 0.0072 ns | 0.0096 ns |   5.560 ns |   5.592 ns |      - |         - |
