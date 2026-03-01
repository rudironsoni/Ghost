using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghost.Proxy;

public sealed class ProxyManager : IProxyManager, IDisposable
{
    // LoggerMessage delegates (EventIds 1100-1109)
    private static readonly Action<ILogger, string, Exception?> _healthCheckFailedForProvider = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1100, nameof(ProxyManager)),
        "Health check failed for provider {Provider}");
    private readonly ProxyConfiguration _config;
    private readonly ILogger<ProxyManager> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, ProviderEntry> _providers = new();
    private readonly ConcurrentDictionary<string, ProxyHealthStatus> _healthStatus = new();
    private readonly Timer? _healthCheckTimer;
    private int _roundRobinIndex;

    public ProxyManager(IOptions<ProxyConfiguration> config, ILogger<ProxyManager>? logger = null, TimeProvider? timeProvider = null)
    {
        _config = config.Value ?? new ProxyConfiguration();
        _logger = logger ?? NullLogger<ProxyManager>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;

        if (_config.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(
                HealthCheckCallback,
                null,
                _config.HealthCheckInterval,
                _config.HealthCheckInterval);
        }
    }

    public Task<ProxyInfo?> GetProxyAsync(string? countryCode = null, CancellationToken ct = default)
    {
        if (!_config.Enabled || _providers.IsEmpty)
        {
            return Task.FromResult<ProxyInfo?>(null);
        }

        List<ProviderEntry> providers = GetEligibleProviders(countryCode);
        if (providers.Count == 0)
        {
            return Task.FromResult<ProxyInfo?>(null);
        }

        IProxyProvider? provider = SelectProvider(providers);
        if (provider == null)
        {
            return Task.FromResult<ProxyInfo?>(null);
        }

        return provider.GetProxyAsync(countryCode ?? "US", ct);
    }

    public Task<IReadOnlyList<ProxyHealthStatus>> GetHealthStatusAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ProxyHealthStatus>>(_healthStatus.Values.ToList());
    }

    public async Task<bool> HealthCheckAsync(string providerName, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out ProviderEntry? entry))
        {
            return false;
        }

        return await PerformHealthCheckAsync(entry).ConfigureAwait(false);
    }

    public Task RegisterProviderAsync(IProxyProvider provider, ProxyProviderConfig config, CancellationToken ct = default)
    {
        var entry = new ProviderEntry
        {
            Provider = provider,
            Config = config,
            LastUsed = DateTime.MinValue,
            UseCount = 0
        };

        _providers.TryAdd(config.Name, entry);

        if (_config.EnableHealthChecks)
        {
            _ = Task.Run(() => PerformHealthCheckAsync(entry), ct);
        }

        return Task.CompletedTask;
    }

    public Task UnregisterProviderAsync(string providerName, CancellationToken ct = default)
    {
        _providers.TryRemove(providerName, out _);

        var keysToRemove = _healthStatus.Keys.Where(k => k.StartsWith(providerName + "_", StringComparison.Ordinal)).ToList();
        foreach (string? key in keysToRemove)
        {
            _healthStatus.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAvailableCountriesAsync(CancellationToken ct = default)
    {
        var countries = _providers.Values
            .SelectMany(p => p.Config.SupportedCountries)
            .Distinct()
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(countries);
    }

    private List<ProviderEntry> GetEligibleProviders(string? countryCode)
    {
        var providers = _providers.Values.Where(p => p.Config.Enabled).ToList();

        if (!string.IsNullOrEmpty(countryCode) && _config.EnableGeographicRouting)
        {
            var geographicProviders = providers
                .Where(p => p.Config.SupportedCountries.Contains(countryCode, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (geographicProviders.Count > 0)
            {
                providers = geographicProviders;
            }
        }

        return providers;
    }

    private IProxyProvider? SelectProvider(List<ProviderEntry> providers)
    {
        var healthyProviders = providers.Where(p =>
        {
            string key = $"{p.Config.Name}_default";
            if (_healthStatus.TryGetValue(key, out ProxyHealthStatus? status))
            {
                return status.IsHealthy;
            }
            return true;
        }).ToList();

        if (healthyProviders.Count == 0)
        {
            healthyProviders = providers;
        }

        return _config.Strategy switch
        {
            ProxySelectionStrategy.RoundRobin => SelectRoundRobin(healthyProviders),
            ProxySelectionStrategy.LeastUsed => SelectLeastUsed(healthyProviders),
            ProxySelectionStrategy.Random => SelectRandom(healthyProviders),
            ProxySelectionStrategy.Weighted => SelectWeighted(healthyProviders),
            _ => SelectRoundRobin(healthyProviders)
        };
    }

    private IProxyProvider? SelectRoundRobin(List<ProviderEntry> providers)
    {
        int index = Interlocked.Increment(ref _roundRobinIndex) % providers.Count;
        ProviderEntry provider = providers[index];
        provider.LastUsed = _timeProvider.GetUtcNow().UtcDateTime;
        Interlocked.Increment(ref provider.UseCount);
        return provider.Provider;
    }

    private IProxyProvider? SelectLeastUsed(List<ProviderEntry> providers)
    {
        ProviderEntry provider = providers.OrderBy(p => p.UseCount).ThenBy(p => p.LastUsed).First();
        provider.LastUsed = _timeProvider.GetUtcNow().UtcDateTime;
        Interlocked.Increment(ref provider.UseCount);
        return provider.Provider;
    }

    private IProxyProvider? SelectRandom(List<ProviderEntry> providers)
    {
        int index = Random.Shared.Next(providers.Count);
        ProviderEntry provider = providers[index];
        provider.LastUsed = _timeProvider.GetUtcNow().UtcDateTime;
        Interlocked.Increment(ref provider.UseCount);
        return provider.Provider;
    }

    private IProxyProvider? SelectWeighted(List<ProviderEntry> providers)
    {
        int totalWeight = providers.Sum(p => p.Config.Weight);
        int random = Random.Shared.Next(totalWeight);
        int current = 0;

        foreach (ProviderEntry provider in providers)
        {
            current += provider.Config.Weight;
            if (random < current)
            {
                provider.LastUsed = _timeProvider.GetUtcNow().UtcDateTime;
                Interlocked.Increment(ref provider.UseCount);
                return provider.Provider;
            }
        }

        ProviderEntry lastProvider = providers.Last();
        lastProvider.LastUsed = _timeProvider.GetUtcNow().UtcDateTime;
        Interlocked.Increment(ref lastProvider.UseCount);
        return lastProvider.Provider;
    }

    private async Task<bool> PerformHealthCheckAsync(ProviderEntry entry)
    {
        string key = $"{entry.Config.Name}_default";

        try
        {
            using var cts = new CancellationTokenSource(_config.HealthCheckTimeout);
            ProxyInfo? proxy = await entry.Provider.GetProxyAsync("US", cts.Token).ConfigureAwait(false);

            var status = new ProxyHealthStatus
            {
                ProviderName = entry.Config.Name,
                Host = proxy?.Server ?? "unknown",
                IsHealthy = proxy != null,
                LastChecked = _timeProvider.GetUtcNow().UtcDateTime,
                SuccessCount = proxy != null ? 1 : 0
            };

            if (_healthStatus.TryGetValue(key, out ProxyHealthStatus? existing))
            {
                status.SuccessCount += existing.SuccessCount;
                status.FailureCount = existing.FailureCount;
            }

            _healthStatus[key] = status;
            return proxy != null;
        }
        catch (Exception ex)
        {
            var status = new ProxyHealthStatus
            {
                ProviderName = entry.Config.Name,
                Host = "unknown",
                IsHealthy = false,
                LastChecked = _timeProvider.GetUtcNow().UtcDateTime,
                LastFailure = _timeProvider.GetUtcNow().UtcDateTime,
                LastErrorMessage = ex.Message
            };

            if (_healthStatus.TryGetValue(key, out ProxyHealthStatus? existing))
            {
                status.SuccessCount = existing.SuccessCount;
                status.FailureCount = existing.FailureCount + 1;
            }

            _healthStatus[key] = status;
            return false;
        }
    }

    private void HealthCheckCallback(object? state)
    {
        _ = Task.Run(async () =>
        {
            foreach (ProviderEntry entry in _providers.Values)
            {
                try
                {
                    await PerformHealthCheckAsync(entry).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _healthCheckFailedForProvider(_logger, entry.Config.Name, ex);
                }
            }
        });
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
    }

    private sealed class ProviderEntry
    {
        public required IProxyProvider Provider { get; set; }
        public required ProxyProviderConfig Config { get; set; }
        public DateTime LastUsed { get; set; }
        public long UseCount;
    }
}
