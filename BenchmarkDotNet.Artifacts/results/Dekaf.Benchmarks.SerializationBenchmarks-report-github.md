```

BenchmarkDotNet v0.14.0, Ubuntu 25.10 (Questing Quokka)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 12 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  Job-VEMOLS : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                            | StringLength | Mean        | Error       | StdDev      | Median      | Allocated |
|---------------------------------- |------------- |------------:|------------:|------------:|------------:|----------:|
| **&#39;Dekaf Writer: VarInt (small)&#39;**    | **10**           |  **44.1667 ns** |  **89.1344 ns** |  **53.0424 ns** |  **24.5000 ns** |         **-** |
| &#39;Dekaf Writer: VarInt (large)&#39;    | 10           |  24.9375 ns |  30.1680 ns |  15.7785 ns |  30.5000 ns |         - |
| &#39;Dekaf Writer: VarInt (negative)&#39; | 10           |  22.7778 ns |  67.6455 ns |  40.2547 ns |   0.0000 ns |         - |
| **&#39;Dekaf Writer: VarInt (small)&#39;**    | **100**          |  **41.9375 ns** |  **42.8611 ns** |  **22.4172 ns** |  **44.5000 ns** |         **-** |
| &#39;Dekaf Writer: VarInt (large)&#39;    | 100          | 538.5000 ns |  25.6315 ns |  13.4058 ns | 533.0000 ns |         - |
| &#39;Dekaf Writer: VarInt (negative)&#39; | 100          | 440.1000 ns | 458.1094 ns | 303.0110 ns | 590.5000 ns |         - |
| **&#39;Dekaf Writer: VarInt (small)&#39;**    | **1000**         |  **48.3333 ns** |  **87.3624 ns** |  **51.9880 ns** |  **28.0000 ns** |         **-** |
| &#39;Dekaf Writer: VarInt (large)&#39;    | 1000         |  16.1250 ns |  27.7907 ns |  14.5351 ns |  19.0000 ns |         - |
| &#39;Dekaf Writer: VarInt (negative)&#39; | 1000         |   0.0000 ns |   0.0000 ns |   0.0000 ns |   0.0000 ns |         - |
