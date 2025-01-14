```

BenchmarkDotNet v0.14.0, Windows 10 (10.0.19045.4780/22H2/2022Update)
AMD Ryzen 7 5800X3D, 1 CPU, 16 logical and 8 physical cores
.NET SDK 8.0.403
  [Host]     : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2


```
| Method  | Mean     | Error    | StdDev   |
|-------- |---------:|---------:|---------:|
| Encrypt | 29.33 ms | 0.584 ms | 1.306 ms |
| Decrypt | 28.63 ms | 0.571 ms | 0.634 ms |
