```

BenchmarkDotNet v0.14.0, Ubuntu 25.10 (Questing Quokka)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-NMRVZI : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

IterationCount=10  WarmupCount=3  

```
| Method                                             | Mean         | Error      | StdDev     | Gen0   | Gen1   | Allocated |
|--------------------------------------------------- |-------------:|-----------:|-----------:|-------:|-------:|----------:|
| &#39;String Serialize: ArrayBufferWriter + Copy&#39;       |     44.95 ns |   1.130 ns |   0.747 ns | 0.0306 |      - |     400 B |
| &#39;String Serialize: PooledBufferWriter Direct&#39;      |     30.75 ns |   1.180 ns |   0.780 ns | 0.0214 |      - |     280 B |
| &#39;String Serialize x100: ArrayBufferWriter + Copy&#39;  |  4,067.22 ns | 111.866 ns |  73.992 ns | 2.3804 |      - |   31200 B |
| &#39;String Serialize x100: PooledBufferWriter Direct&#39; |  1,819.48 ns |   6.764 ns |   4.474 ns |      - |      - |         - |
| &#39;Key+Value Serialize: ArrayBufferWriter + Copy&#39;    |    111.27 ns |   3.040 ns |   2.011 ns | 0.0758 | 0.0002 |     992 B |
| &#39;Key+Value Serialize: PooledBufferWriter Direct&#39;   |     65.03 ns |   1.363 ns |   0.902 ns | 0.0428 |      - |     560 B |
| &#39;Key+Value x100: ArrayBufferWriter + Copy&#39;         | 10,799.86 ns | 162.591 ns | 107.544 ns | 4.7607 |      - |   62400 B |
| &#39;Key+Value x100: PooledBufferWriter Direct&#39;        |  7,474.64 ns |  71.935 ns |  47.581 ns |      - |      - |         - |
| &#39;Int32 Serialize x1000: ArrayBufferWriter + Copy&#39;  | 19,381.68 ns | 289.276 ns | 191.338 ns | 4.8828 |      - |   64000 B |
| &#39;Int32 Serialize x1000: PooledBufferWriter Direct&#39; |  8,541.56 ns |  60.570 ns |  36.044 ns |      - |      - |         - |
| &#39;Large 10KB Serialize: ArrayBufferWriter + Copy&#39;   |  1,428.58 ns |  42.346 ns |  28.009 ns | 2.5120 | 0.3128 |   32848 B |
| &#39;Large 10KB Serialize: PooledBufferWriter Direct&#39;  |    567.52 ns |   8.912 ns |   5.895 ns | 1.2512 | 0.0772 |   16408 B |
