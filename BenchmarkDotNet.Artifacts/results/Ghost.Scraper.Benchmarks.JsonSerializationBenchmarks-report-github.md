```

BenchmarkDotNet v0.15.0, Linux Debian GNU/Linux 13 (trixie)
Intel Core i5-7500 CPU 3.40GHz (Kaby Lake), 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2
  Dry    : .NET 10.0.3 (10.0.326.7603), X64 RyuJIT AVX2


```
| Method                                | Job        | Runtime   | IterationCount | LaunchCount | RunStrategy | UnrollFactor | WarmupCount | Mean        | Error | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------- |----------- |---------- |--------------- |------------ |------------ |------------- |------------ |------------:|------:|------:|--------:|----------:|------------:|
| SerializeWithReflection               | Job-PLFSNH | .NET 9.0  | 5              | 3           | Default     | 16           | 3           |          NA |    NA |     ? |       ? |        NA |           ? |
| SerializeWithSourceGenerator          | Job-PLFSNH | .NET 9.0  | 5              | 3           | Default     | 16           | 3           |          NA |    NA |     ? |       ? |        NA |           ? |
| DeserializeWithReflection             | Job-PLFSNH | .NET 9.0  | 5              | 3           | Default     | 16           | 3           |          NA |    NA |     ? |       ? |        NA |           ? |
| DeserializeWithSourceGenerator        | Job-PLFSNH | .NET 9.0  | 5              | 3           | Default     | 16           | 3           |          NA |    NA |     ? |       ? |        NA |           ? |
| SerializeSingleJobWithReflection      | Job-PLFSNH | .NET 9.0  | 5              | 3           | Default     | 16           | 3           |          NA |    NA |     ? |       ? |        NA |           ? |
| SerializeSingleJobWithSourceGenerator | Job-PLFSNH | .NET 9.0  | 5              | 3           | Default     | 16           | 3           |          NA |    NA |     ? |       ? |        NA |           ? |
|                                       |            |           |                |             |             |              |             |             |       |       |         |           |             |
| SerializeWithReflection               | Dry        | .NET 10.0 | 1              | 1           | ColdStart   | 1            | 1           |    597.6 μs |    NA |  1.00 |    0.00 |   76544 B |        1.00 |
| SerializeWithSourceGenerator          | Dry        | .NET 10.0 | 1              | 1           | ColdStart   | 1            | 1           | 16,712.7 μs |    NA | 27.97 |    0.00 |   76224 B |        1.00 |
| DeserializeWithReflection             | Dry        | .NET 10.0 | 1              | 1           | ColdStart   | 1            | 1           |  7,923.1 μs |    NA | 13.26 |    0.00 |   68936 B |        0.90 |
| DeserializeWithSourceGenerator        | Dry        | .NET 10.0 | 1              | 1           | ColdStart   | 1            | 1           | 20,316.3 μs |    NA | 34.00 |    0.00 |   87672 B |        1.15 |
| SerializeSingleJobWithReflection      | Dry        | .NET 10.0 | 1              | 1           | ColdStart   | 1            | 1           |    335.6 μs |    NA |  0.56 |    0.00 |     848 B |        0.01 |
| SerializeSingleJobWithSourceGenerator | Dry        | .NET 10.0 | 1              | 1           | ColdStart   | 1            | 1           | 14,161.7 μs |    NA | 23.70 |    0.00 |    1856 B |        0.02 |

Benchmarks with issues:
  JsonSerializationBenchmarks.SerializeWithReflection: Job-PLFSNH(Runtime=.NET 9.0, IterationCount=5, LaunchCount=3, WarmupCount=3)
  JsonSerializationBenchmarks.SerializeWithSourceGenerator: Job-PLFSNH(Runtime=.NET 9.0, IterationCount=5, LaunchCount=3, WarmupCount=3)
  JsonSerializationBenchmarks.DeserializeWithReflection: Job-PLFSNH(Runtime=.NET 9.0, IterationCount=5, LaunchCount=3, WarmupCount=3)
  JsonSerializationBenchmarks.DeserializeWithSourceGenerator: Job-PLFSNH(Runtime=.NET 9.0, IterationCount=5, LaunchCount=3, WarmupCount=3)
  JsonSerializationBenchmarks.SerializeSingleJobWithReflection: Job-PLFSNH(Runtime=.NET 9.0, IterationCount=5, LaunchCount=3, WarmupCount=3)
  JsonSerializationBenchmarks.SerializeSingleJobWithSourceGenerator: Job-PLFSNH(Runtime=.NET 9.0, IterationCount=5, LaunchCount=3, WarmupCount=3)
