using Microsoft.Extensions.Logging;

namespace Ghost.Captcha;

/// <summary>
/// Orchestrates CAPTCHA solving across multiple providers with fallback chain
/// </summary>
public sealed class CaptchaService
{
    private readonly ILogger<CaptchaService> _logger;
    private readonly IEnumerable<ICaptchaProvider> _providers;
    private readonly CaptchaMetrics _metrics;

    public CaptchaService(
        ILogger<CaptchaService> logger,
        IEnumerable<ICaptchaProvider> providers)
    {
        _logger = logger;
        _providers = providers;
        _metrics = new CaptchaMetrics();
    }

    /// <summary>
    /// Gets current metrics for CAPTCHA solving success rates
    /// </summary>
    public CaptchaMetrics Metrics => _metrics;

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

        _logger.LogInformation(
            "Attempting to solve {CaptchaType} CAPTCHA with {ProviderCount} providers",
            challenge.Type,
            _providers.Count());

        var errors = new List<Exception>();

        foreach (var provider in _providers)
        {
            if (!provider.CanSolve(challenge.Type))
            {
                _logger.LogDebug(
                    "Skipping {Provider} - does not support {CaptchaType}",
                    provider.Name,
                    challenge.Type);
                continue;
            }

            if (!await provider.IsAvailableAsync(cancellationToken))
            {
                _logger.LogWarning(
                    "{Provider} is not available, trying next provider",
                    provider.Name);
                continue;
            }

            try
            {
                _logger.LogInformation("Trying {Provider} for {CaptchaType}", provider.Name, challenge.Type);

                var solution = await provider.SolveAsync(challenge, cancellationToken);

                _metrics.RecordSuccess(provider.Name, challenge.Type);

                _logger.LogInformation(
                    "{Provider} successfully solved {CaptchaType} CAPTCHA",
                    provider.Name,
                    challenge.Type);

                return solution;
            }
            catch (Exception ex)
            {
                _metrics.RecordFailure(provider.Name, challenge.Type);

                _logger.LogWarning(
                    ex,
                    "{Provider} failed to solve {CaptchaType} CAPTCHA: {Message}",
                    provider.Name,
                    challenge.Type,
                    ex.Message);

                errors.Add(ex);
            }
        }

        // All providers failed
        _logger.LogError(
            "All {ProviderCount} providers failed to solve {CaptchaType} CAPTCHA",
            _providers.Count(),
            challenge.Type);

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
            if (!_providerMetrics.ContainsKey(providerName))
            {
                _providerMetrics[providerName] = new ProviderMetrics();
            }
            _providerMetrics[providerName].Successes++;
        }
    }

    public void RecordFailure(string providerName, CaptchaType type)
    {
        lock (_lock)
        {
            if (!_providerMetrics.ContainsKey(providerName))
            {
                _providerMetrics[providerName] = new ProviderMetrics();
            }
            _providerMetrics[providerName].Failures++;
        }
    }

    public double GetSuccessRate(string providerName)
    {
        lock (_lock)
        {
            if (!_providerMetrics.TryGetValue(providerName, out var metrics))
            {
                return 0.0;
            }

            var total = metrics.Successes + metrics.Failures;
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
