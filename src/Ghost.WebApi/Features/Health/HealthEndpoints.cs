using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ghost.WebApi.Features.Health;

public static class HealthEndpoints
{
    private static readonly Action<ILogger, Exception?> LogHealthCheckStarting =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(LogHealthCheckStarting)), "Starting jobs health check");

    private static readonly Action<ILogger, string, Exception?> LogHealthCheckCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, nameof(LogHealthCheckCompleted)), "Jobs health check completed. Overall status: {Status}");

    private static readonly Action<ILogger, Exception?> LogHealthCheckFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(3, nameof(LogHealthCheckFailed)), "Jobs health check failed with exception");

    private static readonly Action<ILogger, string, Exception?> LogPlatformHealthTest =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(4, nameof(LogPlatformHealthTest)), "Testing {Platform} health");

    private static readonly Action<ILogger, string, int, double, Exception?> LogPlatformHealthPassed =
        LoggerMessage.Define<string, int, double>(LogLevel.Debug, new EventId(5, nameof(LogPlatformHealthPassed)), "{Platform} health check passed: {Count} jobs found in {Duration}ms");

    private static readonly Action<ILogger, string, double, Exception?> LogPlatformHealthDegraded =
        LoggerMessage.Define<string, double>(LogLevel.Warning, new EventId(6, nameof(LogPlatformHealthDegraded)), "{Platform} health check degraded: no jobs found in {Duration}ms");

    private static readonly Action<ILogger, string, Exception?> LogPlatformHealthFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(7, nameof(LogPlatformHealthFailed)), "{Platform} health check failed");

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs");
        group.MapGet("/health", CheckJobsHealth);
    }

    private static async Task<IResult> CheckJobsHealth(
        [FromServices] IJobClient jobClient,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var healthStatus = new JobsHealthStatus
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = "healthy",
            Platforms = new Dictionary<string, PlatformHealthStatus>()
        };

        // Create a logger from the injected factory so DI doesn't require ILogger directly
        var logger = loggerFactory.CreateLogger("Health");

        // Test each platform with a simple search query
        var testCriteria = new JobSearchCriteria
        {
            Query = "test",
            Location = "Remote",
            MaxResults = 1
        };

        try
        {
            LogHealthCheckStarting(logger, null);

            // Test Google Jobs if enabled
            if (IsPlatformEnabled("Google"))
            {
                var googleStatus = await TestPlatformHealthAsync(
                    "Google",
                    testCriteria with { Sources = new List<string> { "Google" } },
                    jobClient,
                    loggerFactory,
                    ct);
                healthStatus.Platforms["Google"] = googleStatus;
            }

            // Test Glassdoor if enabled
            if (IsPlatformEnabled("Glassdoor"))
            {
                var glassdoorStatus = await TestPlatformHealthAsync(
                    "Glassdoor",
                    testCriteria with { Sources = new List<string> { "Glassdoor" } },
                    jobClient,
                    loggerFactory,
                    ct);
                healthStatus.Platforms["Glassdoor"] = glassdoorStatus;
            }

            // Test LinkedIn if enabled
            if (IsPlatformEnabled("LinkedIn"))
            {
                var linkedinStatus = await TestPlatformHealthAsync(
                    "LinkedIn",
                    testCriteria with { Sources = new List<string> { "LinkedIn" } },
                    jobClient,
                    loggerFactory,
                    ct);
                healthStatus.Platforms["LinkedIn"] = linkedinStatus;
            }

            // Test Indeed if enabled
            if (IsPlatformEnabled("Indeed"))
            {
                var indeedStatus = await TestPlatformHealthAsync(
                    "Indeed",
                    testCriteria with { Sources = new List<string> { "Indeed" } },
                    jobClient,
                    loggerFactory,
                    ct);
                healthStatus.Platforms["Indeed"] = indeedStatus;
            }

            // Determine overall status
            healthStatus.OverallStatus = DetermineOverallStatus(healthStatus.Platforms);

            LogHealthCheckCompleted(logger, healthStatus.OverallStatus, null);

            return Results.Ok(healthStatus);
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(logger, ex);
            healthStatus.OverallStatus = "unhealthy";
            healthStatus.Error = ex.Message;
            return Results.Ok(healthStatus); // Return 200 with health status details
        }
    }

    private static async Task<PlatformHealthStatus> TestPlatformHealthAsync(
        string platformName,
        JobSearchCriteria criteria,
        [FromServices] IJobClient jobClient,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var status = new PlatformHealthStatus
        {
            Platform = platformName,
            Status = "unknown",
            LastChecked = DateTime.UtcNow
        };

        // Create a logger from the injected factory so DI doesn't require ILogger directly
        var logger = loggerFactory.CreateLogger("Health");

        try
        {
            LogPlatformHealthTest(logger, platformName, null);

            var startTime = DateTime.UtcNow;
            var result = await jobClient.SearchJobsAsync(criteria, ct);
            var duration = DateTime.UtcNow - startTime;

            status.ResponseTimeMs = (int)duration.TotalMilliseconds;
            status.LastSuccessfulSearch = DateTime.UtcNow;

            if (result?.Count > 0)
            {
                status.Status = "healthy";
                status.JobsFound = result.Count;
                status.Message = $"Successfully found {result.Count} jobs";
                LogPlatformHealthPassed(logger, platformName, result.Count, duration.TotalMilliseconds, null);
            }
            else
            {
                status.Status = "degraded";
                status.JobsFound = 0;
                status.Message = "Platform responded but returned no jobs";
                LogPlatformHealthDegraded(logger, platformName, duration.TotalMilliseconds, null);
            }
        }
        catch (Exception ex)
        {
            status.Status = "unhealthy";
            status.Message = ex.Message;
            status.Error = ex.ToString();
            LogPlatformHealthFailed(logger, platformName, ex);
        }

        return status;
    }

    private static bool IsPlatformEnabled(string platformName)
    {
        // This would typically check configuration, but for now we'll assume they're enabled
        // In a real implementation, you'd check the configuration
        return true;
    }

    private static string DetermineOverallStatus(Dictionary<string, PlatformHealthStatus> platforms)
    {
        if (platforms.Count == 0)
            return "unknown";

        var hasUnhealthy = platforms.Values.Any(p => p.Status == "unhealthy");
        var hasDegraded = platforms.Values.Any(p => p.Status == "degraded");
        var allHealthy = platforms.Values.All(p => p.Status == "healthy");

        return hasUnhealthy ? "unhealthy" :
               hasDegraded ? "degraded" :
               allHealthy ? "healthy" : "unknown";
    }
}

public class JobsHealthStatus
{
    public string OverallStatus { get; set; } = "unknown";
    public DateTime Timestamp { get; set; }
    public Dictionary<string, PlatformHealthStatus> Platforms { get; set; } = new();
    public string? Error { get; set; }
}

public class PlatformHealthStatus
{
    public string Platform { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown";
    public string Message { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
    public DateTime? LastSuccessfulSearch { get; set; }
    public int ResponseTimeMs { get; set; }
    public int JobsFound { get; set; }
    public string? Error { get; set; }
}
