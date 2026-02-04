using Ghost.Sdk.Spider.Strategies.Contracts;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Router for executing extraction strategies with fallback and chaining support.
/// </summary>
/// <remarks>
/// The strategy router manages multiple extraction strategies, executing them in priority
/// order with automatic fallback when strategies fail. It tracks metrics for each strategy
/// to aid in performance analysis and optimization.
/// </remarks>
public class StrategyRouter : IStrategyRouter
{
    private readonly Dictionary<string, Func<StrategyContext, CancellationToken, Task<ExtractionResult>>> _strategies;
    private readonly Dictionary<string, StrategyMetrics> _metrics;
    private readonly ILogger<StrategyRouter>? _logger;
    private readonly object _metricsLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="StrategyRouter"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public StrategyRouter(ILogger<StrategyRouter>? logger = null)
    {
        _strategies = new Dictionary<string, Func<StrategyContext, CancellationToken, Task<ExtractionResult>>>();
        _metrics = new Dictionary<string, StrategyMetrics>();
        _logger = logger;
    }

    /// <summary>
    /// Registers a strategy with the router.
    /// </summary>
    /// <param name="name">The unique name for the strategy.</param>
    /// <param name="strategy">The strategy execution function.</param>
    public void RegisterStrategy(
        string name,
        Func<StrategyContext, CancellationToken, Task<ExtractionResult>> strategy)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(strategy);

        _strategies[name] = strategy;
        
        lock (_metricsLock)
        {
            if (!_metrics.ContainsKey(name))
            {
                _metrics[name] = new StrategyMetrics { StrategyName = name };
            }
        }

        _logger?.LogDebug("Registered strategy: {StrategyName}", name);
    }

    /// <inheritdoc/>
    public async Task<ExtractionResult> ExecuteAsync(
        StrategyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger?.LogDebug("Executing strategies for URL: {Url}", context.Url);

        // Execute strategies in order until one succeeds
        foreach (var (name, strategy) in _strategies.OrderBy(s => s.Key))
        {
            try
            {
                var result = await ExecuteStrategyInternalAsync(name, strategy, context, cancellationToken)
                    .ConfigureAwait(false);

                if (result.Success)
                {
                    _logger?.LogDebug("Strategy {StrategyName} succeeded for {Url}", name, context.Url);
                    return result;
                }

                _logger?.LogDebug("Strategy {StrategyName} failed for {Url}: {Error}",
                    name, context.Url, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Strategy {StrategyName} threw exception for {Url}", name, context.Url);
                UpdateMetrics(name, false, TimeSpan.Zero);
            }
        }

        // All strategies failed
        return ExtractionResult.CreateFailure(
            "All strategies failed",
            "StrategyRouter",
            TimeSpan.Zero);
    }

    /// <inheritdoc/>
    public async Task<ExtractionResult> ExecuteStrategyAsync(
        string strategyName,
        StrategyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategyName);
        ArgumentNullException.ThrowIfNull(context);

        if (!_strategies.TryGetValue(strategyName, out var strategy))
        {
            throw new InvalidOperationException($"Strategy not found: {strategyName}");
        }

        return await ExecuteStrategyInternalAsync(strategyName, strategy, context, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ExtractionResult> ExecuteChainAsync(
        StrategyChain chain,
        StrategyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(context);

        var chainStartTime = DateTimeOffset.UtcNow;
        var results = new List<ExtractionResult>();
        var aggregatedData = new Dictionary<string, object>();

        foreach (var strategyConfig in chain.Strategies)
        {
            var strategyName = strategyConfig.Name;
            if (!_strategies.TryGetValue(strategyName, out var strategy))
            {
                _logger?.LogWarning("Strategy {StrategyName} not found in chain", strategyName);
                continue;
            }

            var result = await ExecuteStrategyInternalAsync(strategyName, strategy, context, cancellationToken)
                .ConfigureAwait(false);

            results.Add(result);

            if (result.Success && result.Data != null)
            {
                aggregatedData[strategyName] = result.Data;
            }

            // Stop chain on failure if configured
            if (!result.Success && chain.StopOnFailure)
            {
                _logger?.LogWarning("Chain execution stopped at {StrategyName} due to failure", strategyName);
                break;
            }
        }

        var chainDuration = DateTimeOffset.UtcNow - chainStartTime;
        var allSucceeded = results.All(r => r.Success);

        return new ExtractionResult
        {
            Success = allSucceeded,
            Data = aggregatedData,
            StrategyName = $"Chain:{chain.Name}",
            Duration = chainDuration,
            Metadata = new Dictionary<string, object>
            {
                ["ChainResults"] = results,
                ["StrategiesExecuted"] = results.Count
            }
        };
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, StrategyMetrics> GetMetrics()
    {
        lock (_metricsLock)
        {
            return new Dictionary<string, StrategyMetrics>(_metrics);
        }
    }

    /// <inheritdoc/>
    public void ResetMetrics()
    {
        lock (_metricsLock)
        {
            foreach (var metric in _metrics.Values)
            {
                metric.Reset();
            }
        }

        _logger?.LogInformation("Strategy metrics reset");
    }

    private async Task<ExtractionResult> ExecuteStrategyInternalAsync(
        string name,
        Func<StrategyContext, CancellationToken, Task<ExtractionResult>> strategy,
        StrategyContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            var result = await strategy(context, cancellationToken).ConfigureAwait(false);
            var duration = DateTimeOffset.UtcNow - startTime;

            UpdateMetrics(name, result.Success, duration);

            return result;
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            UpdateMetrics(name, false, duration);

            return ExtractionResult.CreateFailure(
                $"Strategy execution failed: {ex.Message}",
                name,
                duration,
                ex);
        }
    }

    private void UpdateMetrics(string strategyName, bool success, TimeSpan duration)
    {
        lock (_metricsLock)
        {
            if (_metrics.TryGetValue(strategyName, out var metrics))
            {
                if (success)
                    metrics.RecordSuccess(duration, DateTime.UtcNow);
                else
                    metrics.RecordFailure(duration, DateTime.UtcNow);
            }
        }
    }
}
