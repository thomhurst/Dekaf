```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.3 LTS (Noble Numbat)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-TFWKVB : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                      | Mean      | Error     | StdDev    | Allocated |
|------------------------------------------------------------ |----------:|----------:|----------:|----------:|
| &#39;Serialization: ArrayBufferWriter + Copy (old pattern)&#39;     |  2.603 μs | 0.4519 μs | 0.2689 μs |         - |
| &#39;Serialization: PooledBufferWriter Direct (new pattern)&#39;    |  2.583 μs | 0.4062 μs | 0.2417 μs |         - |
| &#39;Serialization: ArrayBufferWriter + Copy (100 iterations)&#39;  | 15.573 μs | 1.6051 μs | 0.9551 μs |   31648 B |
| &#39;Serialization: PooledBufferWriter Direct (100 iterations)&#39; | 14.499 μs | 0.2955 μs | 0.1759 μs |         - |
