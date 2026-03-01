namespace Ghost.Pool;

/// <summary>
/// Tiered browser pool for managing browser sessions across Hot, Warm, and Cold tiers
/// </summary>
public interface ITieredBrowserPool : IAsyncDisposable
{
    /// <summary>
    /// Acquire a browser session from the specified tier
    /// </summary>
    public Task<IBrowserSession> AcquireBrowserAsync(Tier tier = Tier.Hot, CancellationToken ct = default);

    /// <summary>
    /// Return a browser session to the pool
    /// </summary>
    public Task ReturnBrowserAsync(IBrowserSession session, CancellationToken ct = default);

    /// <summary>
    /// Get the current health status of the pool
    /// </summary>
    public Task<PoolHealth> GetHealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Force warm-up of a specific tier
    /// </summary>
    public Task WarmUpAsync(Tier tier, int count, CancellationToken ct = default);
}

/// <summary>
/// Browser pool tier for different performance characteristics
/// </summary>
public enum Tier
{
    /// <summary>
    /// Pre-warmed sessions ready immediately (&lt;500ms)
    /// </summary>
    Hot,

    /// <summary>
    /// Pre-configured sessions with fast warm-up (&lt;1.5s)
    /// </summary>
    Warm,

    /// <summary>
    /// On-demand sessions spawned as needed
    /// </summary>
    Cold
}

/// <summary>
/// Health status of the browser pool
/// </summary>
public sealed class PoolHealth
{
    public required TierHealth Hot { get; init; }
    public required TierHealth Warm { get; init; }
    public required TierHealth Cold { get; init; }
    public required bool IsHealthy { get; init; }
    public required long TotalAcquisitions { get; init; }
    public required long ActiveSessions { get; init; }
    public required double MemoryPressure { get; init; }
}

/// <summary>
/// Health status for a specific tier
/// </summary>
public sealed class TierHealth
{
    public required int Available { get; init; }
    public required int InUse { get; init; }
    public required int Total { get; init; }
    public required double AverageAcquisitionTimeMs { get; init; }
    public required long AcquisitionCount { get; init; }
    public required bool IsHealthy { get; init; }
}
