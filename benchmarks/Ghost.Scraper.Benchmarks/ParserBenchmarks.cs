using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Glassdoor.Internal;
using Ghost.Platform.Google.Jobs.Internal;
using Ghost.Platform.Indeed.Internal;
using Ghost.Scraper.DotnetSpider.Monitoring;
using Ghost.Scraper.DotnetSpider.Resilience;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ghost.Scraper.Benchmarks;

/// <summary>
/// Performance benchmarks for Ghost Scraper job parsing and monitoring infrastructure.
/// Uses BenchmarkDotNet with memory diagnostics and configurable warm-up/iteration counts.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var config = BenchmarkDotNet.Configs.ManualConfig
            .Create(BenchmarkDotNet.Configs.DefaultConfig.Instance);
        
        var summary = BenchmarkRunner.Run(
            typeof(Program).Assembly,
            args: args ?? Array.Empty<string>());
    }
}

/// <summary>
/// Benchmarks for multi-strategy job parsers across different platforms.
/// Measures parsing speed with memory allocation tracking.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 3, iterationCount: 5)]
public class ParserBenchmarks
{
    private IndeedMultiStrategyParser? _indeedParser;
    private GlassdoorMultiStrategyParser? _glassdoorParser;
    private GoogleJobsMultiStrategyParser? _googleParser;
    private ILogger<IndeedMultiStrategyParser>? _logger;

    private string _sampleIndeedHtml = string.Empty;
    private string _sampleGlassdoorHtml = string.Empty;
    private string _sampleGoogleHtml = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<IndeedMultiStrategyParser>();

        _indeedParser = new IndeedMultiStrategyParser(_logger);
        _glassdoorParser = new GlassdoorMultiStrategyParser(
            factory.CreateLogger<GlassdoorMultiStrategyParser>());
        _googleParser = new GoogleJobsMultiStrategyParser(
            factory.CreateLogger<GoogleJobsMultiStrategyParser>());

        _sampleIndeedHtml = CreateSampleIndeedHtml();
        _sampleGlassdoorHtml = CreateSampleGlassdoorHtml();
        _sampleGoogleHtml = CreateSampleGoogleHtml();
    }

    /// <summary>
    /// Benchmark: Parse Indeed job listings using multi-strategy approach.
    /// Measures the time to parse a typical Indeed HTML response.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task ParseIndeedWithMultiStrategy()
    {
        if (_indeedParser == null)
            throw new InvalidOperationException("Parser not initialized");

        var jobs = await _indeedParser.ParseHtmlAsync(_sampleIndeedHtml);
        if (jobs.Count == 0)
            throw new InvalidOperationException("Expected jobs to be parsed");
    }

    /// <summary>
    /// Benchmark: Parse Indeed with large HTML content.
    /// Measures performance with increased document size (5x).
    /// </summary>
    [Benchmark]
    public async Task ParseIndeedWithLargeHtml()
    {
        if (_indeedParser == null)
            throw new InvalidOperationException("Parser not initialized");

        var largeHtml = string.Concat(_sampleIndeedHtml, _sampleIndeedHtml, _sampleIndeedHtml,
            _sampleIndeedHtml, _sampleIndeedHtml);

        var jobs = await _indeedParser.ParseHtmlAsync(largeHtml);
        if (jobs.Count < 5)
            throw new InvalidOperationException("Expected multiple jobs from large HTML");
    }

    /// <summary>
    /// Benchmark: Parse Glassdoor job listings.
    /// Measures Glassdoor-specific parsing performance.
    /// </summary>
    [Benchmark]
    public async Task ParseGlassdoorWithMultiStrategy()
    {
        if (_glassdoorParser == null)
            throw new InvalidOperationException("Parser not initialized");

        var jobs = await _glassdoorParser.ParseHtmlAsync(_sampleGlassdoorHtml);
    }

    /// <summary>
    /// Benchmark: Parse Google Jobs listings.
    /// Measures Google Jobs-specific parsing performance.
    /// </summary>
    [Benchmark]
    public async Task ParseGoogleJobsWithMultiStrategy()
    {
        if (_googleParser == null)
            throw new InvalidOperationException("Parser not initialized");

        var jobs = await _googleParser.ParseHtmlAsync(_sampleGoogleHtml);
    }

    /// <summary>
    /// Creates sample Indeed HTML for benchmarking.
    /// Includes realistic job listing structure with multiple job entries.
    /// </summary>
    private static string CreateSampleIndeedHtml()
    {
        return @"
<!DOCTYPE html>
<html>
<head><title>Indeed Job Search</title></head>
<body>
<div class='job' data-jk='job123'>
    <h2 class='jobTitle'><span>Software Engineer</span></h2>
    <span class='companyName'>TechCorp Inc.</span>
    <div class='companyLocation'>San Francisco, CA</div>
    <div class='salary-snippet'>$120,000 - $150,000 per year</div>
    <div class='job-snippet'>We are looking for a talented software engineer...</div>
    <span class='date'>3 days ago</span>
</div>
<div class='job' data-jk='job124'>
    <h2 class='jobTitle'><span>Product Manager</span></h2>
    <span class='companyName'>StartupXYZ</span>
    <div class='companyLocation'>Remote</div>
    <div class='salary-snippet'>$100,000 - $130,000 per year</div>
    <div class='job-snippet'>Looking for an experienced product manager...</div>
    <span class='date'>1 day ago</span>
</div>
<div class='job' data-jk='job125'>
    <h2 class='jobTitle'><span>Data Scientist</span></h2>
    <span class='companyName'>DataDriven LLC</span>
    <div class='companyLocation'>New York, NY</div>
    <div class='salary-snippet'>$130,000 - $160,000 per year</div>
    <div class='job-snippet'>We need a data scientist with ML expertise...</div>
    <span class='date'>5 days ago</span>
</div>
</body>
</html>";
    }

    /// <summary>
    /// Creates sample Glassdoor HTML for benchmarking.
    /// </summary>
    private static string CreateSampleGlassdoorHtml()
    {
        return @"
<!DOCTYPE html>
<html>
<head><title>Glassdoor Jobs</title></head>
<body>
<div class='JobCard'>
    <h2 class='job-title'>Senior Developer</h2>
    <span class='employer-name'>Global Tech Corp</span>
    <span class='location'>Austin, TX</span>
    <span class='salary'>$150K - $180K</span>
    <div class='job-description'>Seeking senior developer with 5+ years experience...</div>
</div>
<div class='JobCard'>
    <h2 class='job-title'>UX Designer</h2>
    <span class='employer-name'>Creative Studio</span>
    <span class='location'>Los Angeles, CA</span>
    <div class='job-description'>Join our design team to create amazing user experiences...</div>
</div>
</body>
</html>";
    }

    /// <summary>
    /// Creates sample Google Jobs HTML for benchmarking.
    /// </summary>
    private static string CreateSampleGoogleHtml()
    {
        return @"
<!DOCTYPE html>
<html>
<head><title>Google Jobs</title></head>
<body>
<div class='job-result'>
    <h2><a href='#'>Systems Architect</a></h2>
    <div class='job-company'>Enterprise Solutions Inc</div>
    <div class='job-location'>Seattle, WA</div>
    <div class='job-description'>We are hiring a systems architect for our cloud division...</div>
</div>
<div class='job-result'>
    <h2><a href='#'>DevOps Engineer</a></h2>
    <div class='job-company'>Cloud Native Co</div>
    <div class='job-location'>Remote, Worldwide</div>
    <div class='job-description'>Help us build scalable cloud infrastructure...</div>
</div>
</body>
</html>";
    }
}

/// <summary>
/// Benchmarks for circuit breaker performance and overhead.
/// Measures the cost of circuit breaker wrapper around HTTP requests.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 3, iterationCount: 5)]
public class CircuitBreakerBenchmarks
{
    private JobScraperCircuitBreaker? _circuitBreaker;
    private ILogger<JobScraperCircuitBreaker>? _logger;
    private HttpClient? _httpClient;

    [GlobalSetup]
    public void Setup()
    {
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<JobScraperCircuitBreaker>();

        _circuitBreaker = new JobScraperCircuitBreaker(_logger);

        _circuitBreaker.RegisterPlatform(new PlatformCircuitBreakerConfig
        {
            PlatformName = "Indeed",
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30)
        });

        _circuitBreaker.RegisterPlatform(new PlatformCircuitBreakerConfig
        {
            PlatformName = "Glassdoor",
            FailureThreshold = 5,
            OpenDuration = TimeSpan.FromSeconds(30)
        });

        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Benchmark: Execute HTTP request through circuit breaker (closed state).
    /// Measures the overhead of circuit breaker wrapper when circuit is operational.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task CircuitBreakerHttpRequestOverhead_Closed()
    {
        if (_circuitBreaker == null || _httpClient == null)
            throw new InvalidOperationException("Circuit breaker not initialized");

        var response = await _circuitBreaker.ExecuteHttpRequestAsync(
            "Indeed",
            async () =>
            {
                return await Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent("Success")
                });
            });

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Expected success response");
    }

    /// <summary>
    /// Benchmark: State transition from Closed to Open.
    /// Measures the performance cost of circuit breaker state transitions.
    /// </summary>
    [Benchmark]
    public void CircuitBreakerStateTransition_ClosedToOpen()
    {
        if (_circuitBreaker == null)
            throw new InvalidOperationException("Circuit breaker not initialized");

        var platform = "Indeed";

        _circuitBreaker.ManuallyOpenCircuit(platform, "Benchmark test");

        var state = _circuitBreaker.GetState(platform);
        if (state != CircuitBreakerState.Open)
            throw new InvalidOperationException("Expected Open state");

        _circuitBreaker.ResetCircuit(platform);
    }

    /// <summary>
    /// Benchmark: State transition from Open to Half-Open to Closed.
    /// Measures recovery path performance.
    /// </summary>
    [Benchmark]
    public void CircuitBreakerStateTransition_RecoveryPath()
    {
        if (_circuitBreaker == null)
            throw new InvalidOperationException("Circuit breaker not initialized");

        var platform = "Glassdoor";

        _circuitBreaker.ManuallyOpenCircuit(platform, "Test");

        _circuitBreaker.ResetCircuit(platform);

        var state = _circuitBreaker.GetState(platform);
        if (state != CircuitBreakerState.Closed)
            throw new InvalidOperationException("Expected Closed state after reset");
    }

    /// <summary>
    /// Benchmark: Get metrics snapshot.
    /// Measures the performance cost of metrics collection and aggregation.
    /// </summary>
    [Benchmark]
    public void CircuitBreakerMetricsCollection()
    {
        if (_circuitBreaker == null)
            throw new InvalidOperationException("Circuit breaker not initialized");

        var metrics = _circuitBreaker.GetAllMetrics();

        if (metrics.Count < 2)
            throw new InvalidOperationException("Expected metrics for registered platforms");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _httpClient?.Dispose();
        _circuitBreaker?.Dispose();
    }
}

/// <summary>
/// Benchmarks for monitoring service performance.
/// Measures request recording, health checks, and metrics aggregation throughput.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 3, warmupCount: 3, iterationCount: 5)]
public class MonitoringBenchmarks
{
    private JobScraperMonitoringService? _monitoringService;
    private ILogger<JobScraperMonitoringService>? _logger;

    [GlobalSetup]
    public void Setup()
    {
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<JobScraperMonitoringService>();

        _monitoringService = new JobScraperMonitoringService(_logger);

        for (int i = 0; i < 100; i++)
        {
            _monitoringService.RecordRequest("Indeed", i % 5 != 0, 50 + i);
            _monitoringService.RecordRequest("Glassdoor", i % 3 != 0, 75 + i);
            _monitoringService.RecordRequest("Google", i % 4 != 0, 100 + i);
        }
    }

    /// <summary>
    /// Benchmark: Record request for single platform.
    /// Measures the throughput of request recording (requests/second).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void RecordRequest_SinglePlatform()
    {
        if (_monitoringService == null)
            throw new InvalidOperationException("Monitoring service not initialized");

        _monitoringService.RecordRequest("Indeed", success: true, latencyMs: 75, errorCategory: null);
    }

    /// <summary>
    /// Benchmark: Record failed request with error category.
    /// Measures recording overhead when error tracking is involved.
    /// </summary>
    [Benchmark]
    public void RecordRequest_WithErrorCategory()
    {
        if (_monitoringService == null)
            throw new InvalidOperationException("Monitoring service not initialized");

        _monitoringService.RecordRequest("Indeed", success: false, latencyMs: 150, errorCategory: "NetworkError");
    }

    /// <summary>
    /// Benchmark: Get platform health status.
    /// Measures the latency of health status calculation and retrieval.
    /// </summary>
    [Benchmark]
    public void GetPlatformHealth_SinglePlatform()
    {
        if (_monitoringService == null)
            throw new InvalidOperationException("Monitoring service not initialized");

        var health = _monitoringService.GetPlatformHealth("Indeed");

        if (health?.Status == null)
            throw new InvalidOperationException("Expected health status");
    }

    /// <summary>
    /// Benchmark: Get health for all platforms.
    /// Measures aggregation performance across multiple platforms.
    /// </summary>
    [Benchmark]
    public void GetAllPlatformHealth_Aggregation()
    {
        if (_monitoringService == null)
            throw new InvalidOperationException("Monitoring service not initialized");

        var allHealth = _monitoringService.GetAllPlatformHealth();

        if (allHealth.Count < 3)
            throw new InvalidOperationException("Expected health data for all platforms");
    }

    /// <summary>
    /// Benchmark: Get current metrics snapshot.
    /// Measures the performance cost of metrics aggregation.
    /// </summary>
    [Benchmark]
    public void GetCurrentMetrics_Aggregation()
    {
        if (_monitoringService == null)
            throw new InvalidOperationException("Monitoring service not initialized");

        var metrics = _monitoringService.GetCurrentMetrics();

        if (metrics.PerPlatformMetrics.Count < 3)
            throw new InvalidOperationException("Expected metrics for all platforms");
    }

    /// <summary>
    /// Benchmark: Check alert threshold.
    /// Measures the overhead of alert threshold evaluation.
    /// </summary>
    [Benchmark]
    public void ShouldAlert_ThresholdCheck()
    {
        if (_monitoringService == null)
            throw new InvalidOperationException("Monitoring service not initialized");

        var shouldAlert = _monitoringService.ShouldAlert("Indeed");
    }

    /// <summary>
    /// Benchmark: Batch operations - simulate continuous monitoring.
    /// Records 10 requests and gets health for all platforms.
    /// </summary>
    [Benchmark]
    public void BatchMonitoring_RecordAndCheck()
    {
        if (_monitoringService == null)
            throw new InvalidOperationException("Monitoring service not initialized");

        for (int i = 0; i < 10; i++)
        {
            _monitoringService.RecordRequest("Indeed", i % 2 == 0, 50 + i);
            _monitoringService.RecordRequest("Glassdoor", i % 3 == 0, 75 + i);
        }

        var health = _monitoringService.GetAllPlatformHealth();
        var metrics = _monitoringService.GetCurrentMetrics();

        if (health.Count < 3 || metrics.PerPlatformMetrics.Count < 3)
            throw new InvalidOperationException("Expected complete monitoring data");
    }
}
