# Ghost Scraper Benchmarks

Performance benchmarking suite for Ghost Scraper using BenchmarkDotNet.

## 📊 What's Being Benchmarked

Three core components:

1. **Job Parsers** - Indeed, Glassdoor, Google Jobs parsing performance
2. **Circuit Breaker** - Resilience pattern overhead and state management
3. **Monitoring Service** - Request recording, health checks, metrics aggregation

## 🚀 Quick Start

```bash
# Build project
dotnet build benchmarks/Ghost.Scraper.Benchmarks/ -c Release

# Run all benchmarks
cd benchmarks/Ghost.Scraper.Benchmarks && dotnet run -c Release

# List all benchmarks
dotnet run -c Release -- --list flat

# Run single category
dotnet run -c Release -- --filter "*ParserBenchmarks*"
```

## 📈 15 Benchmark Methods

### Parser Benchmarks (4)
- Parse Indeed job listings [baseline]
- Parse Indeed with large HTML (5x size)
- Parse Glassdoor job listings
- Parse Google Jobs listings

### Circuit Breaker Benchmarks (4)
- HTTP request overhead [baseline]
- State transition: Closed → Open
- State transition: Recovery path
- Metrics collection cost

### Monitoring Benchmarks (7)
- Record single request [baseline]
- Record request with error category
- Get single platform health
- Get all platforms health (aggregation)
- Get current metrics snapshot
- Check alert threshold
- Batch monitoring: record + check

## 📋 Files

```
benchmarks/
├── README.md                           # This file
├── BENCHMARKS.md                       # Detailed documentation
└── Ghost.Scraper.Benchmarks/
    ├── Ghost.Scraper.Benchmarks.csproj
    └── ParserBenchmarks.cs             # All benchmark implementations
```

## 🔧 Configuration

All benchmarks configured with:
- **Memory Diagnostics** - Track allocations and GC
- **Process Launches** - 3 launches for JIT stability
- **Warm-up Iterations** - 3 iterations to eliminate startup effects
- **Measurement Iterations** - 5 iterations for statistical significance

## 📖 Documentation

See [BENCHMARKS.md](./BENCHMARKS.md) for:
- Detailed benchmark descriptions
- Performance expectations
- How to interpret results
- Integration with CI/CD
- Troubleshooting guide

## 🎯 Performance Targets

- **Parser Benchmarks**: 1-5ms baseline, linear scaling
- **Circuit Breaker**: <1ms overhead when closed
- **Monitoring**: <1ms per request, <5ms aggregation

## 💡 Tips

```bash
# Generate JSON report
dotnet run -c Release -- --exportjson results.json

# Custom iterations (faster results)
dotnet run -c Release -- --warmupCount 1 --iterationCount 3

# Run with memory diagnostics disabled (faster)
# Requires code change to remove [MemoryDiagnoser]

# Compare against baseline results
dotnet run -c Release -- --join results-baseline.json
```

## 🔍 Next Steps

1. Run the benchmarks: `dotnet run -c Release`
2. Review the results table
3. Check [BENCHMARKS.md](./BENCHMARKS.md) for interpretation
4. Use results to identify performance regressions
5. Monitor trends in CI/CD pipeline
