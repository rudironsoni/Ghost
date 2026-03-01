using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Pool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Storage.Session;

/// <summary>
/// Orchestrates session allocation, lifecycle management, and health monitoring
/// for both HTTP and Browser sessions with intelligent routing capabilities.
/// </summary>
public sealed class SessionOrchestrator : ISessionOrchestrator, IAsyncDisposable
{
    // LoggerMessage delegates (EventIds 1200-1219)
    private static readonly Action<ILogger, int, int, Exception?> _initialized = LoggerMessage.Define<int, int>(
        LogLevel.Information,
        new EventId(1200, nameof(SessionOrchestrator)),
        "SessionOrchestrator initialized with {MaxHttpSessions} HTTP and {MaxBrowserSessions} browser sessions");

    private static readonly Action<ILogger, SessionType, string, string, int, Exception?> _allocating = LoggerMessage.Define<SessionType, string, string, int>(
        LogLevel.Debug,
        new EventId(1201, nameof(SessionOrchestrator)),
        "Allocating {SessionType} session for platform {Platform}, country {Country}, complexity {Complexity}");

    private static readonly Action<ILogger, string, Exception?> _failedToTrackSession = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1202, nameof(SessionOrchestrator)),
        "Failed to track session {SessionId}");

    private static readonly Action<ILogger, SessionType, string, string, Exception?> _allocated = LoggerMessage.Define<SessionType, string, string>(
        LogLevel.Information,
        new EventId(1203, nameof(SessionOrchestrator)),
        "Allocated {SessionType} session {SessionId} for platform {Platform}");

    private static readonly Action<ILogger, string, Exception?> _failedAllocate = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1204, nameof(SessionOrchestrator)),
        "Failed to allocate session for platform {Platform}");

    private static readonly Action<ILogger, int, Exception?> _performingHealthSweep = LoggerMessage.Define<int>(
        LogLevel.Debug,
        new EventId(1220, nameof(SessionOrchestrator)),
        "Performing health check sweep on {Count} sessions");

    private static readonly Action<ILogger, string, Exception?> _failedRecycle = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1221, nameof(SessionOrchestrator)),
        "Failed to recycle session {SessionId}");

    private static readonly Action<ILogger, string, Exception?> _evictedAffinityMapping = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(1222, nameof(SessionOrchestrator)),
        "Evicted affinity mapping {AffinityKey}");

    private static readonly Action<ILogger, string, Exception?> _failedDisposeSession = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1223, nameof(SessionOrchestrator)),
        "Failed to dispose session {SessionId}");

    private static readonly Action<ILogger, Exception?> _disposing = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1224, nameof(SessionOrchestrator)),
        "Disposing SessionOrchestrator");

    private static readonly Action<ILogger, Exception?> _disposedLog = LoggerMessage.Define(
        LogLevel.Information,
        new EventId(1225, nameof(SessionOrchestrator)),
        "SessionOrchestrator disposed");

    private static readonly Action<ILogger, string, string, Exception?> _affinityReusing = LoggerMessage.Define<string, string>(
        LogLevel.Debug,
        new EventId(1205, nameof(SessionOrchestrator)),
        "Reusing existing session {SessionId} for affinity key {AffinityKey}");

    private static readonly Action<ILogger, string, string, DateTime, Exception?> _affinityCreated = LoggerMessage.Define<string, string, DateTime>(
        LogLevel.Information,
        new EventId(1206, nameof(SessionOrchestrator)),
        "Created affinity mapping: {AffinityKey} -> {SessionId}, expires at {ExpiresAt}");

    private static readonly Action<ILogger, Exception?> _affinityDisabled = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1226, nameof(SessionOrchestrator)),
        "Session affinity is disabled, falling back to regular allocation");

    private static readonly Action<ILogger, string, Exception?> _sessionNotFoundWarning = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1207, nameof(SessionOrchestrator)),
        "Session {SessionId} not found");

    private static readonly Action<ILogger, string, Exception?> _sessionNotHttpWarning = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1208, nameof(SessionOrchestrator)),
        "Session {SessionId} is not an HTTP session");

    private static readonly Action<ILogger, string, Exception?> _sessionNotBrowserWarning = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1209, nameof(SessionOrchestrator)),
        "Session {SessionId} is not a browser session");

    private static readonly Action<ILogger, string, Exception?> _recyclingInfo = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1210, nameof(SessionOrchestrator)),
        "Recycling session {SessionId}");

    private static readonly Action<ILogger, string, Exception?> _sessionNotFoundForRecycle = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1211, nameof(SessionOrchestrator)),
        "Session {SessionId} not found for recycling");

    private static readonly Action<ILogger, string, Exception?> _recycledSuccess = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1212, nameof(SessionOrchestrator)),
        "Session {SessionId} recycled successfully");

    private static readonly Action<ILogger, string, TimeSpan, DateTime, Exception?> _ttlExtended = LoggerMessage.Define<string, TimeSpan, DateTime>(
        LogLevel.Debug,
        new EventId(1213, nameof(SessionOrchestrator)),
        "Extended TTL for session {SessionId} by {AdditionalTime}, new expiration: {ExpiresAt}");

    private static readonly Action<ILogger, string, string, Exception?> _persistedState = LoggerMessage.Define<string, string>(
        LogLevel.Information,
        new EventId(1214, nameof(SessionOrchestrator)),
        "Persisted session state for {SessionId} to {StoragePath}");

    private static readonly Action<ILogger, int, int, Exception?> _healthSweepInfo = LoggerMessage.Define<int, int>(
        LogLevel.Information,
        new EventId(1215, nameof(SessionOrchestrator)),
        "Health check sweep completed: recycled {RecycledCount} sessions, cleaned {ExpiredAffinity} affinity mappings");

    private static readonly Action<ILogger, string, Exception?> _closeSessionInfo = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1216, nameof(SessionOrchestrator)),
        "Closing session {SessionId}");

    private static readonly Action<ILogger, string, Exception?> _closeSessionNotFound = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1217, nameof(SessionOrchestrator)),
        "Session {SessionId} not found for closing");

    private static readonly Action<ILogger, string, Exception?> _closeSessionSuccess = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(1218, nameof(SessionOrchestrator)),
        "Session {SessionId} closed successfully");

    private static readonly Action<ILogger, Exception?> _disposeFailed = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1219, nameof(SessionOrchestrator)),
        "Failed during disposal");
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
        ArgumentNullException.ThrowIfNull(proxyProvider);
        ArgumentNullException.ThrowIfNull(browserPool);
        ArgumentNullException.ThrowIfNull(options);
        _proxyProvider = proxyProvider;
        _browserPool = browserPool;
        _options = options.Value;
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
            _initialized(_logger, _options.MaxConcurrentHttpSessions, _options.MaxConcurrentBrowserSessions, null);
        }
    }

    /// <inheritdoc/>
    public async Task<string> AllocateSessionAsync(SessionAllocationContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        SessionType sessionType = DetermineSessionType(context);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _allocating(_logger, sessionType, context.PlatformName, context.CountryCode ?? "any", context.ComplexityScore ?? 0, null);
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
                _failedToTrackSession(_logger, sessionId, null);
                throw new InvalidOperationException($"Session ID {sessionId} already exists");
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _allocated(_logger, sessionType, sessionId, context.PlatformName, null);
            }

            return sessionId;
        }
        catch (Exception ex)
        {
            _failedAllocate(_logger, context.PlatformName, ex);
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
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _affinityDisabled(_logger, null);
            }
            return await AllocateSessionAsync(context, ct).ConfigureAwait(false);
        }

        if (_affinityMappings.TryGetValue(affinityOptions.AffinityKey, out AffinityMapping? existingMapping))
        {
            if (existingMapping.ExpiresAt > DateTime.UtcNow && _sessions.ContainsKey(existingMapping.SessionId))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _affinityReusing(_logger, existingMapping.SessionId, affinityOptions.AffinityKey, null);
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
                _affinityCreated(_logger, affinityOptions.AffinityKey, sessionId, mapping.ExpiresAt, null);
            }

        return sessionId;
    }

    /// <inheritdoc/>
    public Task<RotatingProxySession?> GetHttpSessionAsync(string sessionId, CancellationToken ct = default)
    {
            if (!_sessions.TryGetValue(sessionId, out SessionMetadata? metadata))
            {
                _sessionNotFoundWarning(_logger, sessionId, null);
                return Task.FromResult<RotatingProxySession?>(null);
            }

            if (metadata.SessionType != SessionType.Http)
            {
                _sessionNotHttpWarning(_logger, sessionId, null);
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
                _sessionNotFoundWarning(_logger, sessionId, null);
                return Task.FromResult<IBrowserSession?>(null);
            }

            if (metadata.SessionType != SessionType.Browser)
            {
                _sessionNotBrowserWarning(_logger, sessionId, null);
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
            _recyclingInfo(_logger, sessionId, null);
        }

        if (!_sessions.TryRemove(sessionId, out SessionMetadata? metadata))
        {
            _sessionNotFoundForRecycle(_logger, sessionId, null);
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
            _recycledSuccess(_logger, sessionId, null);
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
            _ttlExtended(_logger, sessionId, additionalTime, metadata.ExpiresAt, null);
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
            _persistedState(_logger, sessionId, storagePath, null);
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
            _closeSessionInfo(_logger, sessionId, null);
        }

        if (!_sessions.TryRemove(sessionId, out SessionMetadata? metadata))
        {
            _closeSessionNotFound(_logger, sessionId, null);
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
            _closeSessionSuccess(_logger, sessionId, null);
        }
    }

    /// <inheritdoc/>
    public async Task<int> PerformHealthCheckSweepAsync(CancellationToken ct = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _performingHealthSweep(_logger, _sessions.Count, null);
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
                _failedRecycle(_logger, sessionId, ex);
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
            _healthSweepInfo(_logger, recycledCount, expiredAffinityKeys.Count, null);
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
                _evictedAffinityMapping(_logger, oldestKey, null);
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
            _failedDisposeSession(_logger, metadata.SessionId, ex);
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
                _disposeFailed(_logger, ex);
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

        _disposing(_logger, null);

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
                _disposeFailed(_logger, ex);
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);

        _disposedLog(_logger, null);
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
