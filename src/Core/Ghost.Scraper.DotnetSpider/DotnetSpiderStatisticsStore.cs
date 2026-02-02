using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotnetSpider.Statistic.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Scraper.DotnetSpider;

/// <summary>
/// Implements IStatisticStore to integrate DotnetSpider statistics with Ghost's monitoring infrastructure.
/// </summary>
/// <remarks>
/// This in-memory statistics store aggregates spider and agent statistics per platform, 
/// providing integration hooks for Ghost's health check and monitoring systems.
/// </remarks>
public sealed class DotnetSpiderStatisticsStore : IStatisticStore
{
    private readonly ConcurrentDictionary<string, SpiderStatistic> _spiderStatistics = new();
    private readonly ConcurrentDictionary<string, AgentStatistic> _agentStatistics = new();
    private readonly ILogger<DotnetSpiderStatisticsStore> _logger;

    /// <summary>
    /// Gets the platform name for this statistics store instance.
    /// </summary>
    public string PlatformName { get; }

    /// <summary>
    /// Gets the total number of spiders tracked.
    /// </summary>
    public int TrackedSpiderCount => _spiderStatistics.Count;

    /// <summary>
    /// Gets the total number of agents tracked.
    /// </summary>
    public int TrackedAgentCount => _agentStatistics.Count;

    /// <summary>
    /// Initializes a new instance of the DotnetSpiderStatisticsStore class.
    /// </summary>
    /// <param name="platformName">The name of the platform being monitored (e.g., "LinkedIn", "Indeed", "Google").</param>
    /// <param name="logger">Optional logger instance. If null, NullLogger will be used.</param>
    public DotnetSpiderStatisticsStore(string platformName, ILogger<DotnetSpiderStatisticsStore>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(platformName))
        {
            throw new ArgumentException("Platform name cannot be null or whitespace", nameof(platformName));
        }

        PlatformName = platformName;
        _logger = logger ?? NullLogger<DotnetSpiderStatisticsStore>.Instance;

        LogStatisticsStoreInitialized(PlatformName);
    }

    /// <summary>
    /// Ensures database and table are created. In-memory store has no-op implementation.
    /// </summary>
    /// <returns>A completed task.</returns>
    public Task EnsureDatabaseAndTableCreatedAsync()
    {
        LogDatabaseEnsured();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Increases the total request count for a spider.
    /// </summary>
    /// <param name="id">The spider identifier.</param>
    /// <param name="count">The number of requests to add.</param>
    /// <returns>A completed task.</returns>
    public Task IncreaseTotalAsync(string id, long count)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            LogInvalidSpiderId();
            return Task.CompletedTask;
        }

        var statistic = _spiderStatistics.GetOrAdd(id, new SpiderStatistic(id));
        statistic.IncrementTotal(count);
        LogTotalIncremented(id, count, statistic.Total);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Increases the success count for a spider.
    /// </summary>
    /// <param name="id">The spider identifier.</param>
    /// <returns>A completed task.</returns>
    public Task IncreaseSuccessAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            LogInvalidSpiderId();
            return Task.CompletedTask;
        }

        var statistic = _spiderStatistics.GetOrAdd(id, new SpiderStatistic(id));
        statistic.IncrementSuccess();
        LogSuccessIncremented(id, statistic.Success, statistic.Total);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Increases the failure count for a spider.
    /// </summary>
    /// <param name="id">The spider identifier.</param>
    /// <returns>A completed task.</returns>
    public Task IncreaseFailureAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            LogInvalidSpiderId();
            return Task.CompletedTask;
        }

        var statistic = _spiderStatistics.GetOrAdd(id, new SpiderStatistic(id));
        statistic.IncrementFailure();
        LogFailureIncremented(id, statistic.Failure, statistic.Total);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records spider startup.
    /// </summary>
    /// <param name="id">The spider identifier.</param>
    /// <param name="name">The spider name.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            LogInvalidSpiderId();
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            LogInvalidSpiderName();
            return Task.CompletedTask;
        }

        var statistic = _spiderStatistics.GetOrAdd(id, new SpiderStatistic(id));
        statistic.SetName(name);
        statistic.OnStarted();
        LogSpiderStarted(id, name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records spider shutdown.
    /// </summary>
    /// <param name="id">The spider identifier.</param>
    /// <returns>A completed task.</returns>
    public Task ExitAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            LogInvalidSpiderId();
            return Task.CompletedTask;
        }

        if (_spiderStatistics.TryGetValue(id, out var statistic))
        {
            statistic.OnExited();
            var duration = statistic.Exit.HasValue && statistic.Start.HasValue
                ? (statistic.Exit.Value - statistic.Start.Value).TotalMilliseconds
                : 0;
            LogSpiderExited(id, statistic.Name ?? "Unknown", duration);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers an agent (downloader node).
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agentName">The agent name.</param>
    /// <returns>A completed task.</returns>
    public Task RegisterAgentAsync(string agentId, string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            LogInvalidAgentId();
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            LogInvalidAgentName();
            return Task.CompletedTask;
        }

        var statistic = _agentStatistics.GetOrAdd(agentId, new AgentStatistic(agentId));
        statistic.SetName(agentName);
        LogAgentRegistered(agentId, agentName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records a successful agent download.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="elapsedMilliseconds">Elapsed time in milliseconds.</param>
    /// <returns>A completed task.</returns>
    public Task IncreaseAgentSuccessAsync(string agentId, int elapsedMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            LogInvalidAgentId();
            return Task.CompletedTask;
        }

        var statistic = _agentStatistics.GetOrAdd(agentId, new AgentStatistic(agentId));
        statistic.IncreaseSuccess();
        statistic.IncreaseElapsedMilliseconds(elapsedMilliseconds);
        LogAgentSuccess(agentId, statistic.Success, elapsedMilliseconds);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records a failed agent download.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="elapsedMilliseconds">Elapsed time in milliseconds.</param>
    /// <returns>A completed task.</returns>
    public Task IncreaseAgentFailureAsync(string agentId, int elapsedMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            LogInvalidAgentId();
            return Task.CompletedTask;
        }

        var statistic = _agentStatistics.GetOrAdd(agentId, new AgentStatistic(agentId));
        statistic.IncreaseFailure();
        statistic.IncreaseElapsedMilliseconds(elapsedMilliseconds);
        LogAgentFailure(agentId, statistic.Failure, elapsedMilliseconds);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves statistics for a specific spider.
    /// </summary>
    /// <param name="id">The spider identifier.</param>
    /// <returns>The spider statistics or null if not found.</returns>
    public Task<SpiderStatistic> GetSpiderStatisticAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            LogInvalidSpiderId();
            return Task.FromResult<SpiderStatistic>(null!);
        }

        var found = _spiderStatistics.TryGetValue(id, out var statistic);
        if (found && statistic != null)
        {
            LogStatisticRetrieved(id, statistic.Success, statistic.Failure, statistic.Total);
        }
        else
        {
            LogStatisticNotFound(id);
        }

        return Task.FromResult(statistic!);
    }

    /// <summary>
    /// Gets aggregated statistics for all spiders on this platform.
    /// </summary>
    /// <returns>Aggregated spider statistics summary.</returns>
    public PlatformStatisticsSummary GetPlatformSummary()
    {
        var allSpiders = _spiderStatistics.Values.ToList();
        var allAgents = _agentStatistics.Values.ToList();

        var summary = new PlatformStatisticsSummary
        {
            PlatformName = PlatformName,
            Timestamp = DateTime.UtcNow,
            TotalSpidersTracked = allSpiders.Count,
            TotalAgentsTracked = allAgents.Count,
            AggregatedTotalRequests = allSpiders.Sum(s => s.Total),
            AggregatedSuccessfulRequests = allSpiders.Sum(s => s.Success),
            AggregatedFailedRequests = allSpiders.Sum(s => s.Failure),
            AggregatedAgentSuccess = allAgents.Sum(a => a.Success),
            AggregatedAgentFailures = allAgents.Sum(a => a.Failure),
            AggregatedElapsedMilliseconds = allAgents.Sum(a => a.ElapsedMilliseconds),
            ActiveSpiders = allSpiders.Count(s => s.Start.HasValue && !s.Exit.HasValue),
            CompletedSpiders = allSpiders.Count(s => s.Exit.HasValue),
            SpiderDetails = allSpiders.Select(s => new SpiderDetail
            {
                Id = s.Id,
                Name = s.Name ?? "Unknown",
                Total = s.Total,
                Success = s.Success,
                Failure = s.Failure,
                SuccessRate = s.Total > 0 ? (double)s.Success / s.Total : 0,
                StartTime = s.Start,
                ExitTime = s.Exit,
                DurationMilliseconds = s.Exit.HasValue && s.Start.HasValue
                    ? (long)(s.Exit.Value - s.Start.Value).TotalMilliseconds
                    : null,
                LastModificationTime = s.LastModificationTime
            }).ToList(),
            AgentDetails = allAgents.Select(a => new AgentDetail
            {
                Id = a.Id,
                Name = a.Name ?? "Unknown",
                Success = a.Success,
                Failure = a.Failure,
                SuccessRate = (a.Success + a.Failure) > 0 ? (double)a.Success / (a.Success + a.Failure) : 0,
                TotalElapsedMilliseconds = a.ElapsedMilliseconds,
                AverageElapsedMilliseconds = (a.Success + a.Failure) > 0 ? a.ElapsedMilliseconds / (a.Success + a.Failure) : 0,
                LastModificationTime = a.LastModificationTime
            }).ToList()
        };

        LogPlatformSummaryGenerated(PlatformName, summary.TotalSpidersTracked, summary.TotalAgentsTracked,
            summary.AggregatedSuccessfulRequests, summary.AggregatedFailedRequests);

        return summary;
    }

    /// <summary>
    /// Computes a health status based on spider and agent statistics.
    /// </summary>
    /// <returns>A health status object suitable for monitoring integration.</returns>
    public HealthStatus ComputeHealthStatus()
    {
        var summary = GetPlatformSummary();
        var status = new HealthStatus
        {
            Platform = PlatformName,
            Timestamp = DateTime.UtcNow,
            Status = DetermineHealthStatus(summary),
            Summary = summary
        };

        LogHealthStatusComputed(PlatformName, status.Status);
        return status;
    }

    /// <summary>
    /// Clears all statistics for this store.
    /// </summary>
    public void Clear()
    {
        _spiderStatistics.Clear();
        _agentStatistics.Clear();
        LogStatisticsCleared();
    }

    /// <summary>
    /// Gets a snapshot of current spider statistics.
    /// </summary>
    public IReadOnlyDictionary<string, SpiderStatistic> GetSpiderSnapshot()
    {
        return _spiderStatistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Gets a snapshot of current agent statistics.
    /// </summary>
    public IReadOnlyDictionary<string, AgentStatistic> GetAgentSnapshot()
    {
        return _agentStatistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static string DetermineHealthStatus(PlatformStatisticsSummary summary)
    {
        // If no spiders or agents are tracked, status is unknown
        if (summary.TotalSpidersTracked == 0 && summary.TotalAgentsTracked == 0)
        {
            return "unknown";
        }

        // If all requests are failing, unhealthy
        var totalRequests = summary.AggregatedTotalRequests;
        if (totalRequests > 0 && summary.AggregatedSuccessfulRequests == 0)
        {
            return "unhealthy";
        }

        // Calculate failure rate
        if (totalRequests > 0)
        {
            var failureRate = (double)summary.AggregatedFailedRequests / totalRequests;
            
            // If failure rate > 50%, degraded
            if (failureRate > 0.5)
            {
                return "degraded";
            }

            // If failure rate > 10%, warning (still healthy)
            if (failureRate > 0.1)
            {
                return "healthy"; // Still operational but with some failures
            }
        }

        // For agent statistics
        var totalAgentRequests = summary.AggregatedAgentSuccess + summary.AggregatedAgentFailures;
        if (totalAgentRequests > 0)
        {
            var agentFailureRate = (double)summary.AggregatedAgentFailures / totalAgentRequests;
            if (agentFailureRate > 0.5)
            {
                return "degraded";
            }
        }

        return "healthy";
    }

    #region Logging Delegates

    private static readonly Action<ILogger, string, Exception?> LogStatisticsStoreInitializedAction =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2001, nameof(LogStatisticsStoreInitialized)),
            "DotnetSpider statistics store initialized for platform: {PlatformName}");

    private void LogStatisticsStoreInitialized(string platformName)
    {
        LogStatisticsStoreInitializedAction(_logger, platformName, null);
    }

    private static readonly Action<ILogger, Exception?> LogDatabaseEnsuredAction =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(2002, nameof(LogDatabaseEnsured)),
            "Database and table creation ensured (no-op for in-memory store)");

    private void LogDatabaseEnsured()
    {
        LogDatabaseEnsuredAction(_logger, null);
    }

    private static readonly Action<ILogger, Exception?> LogInvalidSpiderIdAction =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2003, nameof(LogInvalidSpiderId)),
            "Invalid spider ID provided");

    private void LogInvalidSpiderId()
    {
        LogInvalidSpiderIdAction(_logger, null);
    }

    private static readonly Action<ILogger, string, long, long, Exception?> LogTotalIncrementedAction =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Debug,
            new EventId(2004, nameof(LogTotalIncremented)),
            "Spider {SpiderId}: incremented total by {Count}, new total: {Total}");

    private void LogTotalIncremented(string spiderId, long count, long total)
    {
        LogTotalIncrementedAction(_logger, spiderId, count, total, null);
    }

    private static readonly Action<ILogger, string, long, long, Exception?> LogSuccessIncrementedAction =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Debug,
            new EventId(2005, nameof(LogSuccessIncremented)),
            "Spider {SpiderId}: success incremented, count: {SuccessCount}, total: {TotalCount}");

    private void LogSuccessIncremented(string spiderId, long successCount, long totalCount)
    {
        LogSuccessIncrementedAction(_logger, spiderId, successCount, totalCount, null);
    }

    private static readonly Action<ILogger, string, long, long, Exception?> LogFailureIncrementedAction =
        LoggerMessage.Define<string, long, long>(
            LogLevel.Debug,
            new EventId(2006, nameof(LogFailureIncremented)),
            "Spider {SpiderId}: failure incremented, count: {FailureCount}, total: {TotalCount}");

    private void LogFailureIncremented(string spiderId, long failureCount, long totalCount)
    {
        LogFailureIncrementedAction(_logger, spiderId, failureCount, totalCount, null);
    }

    private static readonly Action<ILogger, string, string, Exception?> LogSpiderStartedAction =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2007, nameof(LogSpiderStarted)),
            "Spider started - ID: {SpiderId}, Name: {SpiderName}");

    private void LogSpiderStarted(string spiderId, string spiderName)
    {
        LogSpiderStartedAction(_logger, spiderId, spiderName, null);
    }

    private static readonly Action<ILogger, string, string, double, Exception?> LogSpiderExitedAction =
        LoggerMessage.Define<string, string, double>(
            LogLevel.Information,
            new EventId(2008, nameof(LogSpiderExited)),
            "Spider exited - ID: {SpiderId}, Name: {SpiderName}, Duration: {DurationMs}ms");

    private void LogSpiderExited(string spiderId, string spiderName, double durationMs)
    {
        LogSpiderExitedAction(_logger, spiderId, spiderName, durationMs, null);
    }

    private static readonly Action<ILogger, Exception?> LogInvalidSpiderNameAction =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2009, nameof(LogInvalidSpiderName)),
            "Invalid spider name provided");

    private void LogInvalidSpiderName()
    {
        LogInvalidSpiderNameAction(_logger, null);
    }

    private static readonly Action<ILogger, Exception?> LogInvalidAgentIdAction =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2010, nameof(LogInvalidAgentId)),
            "Invalid agent ID provided");

    private void LogInvalidAgentId()
    {
        LogInvalidAgentIdAction(_logger, null);
    }

    private static readonly Action<ILogger, Exception?> LogInvalidAgentNameAction =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(2011, nameof(LogInvalidAgentName)),
            "Invalid agent name provided");

    private void LogInvalidAgentName()
    {
        LogInvalidAgentNameAction(_logger, null);
    }

    private static readonly Action<ILogger, string, string, Exception?> LogAgentRegisteredAction =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2012, nameof(LogAgentRegistered)),
            "Agent registered - ID: {AgentId}, Name: {AgentName}");

    private void LogAgentRegistered(string agentId, string agentName)
    {
        LogAgentRegisteredAction(_logger, agentId, agentName, null);
    }

    private static readonly Action<ILogger, string, long, int, Exception?> LogAgentSuccessAction =
        LoggerMessage.Define<string, long, int>(
            LogLevel.Debug,
            new EventId(2013, nameof(LogAgentSuccess)),
            "Agent {AgentId}: success incremented, count: {SuccessCount}, elapsed: {ElapsedMs}ms");

    private void LogAgentSuccess(string agentId, long successCount, int elapsedMs)
    {
        LogAgentSuccessAction(_logger, agentId, successCount, elapsedMs, null);
    }

    private static readonly Action<ILogger, string, long, int, Exception?> LogAgentFailureAction =
        LoggerMessage.Define<string, long, int>(
            LogLevel.Debug,
            new EventId(2014, nameof(LogAgentFailure)),
            "Agent {AgentId}: failure incremented, count: {FailureCount}, elapsed: {ElapsedMs}ms");

    private void LogAgentFailure(string agentId, long failureCount, int elapsedMs)
    {
        LogAgentFailureAction(_logger, agentId, failureCount, elapsedMs, null);
    }

    private static readonly Action<ILogger, string, long, long, long, Exception?> LogStatisticRetrievedAction =
        LoggerMessage.Define<string, long, long, long>(
            LogLevel.Debug,
            new EventId(2015, nameof(LogStatisticRetrieved)),
            "Spider statistic retrieved - ID: {SpiderId}, Success: {SuccessCount}, Failure: {FailureCount}, Total: {TotalCount}");

    private void LogStatisticRetrieved(string spiderId, long successCount, long failureCount, long totalCount)
    {
        LogStatisticRetrievedAction(_logger, spiderId, successCount, failureCount, totalCount, null);
    }

    private static readonly Action<ILogger, string, Exception?> LogStatisticNotFoundAction =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(2016, nameof(LogStatisticNotFound)),
            "Spider statistic not found - ID: {SpiderId}");

    private void LogStatisticNotFound(string spiderId)
    {
        LogStatisticNotFoundAction(_logger, spiderId, null);
    }

    private static readonly Action<ILogger, string, int, int, long, long, Exception?> LogPlatformSummaryGeneratedAction =
        LoggerMessage.Define<string, int, int, long, long>(
            LogLevel.Debug,
            new EventId(2017, nameof(LogPlatformSummaryGenerated)),
            "Platform summary generated - Platform: {PlatformName}, Spiders: {SpiderCount}, Agents: {AgentCount}, Success: {SuccessCount}, Failures: {FailureCount}");

    private void LogPlatformSummaryGenerated(string platformName, int spiderCount, int agentCount, long successCount, long failureCount)
    {
        LogPlatformSummaryGeneratedAction(_logger, platformName, spiderCount, agentCount, successCount, failureCount, null);
    }

    private static readonly Action<ILogger, string, string, Exception?> LogHealthStatusComputedAction =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2018, nameof(LogHealthStatusComputed)),
            "Health status computed - Platform: {PlatformName}, Status: {HealthStatus}");

    private void LogHealthStatusComputed(string platformName, string healthStatus)
    {
        LogHealthStatusComputedAction(_logger, platformName, healthStatus, null);
    }

    private static readonly Action<ILogger, Exception?> LogStatisticsClearedAction =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(2019, nameof(LogStatisticsCleared)),
            "All statistics cleared from store");

    private void LogStatisticsCleared()
    {
        LogStatisticsClearedAction(_logger, null);
    }

    #endregion
}

/// <summary>
/// Represents aggregated platform statistics across all spiders and agents.
/// </summary>
public sealed class PlatformStatisticsSummary
{
    /// <summary>
    /// Gets or sets the platform name.
    /// </summary>
    public string PlatformName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this summary was generated.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the total number of spiders tracked.
    /// </summary>
    public int TotalSpidersTracked { get; set; }

    /// <summary>
    /// Gets or sets the total number of agents tracked.
    /// </summary>
    public int TotalAgentsTracked { get; set; }

    /// <summary>
    /// Gets or sets the aggregated total requests across all spiders.
    /// </summary>
    public long AggregatedTotalRequests { get; set; }

    /// <summary>
    /// Gets or sets the aggregated successful requests across all spiders.
    /// </summary>
    public long AggregatedSuccessfulRequests { get; set; }

    /// <summary>
    /// Gets or sets the aggregated failed requests across all spiders.
    /// </summary>
    public long AggregatedFailedRequests { get; set; }

    /// <summary>
    /// Gets or sets the aggregated successful agent downloads.
    /// </summary>
    public long AggregatedAgentSuccess { get; set; }

    /// <summary>
    /// Gets or sets the aggregated failed agent downloads.
    /// </summary>
    public long AggregatedAgentFailures { get; set; }

    /// <summary>
    /// Gets or sets the aggregated elapsed time in milliseconds for all agent operations.
    /// </summary>
    public long AggregatedElapsedMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the number of currently active spiders.
    /// </summary>
    public int ActiveSpiders { get; set; }

    /// <summary>
    /// Gets or sets the number of completed spiders.
    /// </summary>
    public int CompletedSpiders { get; set; }

    /// <summary>
    /// Gets or sets detailed statistics for individual spiders.
    /// </summary>
    public List<SpiderDetail> SpiderDetails { get; set; } = new();

    /// <summary>
    /// Gets or sets detailed statistics for individual agents.
    /// </summary>
    public List<AgentDetail> AgentDetails { get; set; } = new();

    /// <summary>
    /// Gets the overall success rate across all spiders and agents.
    /// </summary>
    public double OverallSuccessRate
    {
        get
        {
            if (AggregatedTotalRequests == 0)
                return 0;
            return (double)AggregatedSuccessfulRequests / AggregatedTotalRequests;
        }
    }
}

/// <summary>
/// Represents detailed statistics for a single spider.
/// </summary>
public sealed class SpiderDetail
{
    /// <summary>
    /// Gets or sets the spider identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the spider name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of requests.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Gets or sets the number of successful requests.
    /// </summary>
    public long Success { get; set; }

    /// <summary>
    /// Gets or sets the number of failed requests.
    /// </summary>
    public long Failure { get; set; }

    /// <summary>
    /// Gets or sets the success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the spider start time.
    /// </summary>
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the spider exit time.
    /// </summary>
    public DateTimeOffset? ExitTime { get; set; }

    /// <summary>
    /// Gets or sets the total duration in milliseconds.
    /// </summary>
    public long? DurationMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the last modification time.
    /// </summary>
    public DateTimeOffset LastModificationTime { get; set; }
}

/// <summary>
/// Represents detailed statistics for a single agent.
/// </summary>
public sealed class AgentDetail
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of successful downloads.
    /// </summary>
    public long Success { get; set; }

    /// <summary>
    /// Gets or sets the number of failed downloads.
    /// </summary>
    public long Failure { get; set; }

    /// <summary>
    /// Gets or sets the success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the total elapsed time in milliseconds.
    /// </summary>
    public long TotalElapsedMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the average elapsed time per operation in milliseconds.
    /// </summary>
    public double AverageElapsedMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the last modification time.
    /// </summary>
    public DateTimeOffset LastModificationTime { get; set; }
}

/// <summary>
/// Represents platform health status for monitoring integration.
/// </summary>
public sealed class HealthStatus
{
    /// <summary>
    /// Gets or sets the platform name.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the health status ("healthy", "degraded", "unhealthy", or "unknown").
    /// </summary>
    public string Status { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the timestamp when this status was computed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the platform statistics summary.
    /// </summary>
    public PlatformStatisticsSummary Summary { get; set; } = new();
}
