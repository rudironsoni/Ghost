using Microsoft.Extensions.Logging;

namespace Ghost.Captcha;

/// <summary>
/// Orchestrates CAPTCHA solving across multiple providers with fallback chain
/// </summary>
public sealed class CaptchaService
{
    private readonly ILogger<CaptchaService> _logger;
    private readonly IEnumerable<ICaptchaProvider> _providers;

    public CaptchaService(
        ILogger<CaptchaService> logger,
        IEnumerable<ICaptchaProvider> providers)
    {
        _logger = logger;
        _providers = providers;
        Metrics = new CaptchaMetrics();
    }

    /// <summary>
    /// Gets current metrics for CAPTCHA solving success rates
    /// </summary>
    public CaptchaMetrics Metrics { get; }

    // LoggerMessage delegates for performance
    private static readonly Action<ILogger, CaptchaType, int, Exception?> _logAttemptingSolve =
        LoggerMessage.Define<CaptchaType, int>(LogLevel.Information, new EventId(1, "AttemptingSolve"), "Attempting to solve {CaptchaType} CAPTCHA with {ProviderCount} providers");

    private static readonly Action<ILogger, string, CaptchaType, Exception?> _logSkippingProvider =
        LoggerMessage.Define<string, CaptchaType>(LogLevel.Debug, new EventId(2, "SkippingProvider"), "Skipping {Provider} - does not support {CaptchaType}");

    private static readonly Action<ILogger, string, Exception?> _logProviderNotAvailable =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3, "ProviderNotAvailable"), "{Provider} is not available, trying next provider");

    private static readonly Action<ILogger, string, CaptchaType, Exception?> _logTryingProvider =
        LoggerMessage.Define<string, CaptchaType>(LogLevel.Information, new EventId(4, "TryingProvider"), "Trying {Provider} for {CaptchaType}");

    private static readonly Action<ILogger, string, CaptchaType, Exception?> _logProviderSuccess =
        LoggerMessage.Define<string, CaptchaType>(LogLevel.Information, new EventId(5, "ProviderSuccess"), "{Provider} successfully solved {CaptchaType} CAPTCHA");

    private static readonly Action<ILogger, string, CaptchaType, string, Exception?> _logProviderFailure =
        LoggerMessage.Define<string, CaptchaType, string>(LogLevel.Warning, new EventId(6, "ProviderFailure"), "{Provider} failed to solve {CaptchaType} CAPTCHA: {Message}");

    private static readonly Action<ILogger, int, CaptchaType, Exception?> _logAllProvidersFailed =
        LoggerMessage.Define<int, CaptchaType>(LogLevel.Error, new EventId(7, "AllProvidersFailed"), "All {ProviderCount} providers failed to solve {CaptchaType} CAPTCHA");

    /// <summary>
    /// Solves a CAPTCHA challenge using the fallback chain:
    /// 1. Try NopeCHA (fastest, browser-based)
    /// 2. Try TensorFlow (self-hosted, slower)
    /// 3. Throw exception if all fail
    /// </summary>
    public async Task<string> SolveAsync(
        ICaptchaChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        _logAttemptingSolve(_logger, challenge.Type, _providers.Count(), null);

        var errors = new List<Exception>();

        foreach (ICaptchaProvider provider in _providers)
        {
            if (!provider.CanSolve(challenge.Type))
            {
                _logSkippingProvider(_logger, provider.Name, challenge.Type, null);
                continue;
            }

            if (!await provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
            {
                _logProviderNotAvailable(_logger, provider.Name, null);
                continue;
            }

            try
            {
                _logTryingProvider(_logger, provider.Name, challenge.Type, null);

                string solution = await provider.SolveAsync(challenge, cancellationToken).ConfigureAwait(false);

                Metrics.RecordSuccess(provider.Name, challenge.Type);

                _logProviderSuccess(_logger, provider.Name, challenge.Type, null);

                return solution;
            }
            catch (Exception ex)
            {
                Metrics.RecordFailure(provider.Name, challenge.Type);

                _logProviderFailure(_logger, provider.Name, challenge.Type, ex.Message, ex);

                errors.Add(ex);
            }
        }

        // All providers failed
        _logAllProvidersFailed(_logger, _providers.Count(), challenge.Type, null);

        throw new AggregateException(
            $"Failed to solve {challenge.Type} CAPTCHA with {_providers.Count()} providers",
            errors);
    }

    /// <summary>
    /// Gets the list of available providers
    /// </summary>
    public IEnumerable<string> GetAvailableProviders()
    {
        return _providers.Select(p => p.Name);
    }
}

/// <summary>
/// Tracks CAPTCHA solving metrics
/// </summary>
public sealed class CaptchaMetrics
{
    private readonly Dictionary<string, ProviderMetrics> _providerMetrics = new();
    private readonly object _lock = new();

    public void RecordSuccess(string providerName, CaptchaType type)
    {
        lock (_lock)
        {
            if (!_providerMetrics.TryGetValue(providerName, out ProviderMetrics? metrics))
            {
                metrics = new ProviderMetrics();
                _providerMetrics[providerName] = metrics;
            }
            metrics.Successes++;
        }
    }

    public void RecordFailure(string providerName, CaptchaType type)
    {
        lock (_lock)
        {
            if (!_providerMetrics.TryGetValue(providerName, out ProviderMetrics? metrics))
            {
                metrics = new ProviderMetrics();
                _providerMetrics[providerName] = metrics;
            }
            metrics.Failures++;
        }
    }

    public double GetSuccessRate(string providerName)
    {
        lock (_lock)
        {
            if (!_providerMetrics.TryGetValue(providerName, out ProviderMetrics? metrics))
            {
                return 0.0;
            }

            int total = metrics.Successes + metrics.Failures;
            return total == 0 ? 0.0 : (double)metrics.Successes / total;
        }
    }

    public IReadOnlyDictionary<string, (int Successes, int Failures, double Rate)> GetAllMetrics()
    {
        lock (_lock)
        {
            return _providerMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => (
                    kvp.Value.Successes,
                    kvp.Value.Failures,
                    GetSuccessRate(kvp.Key)
                ));
        }
    }

    private sealed class ProviderMetrics
    {
        public int Successes { get; set; }
        public int Failures { get; set; }
    }
}
