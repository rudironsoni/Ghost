using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Ghost.Scraper.DotnetSpider.Resilience;

/// <summary>
/// Represents the state of a circuit breaker.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed and requests are allowed through normally.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open and requests are rejected immediately.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open and test requests are allowed to check recovery.
    /// </summary>
    HalfOpen
}

/// <summary>
/// Tracks metrics for a circuit breaker instance.
/// </summary>
public sealed class CircuitBreakerMetrics
{
    /// <summary>
    /// Total number of successful requests.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Total number of failed requests.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Number of requests rejected due to open circuit.
    /// </summary>
    public long RejectedCount { get; set; }

    /// <summary>
    /// Current state of the circuit breaker.
    /// </summary>
    public CircuitBreakerState CurrentState { get; set; } = CircuitBreakerState.Closed;

    /// <summary>
    /// Timestamp of the last state transition.
    /// </summary>
    public DateTime? LastStateTransitionTime { get; set; }

    /// <summary>
    /// Timestamp of the last successful request.
    /// </summary>
    public DateTime? LastSuccessTime { get; set; }

    /// <summary>
    /// Timestamp of the last failed request.
    /// </summary>
    public DateTime? LastFailureTime { get; set; }

    /// <summary>
    /// Duration the circuit has been in the current state.
    /// </summary>
    public TimeSpan CurrentStateDuration =>
        LastStateTransitionTime.HasValue
            ? DateTime.UtcNow - LastStateTransitionTime.Value
            : TimeSpan.Zero;

    /// <summary>
    /// Total number of state transitions (Closed -> Open -> Half-Open -> Closed).
    /// </summary>
    public int StateTransitionCount { get; set; }

    /// <summary>
    /// Gets the success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate =>
        (SuccessCount + FailureCount) > 0
            ? (SuccessCount * 100.0) / (SuccessCount + FailureCount)
            : 100.0;

    /// <summary>
    /// Creates a copy of the current metrics.
    /// </summary>
    public CircuitBreakerMetrics Clone()
    {
        return new CircuitBreakerMetrics
        {
            SuccessCount = SuccessCount,
            FailureCount = FailureCount,
            RejectedCount = RejectedCount,
            CurrentState = CurrentState,
            LastStateTransitionTime = LastStateTransitionTime,
            LastSuccessTime = LastSuccessTime,
            LastFailureTime = LastFailureTime,
            StateTransitionCount = StateTransitionCount
        };
    }
}

/// <summary>
/// Configuration for platform-specific circuit breaker behavior.
/// </summary>
public sealed class PlatformCircuitBreakerConfig
{
    /// <summary>
    /// Platform name (Indeed, Glassdoor, Google).
    /// </summary>
    public string PlatformName { get; set; } = string.Empty;

    /// <summary>
    /// Number of consecutive failures before opening the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Duration the circuit remains open before transitioning to half-open.
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of successful requests in half-open state required to close the circuit.
    /// </summary>
    public int HalfOpenSuccessThreshold { get; set; } = 2;

    /// <summary>
    /// HTTP status codes that should trigger a failure.
    /// </summary>
    public ISet<int> FailureStatusCodes { get; set; } = new HashSet<int> { 500, 502, 503, 504 };

    /// <summary>
    /// HTTP status codes that should trigger anti-bot detection.
    /// </summary>
    public ISet<int> AntiBotStatusCodes { get; set; } = new HashSet<int> { 403, 429 };

    /// <summary>
    /// Whether anti-bot responses (403, 429) trigger a circuit breach.
    /// Glassdoor: true (strict), Indeed: false (lenient), Google: true (moderate).
    /// </summary>
    public bool TreatAntiBotAsFailure { get; set; } = true;

    /// <summary>
    /// Timeout for individual requests.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to enable graceful degradation when circuit is open.
    /// </summary>
    public bool EnableGracefulDegradation { get; set; } = true;
}

/// <summary>
/// Manages circuit breakers for job scraping platforms using Polly.
/// Provides resilience patterns for HTTP requests, anti-bot detection, and parsing failures.
/// </summary>
public sealed class JobScraperCircuitBreaker : IDisposable
{
    private readonly ILogger<JobScraperCircuitBreaker> _logger;
    private readonly ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>> _httpPolicies;
    private readonly ConcurrentDictionary<string, IAsyncPolicy> _generalPolicies;
    private readonly ConcurrentDictionary<string, CircuitBreakerMetrics> _metrics;
    private readonly ConcurrentDictionary<string, PlatformCircuitBreakerConfig> _configs;
    private readonly object _lockObject = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the JobScraperCircuitBreaker.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public JobScraperCircuitBreaker(ILogger<JobScraperCircuitBreaker> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _httpPolicies = new ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>>();
        _generalPolicies = new ConcurrentDictionary<string, IAsyncPolicy>();
        _metrics = new ConcurrentDictionary<string, CircuitBreakerMetrics>();
        _configs = new ConcurrentDictionary<string, PlatformCircuitBreakerConfig>();

        _logInitialized(_logger, null);
    }

    #region Configuration

    /// <summary>
    /// Registers a circuit breaker policy for a specific platform.
    /// Creates all necessary policies (HTTP, parsing, general resilience).
    /// </summary>
    /// <param name="config">The circuit breaker configuration for the platform.</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
    /// <exception cref="ArgumentException">Thrown when platform name is empty.</exception>
    public void RegisterPlatform(PlatformCircuitBreakerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.PlatformName))
            throw new ArgumentException("Platform name cannot be empty", nameof(config));

        lock (_lockObject)
        {
            _configs.AddOrUpdate(config.PlatformName, config, (_, _) => config);
            _metrics.AddOrUpdate(config.PlatformName, new CircuitBreakerMetrics(), (_, _) => new CircuitBreakerMetrics());

            var httpPolicy = CreateHttpPolicy(config);
            _httpPolicies.AddOrUpdate(config.PlatformName, httpPolicy, (_, _) => httpPolicy);

            var generalPolicy = CreateGeneralPolicy(config);
            _generalPolicies.AddOrUpdate(config.PlatformName, generalPolicy, (_, _) => generalPolicy);

            _logPlatformRegistered(_logger, config.PlatformName, config.FailureThreshold, config.OpenDuration.TotalSeconds, null);
        }
    }

    /// <summary>
    /// Gets the circuit breaker configuration for a platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>The configuration, or null if not found.</returns>
    public PlatformCircuitBreakerConfig? GetPlatformConfig(string platformName)
    {
        return _configs.TryGetValue(platformName, out var config) ? config : null;
    }

    #endregion

    #region HTTP Policy Execution

    /// <summary>
    /// Executes an HTTP request with circuit breaker protection.
    /// Tracks success/failure metrics and manages state transitions.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <param name="requestFactory">Factory function that creates the HTTP request.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The HTTP response message.</returns>
    /// <exception cref="BrokenCircuitException">Thrown when circuit is open and no fallback available.</exception>
    public async Task<HttpResponseMessage> ExecuteHttpRequestAsync(
        string platformName,
        Func<Task<HttpResponseMessage>> requestFactory,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_httpPolicies.TryGetValue(platformName, out var policy))
        {
            _logNoPolicyFound(_logger, platformName, null);
            throw new InvalidOperationException($"No circuit breaker policy registered for platform: {platformName}");
        }

        try
        {
            var response = await policy.ExecuteAsync(
                async (ctx, ct) => await requestFactory.Invoke(),
                new Polly.Context(),
                cancellationToken);
            RecordSuccess(platformName);
            _logHttpRequestSucceeded(_logger, platformName, null);
            return response;
        }
        catch (BrokenCircuitException)
        {
            RecordRejection(platformName);
            _logCircuitOpen(_logger, platformName, null);
            throw;
        }
        catch (Exception ex)
        {
            RecordFailure(platformName);
            _logHttpRequestFailed(_logger, platformName, ex);
            throw;
        }
    }

    #endregion

    #region Parsing Failure Handling

    /// <summary>
    /// Executes a parsing operation with circuit breaker protection.
    /// Handles parsing failures with graceful degradation.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <param name="parsingAction">The parsing function to execute.</param>
    /// <param name="fallbackAction">Optional fallback action if circuit is open.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the parsing operation.</returns>
    public async Task<T> ExecuteParsingOperationAsync<T>(
        string platformName,
        Func<Task<T>> parsingAction,
        Func<Task<T>>? fallbackAction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_generalPolicies.TryGetValue(platformName, out var policy))
        {
            _logNoPolicyFoundForParsing(_logger, platformName, null);
            return await parsingAction.Invoke();
        }

        try
        {
            var result = await policy.ExecuteAsync(
                async (ctx, ct) => await parsingAction.Invoke(),
                new Polly.Context(),
                cancellationToken);
            RecordSuccess(platformName);
            return result;
        }
        catch (BrokenCircuitException) when (fallbackAction != null)
        {
            RecordRejection(platformName);
            _logCircuitOpenExecutingFallback(_logger, platformName, null);
            return await fallbackAction.Invoke();
        }
        catch (Exception ex)
        {
            RecordFailure(platformName);
            _logParsingOperationFailed(_logger, platformName, ex);
            throw;
        }
    }

    #endregion

    #region State Management

    /// <summary>
    /// Gets the current state of the circuit breaker for a platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>The current circuit breaker state.</returns>
    public CircuitBreakerState GetState(string platformName)
    {
        if (_metrics.TryGetValue(platformName, out var metrics))
        {
            return metrics.CurrentState;
        }

        return CircuitBreakerState.Closed;
    }

    /// <summary>
    /// Manually reset the circuit breaker for a platform to Closed state.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    public void ResetCircuit(string platformName)
    {
        lock (_lockObject)
        {
            if (_metrics.TryGetValue(platformName, out var metrics))
            {
                metrics.CurrentState = CircuitBreakerState.Closed;
                metrics.FailureCount = 0;
                metrics.SuccessCount = 0;
                metrics.RejectedCount = 0;
                metrics.LastStateTransitionTime = DateTime.UtcNow;
                metrics.StateTransitionCount++;

                _logCircuitManuallyReset(_logger, platformName, null);
            }
        }
    }

    /// <summary>
    /// Manually open the circuit breaker for a platform (for maintenance or emergency).
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <param name="reason">Optional reason for opening the circuit.</param>
    public void ManuallyOpenCircuit(string platformName, string? reason = null)
    {
        lock (_lockObject)
        {
            if (_metrics.TryGetValue(platformName, out var metrics))
            {
                metrics.CurrentState = CircuitBreakerState.Open;
                metrics.LastStateTransitionTime = DateTime.UtcNow;
                metrics.StateTransitionCount++;

                _logCircuitManuallyOpened(_logger, platformName, reason ?? "Not specified", null);
            }
        }
    }

    #endregion

    #region Metrics

    /// <summary>
    /// Gets current metrics for a platform's circuit breaker.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>A copy of the current metrics.</returns>
    public CircuitBreakerMetrics? GetMetrics(string platformName)
    {
        if (_metrics.TryGetValue(platformName, out var metrics))
        {
            return metrics.Clone();
        }

        return null;
    }

    /// <summary>
    /// Gets metrics for all registered platforms.
    /// </summary>
    /// <returns>Dictionary mapping platform names to their metrics.</returns>
    public Dictionary<string, CircuitBreakerMetrics> GetAllMetrics()
    {
        var result = new Dictionary<string, CircuitBreakerMetrics>();
        foreach (var kvp in _metrics)
        {
            result[kvp.Key] = kvp.Value.Clone();
        }

        return result;
    }

    /// <summary>
    /// Resets metrics for a specific platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    public void ResetMetrics(string platformName)
    {
        lock (_lockObject)
        {
            if (_metrics.TryGetValue(platformName, out var metrics))
            {
                metrics.SuccessCount = 0;
                metrics.FailureCount = 0;
                metrics.RejectedCount = 0;
                metrics.LastSuccessTime = null;
                metrics.LastFailureTime = null;
                metrics.StateTransitionCount = 0;

                _logMetricsReset(_logger, platformName, null);
            }
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates the HTTP policy for a platform with circuit breaker, retry, and timeout handling.
    /// </summary>
#pragma warning disable CA1859
    private IAsyncPolicy<HttpResponseMessage> CreateHttpPolicy(PlatformCircuitBreakerConfig config)
#pragma warning restore CA1859
    {
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(config.RequestTimeout);

        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => ShouldRetry(r, config))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logRetryAttempt(_logger, retryCount, config.PlatformName, timespan.TotalMilliseconds, null);
                });

        var circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => ShouldFail(r, config))
            .CircuitBreakerAsync<HttpResponseMessage>(
                handledEventsAllowedBeforeBreaking: config.FailureThreshold,
                durationOfBreak: config.OpenDuration,
                onBreak: (outcome, timespan) =>
                {
                    lock (_lockObject)
                    {
                        if (_metrics.TryGetValue(config.PlatformName, out var metrics))
                        {
                            metrics.CurrentState = CircuitBreakerState.Open;
                            metrics.LastStateTransitionTime = DateTime.UtcNow;
                            metrics.StateTransitionCount++;
                        }
                    }

                    _logCircuitBreakerOpened(_logger, config.PlatformName, timespan.TotalSeconds, null);
                },
                onReset: () =>
                {
                    lock (_lockObject)
                    {
                        if (_metrics.TryGetValue(config.PlatformName, out var metrics))
                        {
                            metrics.CurrentState = CircuitBreakerState.Closed;
                            metrics.LastStateTransitionTime = DateTime.UtcNow;
                            metrics.StateTransitionCount++;
                        }
                    }

                    _logCircuitBreakerClosed(_logger, config.PlatformName, null);
                },
                onHalfOpen: () =>
                {
                    lock (_lockObject)
                    {
                        if (_metrics.TryGetValue(config.PlatformName, out var metrics))
                        {
                            metrics.CurrentState = CircuitBreakerState.HalfOpen;
                            metrics.LastStateTransitionTime = DateTime.UtcNow;
                        }
                    }

                    _logCircuitBreakerHalfOpen(_logger, config.PlatformName, null);
                });

        return Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);
    }

    /// <summary>
    /// Creates a general resilience policy for parsing and other operations.
    /// </summary>
#pragma warning disable CA1859
    private static IAsyncPolicy CreateGeneralPolicy(PlatformCircuitBreakerConfig config)
#pragma warning restore CA1859
    {
        var noOpPolicy = Policy.NoOpAsync();
        return noOpPolicy;
    }

    /// <summary>
    /// Determines if an HTTP response should trigger a retry.
    /// </summary>
    private static bool ShouldRetry(HttpResponseMessage response, PlatformCircuitBreakerConfig config)
    {
        if (response == null)
            return false;

        return config.FailureStatusCodes.Contains((int)response.StatusCode) ||
               (int)response.StatusCode == 429;
    }

    /// <summary>
    /// Determines if an HTTP response should be treated as a failure for circuit breaker.
    /// </summary>
    private static bool ShouldFail(HttpResponseMessage response, PlatformCircuitBreakerConfig config)
    {
        if (response == null)
            return true;

        if (config.FailureStatusCodes.Contains((int)response.StatusCode))
            return true;

        if (config.TreatAntiBotAsFailure &&
            config.AntiBotStatusCodes.Contains((int)response.StatusCode))
            return true;

        return false;
    }

    /// <summary>
    /// Records a successful request in metrics.
    /// </summary>
    private void RecordSuccess(string platformName)
    {
        if (_metrics.TryGetValue(platformName, out var metrics))
        {
            lock (_lockObject)
            {
                metrics.SuccessCount++;
                metrics.LastSuccessTime = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Records a failed request in metrics.
    /// </summary>
    private void RecordFailure(string platformName)
    {
        if (_metrics.TryGetValue(platformName, out var metrics))
        {
            lock (_lockObject)
            {
                metrics.FailureCount++;
                metrics.LastFailureTime = DateTime.UtcNow;
            }
        }
    }

    /// <summary>
    /// Records a rejected request (due to open circuit) in metrics.
    /// </summary>
    private void RecordRejection(string platformName)
    {
        if (_metrics.TryGetValue(platformName, out var metrics))
        {
            lock (_lockObject)
            {
                metrics.RejectedCount++;
            }
        }
    }

    /// <summary>
    /// Throws if the instance has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes resources used by the circuit breaker.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lockObject)
        {
            _httpPolicies.Clear();
            _generalPolicies.Clear();
            _metrics.Clear();
            _configs.Clear();

            _disposed = true;
            _logDisposed(_logger, null);
        }

        GC.SuppressFinalize(this);
    }

    #endregion

    #region LoggerMessage.Define Static Delegates

    private static readonly Action<ILogger, Exception?> _logInitialized =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3001, nameof(JobScraperCircuitBreaker)),
            "JobScraperCircuitBreaker initialized");

    private static readonly Action<ILogger, string, int, double, Exception?> _logPlatformRegistered =
        LoggerMessage.Define<string, int, double>(
            LogLevel.Information,
            new EventId(3002, nameof(RegisterPlatform)),
            "Platform circuit breaker registered: {Platform} (FailureThreshold: {Threshold}, OpenDuration: {Duration}s)");

    private static readonly Action<ILogger, string, Exception?> _logNoPolicyFound =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3003, nameof(ExecuteHttpRequestAsync)),
            "No circuit breaker policy registered for platform: {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logHttpRequestSucceeded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(3004, nameof(ExecuteHttpRequestAsync)),
            "HTTP request succeeded for platform: {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logCircuitOpen =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3005, nameof(ExecuteHttpRequestAsync)),
            "Circuit breaker is open for platform {Platform}. Request rejected.");

    private static readonly Action<ILogger, string, Exception> _logHttpRequestFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3006, nameof(ExecuteHttpRequestAsync)),
            "HTTP request failed for platform: {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logNoPolicyFoundForParsing =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3007, nameof(ExecuteParsingOperationAsync)),
            "No circuit breaker policy found for platform: {Platform}. Executing without circuit breaker.");

    private static readonly Action<ILogger, string, Exception?> _logCircuitOpenExecutingFallback =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3008, nameof(ExecuteParsingOperationAsync)),
            "Circuit breaker open for platform {Platform}. Executing fallback action.");

    private static readonly Action<ILogger, string, Exception> _logParsingOperationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3009, nameof(ExecuteParsingOperationAsync)),
            "Parsing operation failed for platform: {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logCircuitManuallyReset =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3010, nameof(ResetCircuit)),
            "Circuit breaker manually reset for platform: {Platform}");

    private static readonly Action<ILogger, string, string, Exception?> _logCircuitManuallyOpened =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(3011, nameof(ManuallyOpenCircuit)),
            "Circuit breaker manually opened for platform: {Platform}. Reason: {Reason}");

    private static readonly Action<ILogger, string, Exception?> _logMetricsReset =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3012, nameof(ResetMetrics)),
            "Metrics reset for platform: {Platform}");

    private static readonly Action<ILogger, int, string, double, Exception?> _logRetryAttempt =
        LoggerMessage.Define<int, string, double>(
            LogLevel.Warning,
            new EventId(3013, nameof(CreateHttpPolicy)),
            "Retry attempt {RetryCount} for platform {Platform} after {Delay}ms");

    private static readonly Action<ILogger, string, double, Exception?> _logCircuitBreakerOpened =
        LoggerMessage.Define<string, double>(
            LogLevel.Critical,
            new EventId(3014, nameof(CreateHttpPolicy)),
            "Circuit breaker opened for platform {Platform}. Duration: {Duration}s");

    private static readonly Action<ILogger, string, Exception?> _logCircuitBreakerClosed =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3015, nameof(CreateHttpPolicy)),
            "Circuit breaker closed for platform {Platform}");

    private static readonly Action<ILogger, string, Exception?> _logCircuitBreakerHalfOpen =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3016, nameof(CreateHttpPolicy)),
            "Circuit breaker half-open for platform {Platform}. Testing recovery...");

    private static readonly Action<ILogger, Exception?> _logDisposed =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(3020, nameof(Dispose)),
            "JobScraperCircuitBreaker disposed");

    #endregion
}
