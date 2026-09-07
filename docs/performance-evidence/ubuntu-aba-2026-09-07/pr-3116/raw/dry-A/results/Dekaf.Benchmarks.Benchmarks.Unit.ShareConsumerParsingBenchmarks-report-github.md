```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763 2.45GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=DryA  OutlierMode=DontRemove  Toolchain=InProcessEmitToolchain  
IterationCount=1  LaunchCount=1  RunStrategy=ColdStart  
UnrollFactor=1  WarmupCount=1  

```
| Method                         | RecordCount | HeaderCount | Mean       | Error | Allocated |
|------------------------------- |------------ |------------ |-----------:|------:|----------:|
| ParseSynchronousBatch          | 1024        | 0           |   505.3 μs |    NA |   90448 B |
| ParseWarmPreparedBatch         | 1024        | 0           |   442.4 μs |    NA |   90328 B |
| TraverseRetainedBatch          | 1024        | 0           |   219.1 μs |    NA |         - |
| ParseBorrowedSynchronousBatch  | 1024        | 0           | 1,060.6 μs |    NA |   90448 B |
| ParseBorrowedWarmPreparedBatch | 1024        | 0           |   481.1 μs |    NA |   90328 B |
| ParseBorrowedColdPreparedBatch | 1024        | 0           | 1,386.3 μs |    NA |   90792 B |
| ParseColdPreparedBatch         | 1024        | 0           |   484.5 μs |    NA |   90792 B |
