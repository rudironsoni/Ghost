using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Pool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghost.Infrastructure.Session;

/// <summary>
/// Orchestrates session allocation, lifecycle management, and health monitoring
/// for both HTTP and Browser sessions with intelligent routing capabilities.
/// </summary>
[SuppressMessage("Performance", "CA1848:Use LoggerMessage delegates", Justification = "Session orchestration - readability over performance")]
public sealed class SessionOrchestrator : ISessionOrchestrator, IAsyncDisposable
{
    private readonly IProxyProvider _proxyProvider;
    private readonly ITieredBrowserPool _browserPool;
    private readonly SessionOrchestratorOptions _options;
    private readonly ILogger<SessionOrchestrator> _logger;

    private readonly ConcurrentDictionary<string, SessionMetadata> _sessions = new();
    private readonly ConcurrentDictionary<string, AffinityMapping> _affinityMappings = new();
    private readonly Timer? _healthCheckTimer;
    private bool _disposed;

    public SessionOrchestrator(
        IProxyProvider proxyProvider,
        ITieredBrowserPool browserPool,
        IOptions<SessionOrchestratorOptions> options,
        ILogger<SessionOrchestrator>? logger = null)
    {
        _proxyProvider = proxyProvider ?? throw new ArgumentNullException(nameof(proxyProvider));
        _browserPool = browserPool ?? throw new ArgumentNullException(nameof(browserPool));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<SessionOrchestrator>.Instance;

        _options.Validate();

        if (_options.EnableAutoRecycling)
        {
            _healthCheckTimer = new Timer(
                HealthCheckCallback,
                null,
                _options.HealthCheckInterval,
                _options.HealthCheckInterval);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "SessionOrchestrator initialized with {MaxHttpSessions} HTTP and {MaxBrowserSessions} browser sessions",
                _options.MaxConcurrentHttpSessions,
                _options.MaxConcurrentBrowserSessions);
        }
    }

    /// <inheritdoc/>
    public async Task<string> AllocateSessionAsync(SessionAllocationContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        SessionType sessionType = DetermineSessionType(context);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Allocating {SessionType} session for platform {Platform}, country {Country}, complexity {Complexity}",
                sessionType,
                context.PlatformName,
                context.CountryCode ?? "any",
                context.ComplexityScore ?? 0);
        }

        string sessionId = GenerateSessionId();
        var metadata = new SessionMetadata
        {
            SessionId = sessionId,
            SessionType = sessionType,
            PlatformName = context.PlatformName,
            CountryCode = context.CountryCode,
            ComplexityScore = context.ComplexityScore,
            CreatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_options.DefaultSessionTtl),
            SuccessfulRequests = 0,
            FailedRequests = 0,
            RecentFailures = new ConcurrentQueue<DateTime>(),
            Metadata = context.Metadata
        };

        try
        {
            if (sessionType == SessionType.Http)
            {
                await AllocateHttpSessionAsync(metadata, ct).ConfigureAwait(false);
            }
            else
            {
                await AllocateBrowserSessionAsync(metadata, context.ComplexityScore, ct).ConfigureAwait(false);
            }

            if (!_sessions.TryAdd(sessionId, metadata))
            {
                _logger.LogError("Failed to track session {SessionId}", sessionId);
                throw new InvalidOperationException($"Session ID {sessionId} already exists");
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Allocated {SessionType} session {SessionId} for platform {Platform}",
                    sessionType,
                    sessionId,
                    context.PlatformName);
            }

            return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate session for platform {Platform}", context.PlatformName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> AllocateSessionWithAffinityAsync(
        SessionAllocationContext context,
        SessionAffinityOptions affinityOptions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(affinityOptions);

        if (!_options.EnableSessionAffinity)
        {
            _logger.LogWarning("Session affinity is disabled, falling back to regular allocation");
            return await AllocateSessionAsync(context, ct).ConfigureAwait(false);
        }

        if (_affinityMappings.TryGetValue(affinityOptions.AffinityKey, out AffinityMapping? existingMapping))
        {
            if (existingMapping.ExpiresAt > DateTime.UtcNow && _sessions.ContainsKey(existingMapping.SessionId))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Reusing existing session {SessionId} for affinity key {AffinityKey}",
                        existingMapping.SessionId,
                        affinityOptions.AffinityKey);
                }

                if (_sessions.TryGetValue(existingMapping.SessionId, out SessionMetadata? metadata))
                {
                    metadata.LastUsedAt = DateTime.UtcNow;
                }

                return existingMapping.SessionId;
            }

            _affinityMappings.TryRemove(affinityOptions.AffinityKey, out _);
        }

        string sessionId = await AllocateSessionAsync(context, ct).ConfigureAwait(false);

        TimeSpan affinityDuration = affinityOptions.AffinityDuration ?? _options.DefaultAffinityDuration;

        if (affinityDuration > _options.MaxAffinityDuration)
        {
            affinityDuration = _options.MaxAffinityDuration;
        }

        var mapping = new AffinityMapping
        {
            AffinityKey = affinityOptions.AffinityKey,
            SessionId = sessionId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(affinityDuration)
        };

        if (_affinityMappings.Count >= _options.MaxAffinityCacheSize)
        {
            EvictOldestAffinityMapping();
        }

        _affinityMappings.TryAdd(affinityOptions.AffinityKey, mapping);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Created affinity mapping: {AffinityKey} -> {SessionId}, expires at {ExpiresAt}",
                affinityOptions.AffinityKey,
                sessionId,
                mapping.ExpiresAt);
        }

        return sessionId;
    }

    /// <inheritdoc/>
    public Task<RotatingProxySession?> GetHttpSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out SessionMetadata? metadata))
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            return Task.FromResult<RotatingProxySession?>(null);
        }

        if (metadata.SessionType != SessionType.Http)
        {
            _logger.LogWarning("Session {SessionId} is not an HTTP session", sessionId);
            return Task.FromResult<RotatingProxySession?>(null);
        }

        metadata.LastUsedAt = DateTime.UtcNow;
        return Task.FromResult<RotatingProxySession?>(metadata.HttpSession);
    }

    /// <inheritdoc/>
    public Task<IBrowserSession?> GetBrowserSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out SessionMetadata? metadata))
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            return Task.FromResult<IBrowserSession?>(null);
        }

        if (metadata.SessionType != SessionType.Browser)
        {
            _logger.LogWarning("Session {SessionId} is not a browser session", sessionId);
            return Task.FromResult<IBrowserSession?>(null);
        }

        metadata.LastUsedAt = DateTime.UtcNow;
        return Task.FromResult<IBrowserSession?>(metadata.BrowserSession);
    }

    /// <inheritdoc/>
    public Task<SessionHealthMetrics> GetSessionHealthAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out SessionMetadata? metadata))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        SessionHealth health = CalculateSessionHealth(metadata);
        TimeSpan uptime = DateTime.UtcNow - metadata.CreatedAt;

        var metrics = new SessionHealthMetrics(
            SessionId: sessionId,
            Health: health,
            LastChecked: DateTime.UtcNow,
            SuccessfulRequests: metadata.SuccessfulRequests,
            FailedRequests: metadata.FailedRequests,
            Uptime: uptime,
            AdditionalMetrics: _options.EnableDetailedHealthMetrics
                ? BuildDetailedMetrics(metadata)
                : null);

        return Task.FromResult(metrics);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SessionHealthMetrics>> GetAllSessionHealthAsync(CancellationToken ct = default)
    {
        var metrics = _sessions.Values
            .Select(metadata =>
            {
                SessionHealth health = CalculateSessionHealth(metadata);
                TimeSpan uptime = DateTime.UtcNow - metadata.CreatedAt;

                return new SessionHealthMetrics(
                    SessionId: metadata.SessionId,
                    Health: health,
                    LastChecked: DateTime.UtcNow,
                    SuccessfulRequests: metadata.SuccessfulRequests,
                    FailedRequests: metadata.FailedRequests,
                    Uptime: uptime,
                    AdditionalMetrics: _options.EnableDetailedHealthMetrics
                        ? BuildDetailedMetrics(metadata)
                        : null);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionHealthMetrics>>(metrics);
    }

    /// <inheritdoc/>
    public async Task RecycleSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Recycling session {SessionId}", sessionId);
        }

        if (!_sessions.TryRemove(sessionId, out SessionMetadata? metadata))
        {
            _logger.LogWarning("Session {SessionId} not found for recycling", sessionId);
            return;
        }

        var affinityKeys = _affinityMappings
            .Where(kvp => kvp.Value.SessionId == sessionId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string? key in affinityKeys)
        {
            _affinityMappings.TryRemove(key, out _);
        }

        await DisposeSessionAsync(metadata).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Session {SessionId} recycled successfully", sessionId);
        }
    }

    /// <inheritdoc/>
    public Task ExtendSessionTtlAsync(string sessionId, TimeSpan additionalTime, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out SessionMetadata? metadata))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        metadata.ExpiresAt = metadata.ExpiresAt.Add(additionalTime);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Extended TTL for session {SessionId} by {AdditionalTime}, new expiration: {ExpiresAt}",
                sessionId,
                additionalTime,
                metadata.ExpiresAt);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task PersistSessionStateAsync(string sessionId, string storagePath, CancellationToken ct = default)
    {
        if (!_options.EnableStatePersistence)
        {
            throw new InvalidOperationException("Session state persistence is disabled");
        }

        if (!_sessions.TryGetValue(sessionId, out SessionMetadata? metadata))
        {
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        if (metadata.SessionType != SessionType.Browser || metadata.BrowserSession == null)
        {
            throw new InvalidOperationException("Only browser sessions support state persistence");
        }

        string? directory = Path.GetDirectoryName(storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await metadata.BrowserSession.SaveStorageStateAsync(storagePath).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Persisted session state for {SessionId} to {StoragePath}",
                sessionId,
                storagePath);
        }
    }

    /// <inheritdoc/>
    public Task<string> RestoreSessionFromStateAsync(string storagePath, CancellationToken ct = default)
    {
        if (!_options.EnableStatePersistence)
        {
            throw new InvalidOperationException("Session state persistence is disabled");
        }

        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"Session state file not found: {storagePath}");
        }

        // TODO: Implement session restoration from storage state
        // This requires creating a browser session with restored state
        // For now, throw NotImplementedException as this requires integration with GhostKernel
        throw new NotImplementedException(
            "Session restoration from state is not yet implemented. " +
            "This requires integration with GhostKernel to create sessions with storage state.");
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        var sessionIds = _sessions.Keys.ToList();
        return Task.FromResult<IReadOnlyList<string>>(sessionIds);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> GetActiveSessionsByTypeAsync(SessionType sessionType, CancellationToken ct = default)
    {
        var sessionIds = _sessions.Values
            .Where(m => m.SessionType == sessionType)
            .Select(m => m.SessionId)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(sessionIds);
    }

    /// <inheritdoc/>
    public async Task CloseSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Closing session {SessionId}", sessionId);
        }

        if (!_sessions.TryRemove(sessionId, out SessionMetadata? metadata))
        {
            _logger.LogWarning("Session {SessionId} not found for closing", sessionId);
            return;
        }

        var affinityKeys = _affinityMappings
            .Where(kvp => kvp.Value.SessionId == sessionId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string? key in affinityKeys)
        {
            _affinityMappings.TryRemove(key, out _);
        }

        await DisposeSessionAsync(metadata).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Session {SessionId} closed successfully", sessionId);
        }
    }

    /// <inheritdoc/>
    public async Task<int> PerformHealthCheckSweepAsync(CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Performing health check sweep on {Count} sessions", _sessions.Count);
        }

        int recycledCount = 0;
        DateTime now = DateTime.UtcNow;

        var sessionsToRecycle = _sessions.Values
            .Where(metadata =>
            {
                if (metadata.ExpiresAt <= now)
                {
                    return true;
                }

                SessionHealth health = CalculateSessionHealth(metadata);
                return health == SessionHealth.Unhealthy;
            })
            .Select(m => m.SessionId)
            .ToList();

        foreach (string? sessionId in sessionsToRecycle)
        {
            try
            {
                await RecycleSessionAsync(sessionId, ct).ConfigureAwait(false);
                recycledCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recycle session {SessionId}", sessionId);
            }
        }

        var expiredAffinityKeys = _affinityMappings
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string? key in expiredAffinityKeys)
        {
            _affinityMappings.TryRemove(key, out _);
        }

        if (recycledCount > 0)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Health check sweep completed: recycled {RecycledCount} sessions, cleaned {ExpiredAffinity} affinity mappings",
                    recycledCount,
                    expiredAffinityKeys.Count);
            }
        }

        return recycledCount;
    }

    /// <summary>
    /// Determines the appropriate session type based on allocation context.
    /// </summary>
    private SessionType DetermineSessionType(SessionAllocationContext context)
    {
        if (context.SessionType != default)
        {
            return context.SessionType;
        }

        if (!_options.EnableComplexityRouting)
        {
            return SessionType.Http;
        }

        if (context.ComplexityScore.HasValue &&
            context.ComplexityScore.Value >= _options.BrowserSessionComplexityThreshold)
        {
            return SessionType.Browser;
        }

        return SessionType.Http;
    }

    /// <summary>
    /// Allocates an HTTP session with proxy rotation.
    /// </summary>
    private Task AllocateHttpSessionAsync(SessionMetadata metadata, CancellationToken ct)
    {
        int currentHttpSessions = _sessions.Values.Count(m => m.SessionType == SessionType.Http);
        if (currentHttpSessions >= _options.MaxConcurrentHttpSessions)
        {
            throw new InvalidOperationException(
                $"Maximum concurrent HTTP sessions ({_options.MaxConcurrentHttpSessions}) reached");
        }

        var sessionOptions = new RotatingProxySessionOptions
        {
            DefaultCountryCode = metadata.CountryCode ?? string.Empty,
            EnableProxyRotation = true,
            Timeout = _options.SessionAcquisitionTimeout
        };

        metadata.HttpSession = new RotatingProxySession(_proxyProvider, sessionOptions);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Allocates a browser session from the tiered pool.
    /// </summary>
    private async Task AllocateBrowserSessionAsync(
        SessionMetadata metadata,
        int? complexityScore,
        CancellationToken ct)
    {
        int currentBrowserSessions = _sessions.Values.Count(m => m.SessionType == SessionType.Browser);
        if (currentBrowserSessions >= _options.MaxConcurrentBrowserSessions)
        {
            throw new InvalidOperationException(
                $"Maximum concurrent browser sessions ({_options.MaxConcurrentBrowserSessions}) reached");
        }

        Tier tier = DetermineBrowserTier(complexityScore);

        using var timeoutCts = new CancellationTokenSource(_options.SessionAcquisitionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        metadata.BrowserSession = await _browserPool.AcquireBrowserAsync(tier, linkedCts.Token).ConfigureAwait(false);
        metadata.BrowserTier = tier;
    }

    /// <summary>
    /// Determines the appropriate browser pool tier based on complexity score.
    /// </summary>
    private static Tier DetermineBrowserTier(int? complexityScore)
    {
        if (!complexityScore.HasValue)
        {
            return Tier.Hot;
        }

        // Complexity thresholds: 80+ Hot, 50-79 Warm, <50 Cold
        return complexityScore.Value switch
        {
            >= 80 => Tier.Hot,
            >= 50 => Tier.Warm,
            _ => Tier.Cold
        };
    }

    /// <summary>
    /// Calculates the health status of a session based on recent failures.
    /// </summary>
    private SessionHealth CalculateSessionHealth(SessionMetadata metadata)
    {
        DateTime cutoffTime = DateTime.UtcNow.Subtract(_options.FailureTrackingWindow);

        while (metadata.RecentFailures.TryPeek(out DateTime failureTime))
        {
            if (failureTime < cutoffTime)
            {
                metadata.RecentFailures.TryDequeue(out _);
            }
            else
            {
                break;
            }
        }

        int recentFailureCount = metadata.RecentFailures.Count;
        int threshold = metadata.SessionType == SessionType.Http
            ? _options.HttpSessionFailureThreshold
            : _options.BrowserSessionFailureThreshold;

        if (recentFailureCount >= threshold)
        {
            return SessionHealth.Unhealthy;
        }

        if (recentFailureCount >= threshold / 2)
        {
            return SessionHealth.Degraded;
        }

        return SessionHealth.Healthy;
    }

    /// <summary>
    /// Builds detailed metrics for a session.
    /// </summary>
    private static Dictionary<string, object> BuildDetailedMetrics(SessionMetadata metadata)
    {
        return new Dictionary<string, object>
        {
            ["SessionType"] = metadata.SessionType.ToString(),
            ["Platform"] = metadata.PlatformName,
            ["CountryCode"] = metadata.CountryCode ?? "any",
            ["ComplexityScore"] = metadata.ComplexityScore ?? 0,
            ["CreatedAt"] = metadata.CreatedAt,
            ["ExpiresAt"] = metadata.ExpiresAt,
            ["RecentFailureCount"] = metadata.RecentFailures.Count,
            ["BrowserTier"] = metadata.BrowserTier?.ToString() ?? "N/A"
        };
    }

    /// <summary>
    /// Generates a unique session identifier.
    /// </summary>
    private static string GenerateSessionId()
    {
        return $"session_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Evicts the oldest affinity mapping to maintain cache size limit.
    /// </summary>
    private void EvictOldestAffinityMapping()
    {
        string? oldestKey = _affinityMappings
            .OrderBy(kvp => kvp.Value.CreatedAt)
            .Select(kvp => kvp.Key)
            .FirstOrDefault();

        if (oldestKey != null)
        {
            _affinityMappings.TryRemove(oldestKey, out _);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Evicted affinity mapping {AffinityKey}", oldestKey);
            }
        }
    }

    /// <summary>
    /// Disposes a session and its resources.
    /// </summary>
    private async Task DisposeSessionAsync(SessionMetadata metadata)
    {
        try
        {
            if (metadata.SessionType == SessionType.Http && metadata.HttpSession != null)
            {
                metadata.HttpSession.Dispose();
            }
            else if (metadata.SessionType == SessionType.Browser && metadata.BrowserSession != null)
            {
                await _browserPool.ReturnBrowserAsync(metadata.BrowserSession, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispose session {SessionId}", metadata.SessionId);
        }
    }

    /// <summary>
    /// Health check timer callback.
    /// </summary>
    private void HealthCheckCallback(object? state)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await PerformHealthCheckSweepAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check sweep failed");
            }
        });
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing SessionOrchestrator");

        _healthCheckTimer?.Dispose();

        var sessionIds = _sessions.Keys.ToList();
        foreach (string? sessionId in sessionIds)
        {
            try
            {
                await CloseSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close session {SessionId} during disposal", sessionId);
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);

        _logger.LogInformation("SessionOrchestrator disposed");
    }

    /// <summary>
    /// Metadata for tracking session state and health.
    /// </summary>
    private sealed class SessionMetadata
    {
        public required string SessionId { get; init; }
        public required SessionType SessionType { get; init; }
        public required string PlatformName { get; init; }
        public string? CountryCode { get; init; }
        public int? ComplexityScore { get; init; }
        public required DateTime CreatedAt { get; init; }
        public DateTime LastUsedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public ConcurrentQueue<DateTime> RecentFailures { get; init; } = new();
        public Dictionary<string, string>? Metadata { get; init; }

        public RotatingProxySession? HttpSession { get; set; }
        public IBrowserSession? BrowserSession { get; set; }
        public Tier? BrowserTier { get; set; }
    }

    /// <summary>
    /// Affinity mapping for session stickiness.
    /// </summary>
    private sealed class AffinityMapping
    {
        public required string AffinityKey { get; init; }
        public required string SessionId { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime ExpiresAt { get; init; }
    }
}
