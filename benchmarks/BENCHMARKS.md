# Ghost Scraper Performance Benchmarks

This directory contains performance benchmarks for the Ghost Scraper infrastructure using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Overview

The benchmarks measure performance characteristics of:
- **Multi-strategy job parsers** for Indeed, Glassdoor, and Google Jobs
- **Circuit breaker resilience patterns** and overhead
- **Monitoring service** throughput and aggregation performance

## Project Structure

```
Ghost.Scraper.Benchmarks/
├── Ghost.Scraper.Benchmarks.csproj    # Project configuration
└── ParserBenchmarks.cs                # All benchmark implementations
```

## Building

```bash
# Build the benchmark project
dotnet build benchmarks/Ghost.Scraper.Benchmarks/Ghost.Scraper.Benchmarks.csproj -c Release

# Or from the root
dotnet build Ghost.sln -c Release
```

## Running Benchmarks

### Run all benchmarks
```bash
cd benchmarks/Ghost.Scraper.Benchmarks
dotnet run -c Release
```

### List all available benchmarks
```bash
dotnet run -c Release -- --list flat
```

### Run specific benchmark
```bash
dotnet run -c Release -- --filter *ParserBenchmarks*
```

### Run with specific parameters
```bash
# Run with custom job settings
dotnet run -c Release -- --warmupCount 5 --iterationCount 10

# Run and generate report
dotnet run -c Release -- --exportjson benchmarks.json
```

## Benchmark Categories

### 1. Parser Benchmarks (4 methods)

**Class:** `ParserBenchmarks`

#### Baseline: ParseIndeedWithMultiStrategy
- **Purpose:** Baseline parsing performance for Indeed jobs
- **Measures:** Time and memory allocation for typical HTML parsing
- **Setup:** Parses sample Indeed HTML with 3 job listings
- **Attributes:** `[Baseline]` - other parser tests compare to this

#### ParseIndeedWithLargeHtml
- **Purpose:** Performance scaling with large HTML documents
- **Measures:** Overhead when parsing 5x larger HTML
- **Setup:** Concatenates sample HTML 5 times, expects 15+ jobs
- **Insight:** Identifies memory pressure and O(n) scaling issues

#### ParseGlassdoorWithMultiStrategy
- **Purpose:** Glassdoor-specific parser performance
- **Measures:** Glassdoor HTML parsing with fallback strategies
- **Setup:** Parses sample Glassdoor HTML structure

#### ParseGoogleJobsWithMultiStrategy
- **Purpose:** Google Jobs parser performance
- **Measures:** Google Jobs HTML parsing with multiple fallback strategies
- **Setup:** Parses sample Google Jobs HTML structure

### 2. Circuit Breaker Benchmarks (4 methods)

**Class:** `CircuitBreakerBenchmarks`

#### Baseline: CircuitBreakerHttpRequestOverhead_Closed
- **Purpose:** Wrapper overhead when circuit is operational (closed state)
- **Measures:** Cost of circuit breaker pattern on HTTP requests
- **Setup:** Mock HTTP request returning 200 OK
- **Attributes:** `[Baseline]` - measures pure wrapper cost

#### CircuitBreakerStateTransition_ClosedToOpen
- **Purpose:** Performance of state transitions
- **Measures:** Time to transition from Closed → Open → Closed
- **Setup:** Manual open/reset cycle
- **Insight:** Critical path for circuit breaker activation

#### CircuitBreakerStateTransition_RecoveryPath
- **Purpose:** Recovery path performance
- **Measures:** Time for Closed → Open → Closed recovery
- **Setup:** Full recovery cycle simulation
- **Insight:** Impact of circuit breaker reset operations

#### CircuitBreakerMetricsCollection
- **Purpose:** Metrics snapshot performance
- **Measures:** Overhead of retrieving all platform metrics
- **Setup:** Retrieves metrics for all registered platforms
- **Insight:** Monitoring query performance

### 3. Monitoring Benchmarks (7 methods)

**Class:** `MonitoringBenchmarks`

#### Baseline: RecordRequest_SinglePlatform
- **Purpose:** Single request recording throughput
- **Measures:** Requests per second for successful requests
- **Setup:** Records one successful request for a platform
- **Attributes:** `[Baseline]` - reference for other monitoring tests

#### RecordRequest_WithErrorCategory
- **Purpose:** Error tracking overhead
- **Measures:** Recording cost when tracking error categories
- **Setup:** Records failed request with error category
- **Insight:** Cost of detailed error tracking

#### GetPlatformHealth_SinglePlatform
- **Purpose:** Single platform health calculation latency
- **Measures:** Time to compute health status for one platform
- **Setup:** Pre-populated metrics for 3 platforms
- **Insight:** Real-time health check performance

#### GetAllPlatformHealth_Aggregation
- **Purpose:** Multi-platform health aggregation
- **Measures:** Latency to aggregate health across all platforms
- **Setup:** Retrieves health for 3 platforms
- **Insight:** Aggregation query overhead

#### GetCurrentMetrics_Aggregation
- **Purpose:** Full metrics snapshot performance
- **Measures:** Cost of comprehensive metrics aggregation
- **Setup:** Per-platform metrics collection from 3 platforms
- **Insight:** Expensive aggregation operations

#### ShouldAlert_ThresholdCheck
- **Purpose:** Alert threshold evaluation performance
- **Measures:** Cost of checking if platform needs alert
- **Setup:** Evaluates threshold against pre-populated metrics
- **Insight:** Real-time alert trigger evaluation

#### BatchMonitoring_RecordAndCheck
- **Purpose:** Realistic continuous monitoring workload
- **Measures:** Cost of batch recording and aggregation
- **Setup:** Records 10 requests then retrieves all metrics and health
- **Insight:** Combined workflow of typical monitoring scenario

## Configuration

All benchmarks use consistent BenchmarkDotNet configuration:

```csharp
[MemoryDiagnoser]                    // Track memory allocations
[SimpleJob(
    launchCount: 3,                  // Launch process 3 times
    warmupCount: 3,                  // Warm-up iterations: 3
    iterationCount: 5                // Measurement iterations: 5
)]
```

This configuration ensures:
- **Stability:** 3 process launches for JIT consistency
- **Accuracy:** 3 warm-up iterations to eliminate startup effects
- **Precision:** 5 iterations for statistical significance
- **Memory tracking:** GC allocations and peak memory usage

## Sample HTML Data

Benchmarks use realistic sample HTML:

- **Indeed**: 3 job listings with titles, companies, locations, salaries, descriptions
- **Glassdoor**: 2 job listings with Glassdoor-specific HTML structure
- **Google Jobs**: 2 job listings with Google Jobs widget structure

## Performance Expectations

### Parser Benchmarks
- **Baseline (Indeed)**: ~1-5ms per typical HTML document
- **Large HTML (5x)**: Should show linear scaling ~5-25ms
- **Glassdoor/Google**: Similar range with platform-specific optimizations

### Circuit Breaker Benchmarks
- **HTTP overhead**: <1ms for wrapper overhead when closed
- **State transitions**: <1ms for state change operations
- **Metrics collection**: <1ms for per-platform snapshots

### Monitoring Benchmarks
- **Single request**: <1ms for recording
- **Health check**: <1ms for single platform calculation
- **Full aggregation**: <5ms for all platforms combined
- **Batch operations**: <10ms for realistic workloads

## Integration with CI/CD

To run benchmarks in CI/CD:

```bash
# Generate full results
dotnet run -c Release --project benchmarks/Ghost.Scraper.Benchmarks -- \
  --exportjson benchmarks-results.json \
  --exportcsv benchmarks-results.csv

# Upload or analyze results
# Compare against baseline results
```

## Interpreting Results

BenchmarkDotNet provides detailed output:

```
| Method                                      | Mean      | Error    | StdDev   | Ratio | Allocated |
|---------------------------------------------|-----------|----------|----------|-------|-----------|
| ParseIndeedWithMultiStrategy (baseline)     | 2.345 ms  | 0.123 ms | 0.045 ms | 1.00  | 15.2 KB   |
| ParseIndeedWithLargeHtml                    | 11.456 ms | 0.456 ms | 0.234 ms | 4.88  | 76.0 KB   |
| ParseGlassdoorWithMultiStrategy             | 2.123 ms  | 0.089 ms | 0.035 ms | 0.91  | 12.5 KB   |
```

Key metrics:
- **Mean**: Average execution time
- **Error**: Standard error
- **StdDev**: Standard deviation (lower is more stable)
- **Ratio**: Comparison to baseline
- **Allocated**: Memory allocations in bytes

## Troubleshooting

### Benchmarks are slow/timing out
- Reduce iterations: `--iterationCount 3`
- Skip warmup: `--warmupCount 0`
- Disable memory diagnostics: Remove `[MemoryDiagnoser]`

### High variance in results
- Increase warmup: `--warmupCount 5`
- Ensure system is idle (close other applications)
- Check for background processes affecting performance

### Memory tracking missing
- Ensure `[MemoryDiagnoser]` attribute is present
- Run with admin/root permissions (some platforms require this)

## Further Reading

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Performance Benchmarking Guide](https://benchmarkdotnet.org/articles/guides/getting-started.html)
- [Memory Diagnostics](https://benchmarkdotnet.org/articles/features/memory-diagnostics.html)
