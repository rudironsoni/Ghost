using System.Globalization;
using System.Text.RegularExpressions;
using Ghost.Resilience;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ghost.WebApi.Features.Admin;

/// <summary>
/// Admin endpoints for the dead letter queue.
/// </summary>
public static class DlqEndpoints
{
    private static readonly Action<ILogger, string, Exception?> s_logRetryAll =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(RetryAll)), "Retrying DLQ jobs since {Since}");

    private static readonly Action<ILogger, string, Exception?> s_logArchiveAll =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, nameof(ArchiveAll)), "Archiving DLQ jobs older than {OlderThan}");

    /// <summary>
    /// Maps admin DLQ endpoints under /api/admin/dlq.
    /// </summary>
    /// <param name="app">Endpoint route builder.</param>
    public static void MapDlqEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/dlq")
            .WithTags("Admin");

        group.MapGet(string.Empty, GetJobs)
            .WithName("GetDlqJobs")
            .AllowAnonymous();

        group.MapGet("/{id}", GetJob)
            .WithName("GetDlqJob")
            .AllowAnonymous();

        group.MapPost("/{id}/retry", RetryJob)
            .WithName("RetryDlqJob")
            .AllowAnonymous();

        group.MapPost("/retry-all", RetryAll)
            .WithName("RetryAllDlqJobs")
            .AllowAnonymous();

        group.MapPost("/archive-all", ArchiveAll)
            .WithName("ArchiveAllDlqJobs")
            .AllowAnonymous();

        group.MapGet("/stats", GetStats)
            .WithName("GetDlqStats")
            .AllowAnonymous();
    }

    private static async Task<IResult> GetJobs(
        [FromQuery] string? since,
        [FromQuery] string? platform,
        [FromServices] IDeadLetterQueue dlq)
    {
        var sinceWindow = ParseDurationOrDefault(since, TimeSpan.FromHours(24));
        if (sinceWindow is null)
        {
            return Results.BadRequest("Invalid 'since' format. Use values like 24h or 7d.");
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            var byPlatform = await dlq.GetFailedJobsByPlatformAsync(platform, sinceWindow.Value).ConfigureAwait(false);
            return Results.Ok(byPlatform);
        }

        var jobs = await dlq.GetFailedJobsAsync(sinceWindow.Value).ConfigureAwait(false);
        return Results.Ok(jobs);
    }

    private static async Task<IResult> GetJob(
        string id,
        [FromServices] IDeadLetterQueue dlq)
    {
        var job = await dlq.GetJobAsync(id).ConfigureAwait(false);
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static async Task<IResult> RetryJob(
        string id,
        [FromServices] IDeadLetterQueue dlq)
    {
        try
        {
            await dlq.RetryAsync(id).ConfigureAwait(false);
            return Results.Accepted();
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(ex.Message);
        }
    }

    private static async Task<IResult> RetryAll(
        [FromQuery] string? since,
        [FromServices] IDeadLetterQueue dlq,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var sinceWindow = ParseDurationOrDefault(since, TimeSpan.FromHours(24));
        if (sinceWindow is null)
        {
            return Results.BadRequest("Invalid 'since' format. Use values like 24h or 7d.");
        }

        var logger = loggerFactory.CreateLogger("DlqEndpoints");
        s_logRetryAll(logger, sinceWindow.Value.ToString(), null);
        await dlq.RetryAllAsync(sinceWindow.Value).ConfigureAwait(false);
        return Results.Accepted();
    }

    private static async Task<IResult> ArchiveAll(
        [FromQuery] string? olderThan,
        [FromServices] IDeadLetterQueue dlq,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var window = ParseDurationOrDefault(olderThan, TimeSpan.FromDays(7));
        if (window is null)
        {
            return Results.BadRequest("Invalid 'olderThan' format. Use values like 7d or 24h.");
        }

        var logger = loggerFactory.CreateLogger("DlqEndpoints");
        s_logArchiveAll(logger, window.Value.ToString(), null);
        await dlq.ArchiveAllAsync(window.Value).ConfigureAwait(false);
        return Results.Accepted();
    }

    private static async Task<IResult> GetStats([FromServices] IDeadLetterQueue dlq)
    {
        var depth = await dlq.GetQueueDepthAsync().ConfigureAwait(false);
        return Results.Ok(new DlqStatsResponse { ActiveCount = depth, Timestamp = DateTime.UtcNow });
    }

    private static TimeSpan? ParseDurationOrDefault(string? input, TimeSpan defaultValue)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        return ParseDuration(input);
    }

    private static TimeSpan? ParseDuration(string input)
    {
        var trimmed = input.Trim();
        var match = Regex.Match(trimmed, "^(?<value>\\d+)(?<unit>[smhdw])$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        if (!int.TryParse(match.Groups["value"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        return unit switch
        {
            "s" => TimeSpan.FromSeconds(value),
            "m" => TimeSpan.FromMinutes(value),
            "h" => TimeSpan.FromHours(value),
            "d" => TimeSpan.FromDays(value),
            "w" => TimeSpan.FromDays(value * 7),
            _ => null
        };
    }

    private sealed class DlqStatsResponse
    {
        public int ActiveCount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
