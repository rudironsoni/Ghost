using System.Globalization;
using Ghost.Kernel;
using Ghost.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ghost.WebApi.Features.Admin;

/// <summary>
/// Admin endpoints for the dead letter queue.
/// Simplified version for ULTRA MISER MODE - core functionality only.
/// </summary>
public static class DlqEndpoints
{
    /// <summary>
    /// Maps admin DLQ endpoints under /api/admin/dlq.
    /// </summary>
    public static void MapDlqEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/admin/dlq")
            .WithTags("Admin");

        group.MapGet(string.Empty, GetJobsAsync)
            .WithName("GetDlqJobs")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();

        group.MapGet("/stats", GetStatsAsync)
            .WithName("GetDlqStats")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();

        group.MapPost("/clear", ClearQueueAsync)
            .WithName("ClearDlq")
            .AddEndpointFilter<AdminApiKeyEndpointFilter>();
    }

    private static async Task<IResult> GetJobsAsync(
        [FromQuery] int count,
        [FromServices] IGenericDeadLetterQueue dlq)
    {
        count = count > 0 ? Math.Min(count, 100) : 10;
        List<DeadLetterItem> items = await dlq.PeekAsync(count).ConfigureAwait(false);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetStatsAsync([FromServices] IGenericDeadLetterQueue dlq)
    {
        int depth = await dlq.GetCountAsync().ConfigureAwait(false);
        return Results.Ok(new { ActiveCount = depth, Timestamp = DateTime.UtcNow });
    }

    private static async Task<IResult> ClearQueueAsync([FromServices] IGenericDeadLetterQueue dlq)
    {
        await dlq.ClearAsync().ConfigureAwait(false);
        return Results.Ok(new { Message = "DLQ cleared successfully" });
    }
}
