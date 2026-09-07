```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=B  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=25  IterationTime=250ms  WarmupCount=8  

```
| Method                         | RecordCount | HeaderCount | Mean       | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|------------------------------- |------------ |------------ |-----------:|----------:|----------:|-------:|-------:|----------:|
| ParseSynchronousBatch          | 1024        | 0           | 183.736 μs | 0.3528 μs | 0.4710 μs | 5.0872 | 0.7267 |   90448 B |
| ParseWarmPreparedBatch         | 1024        | 0           | 163.644 μs | 0.3078 μs | 0.4109 μs | 5.2083 | 0.6510 |   90328 B |
| TraverseRetainedBatch          | 1024        | 0           |   3.527 μs | 0.0091 μs | 0.0121 μs |      - |      - |         - |
| ParseBorrowedSynchronousBatch  | 1024        | 0           | 173.292 μs | 0.1826 μs | 0.2438 μs |      - |      - |     128 B |
| ParseBorrowedWarmPreparedBatch | 1024        | 0           | 156.421 μs | 1.6989 μs | 2.2679 μs |      - |      - |     128 B |
| ParseBorrowedColdPreparedBatch | 1024        | 0           | 179.628 μs | 1.1444 μs | 1.5278 μs |      - |      - |     379 B |
| ParseColdPreparedBatch         | 1024        | 0           | 182.279 μs | 0.7114 μs | 0.9497 μs | 5.0872 | 0.7267 |   90795 B |
