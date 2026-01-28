```

BenchmarkDotNet v0.14.0, Ubuntu 25.10 (Questing Quokka)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-WSGRXV : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                         | Mean        | Error      | StdDev      | Allocated |
|----------------------------------------------- |------------:|-----------:|------------:|----------:|
| &#39;Protocol Write: 1000 Int32s&#39;                  | 18,103.3 ns | 6,018.5 ns | 3,581.49 ns |         - |
| &#39;Protocol Write: 100 Strings (100 chars each)&#39; |  6,578.1 ns |   182.1 ns |   108.39 ns |         - |
| &#39;Protocol Write: 100 CompactStrings&#39;           |  7,132.6 ns |   177.1 ns |   105.41 ns |         - |
| &#39;Protocol Read: 1000 Int32s&#39;                   | 28,363.2 ns | 8,200.4 ns | 4,879.95 ns |         - |
| &#39;RecordBatch: Write 10 Records&#39;                |  9,325.8 ns |   784.2 ns |   466.65 ns |         - |
| &#39;RecordBatch: Read 10 Records&#39;                 |    919.4 ns |   149.1 ns |    77.99 ns |         - |
| &#39;VarInt: Write 1000 values&#39;                    | 23,151.7 ns | 1,923.1 ns | 1,144.41 ns |         - |
| &#39;VarInt: Read 1000 values&#39;                     | 30,358.1 ns | 8,632.3 ns | 5,136.97 ns |         - |
| &#39;PartitionBatch: Create and append 50 records&#39; | 31,483.9 ns | 2,631.9 ns | 1,376.51 ns |   17088 B |
