using System.Globalization;
using Ghost.Core;
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
        var group = app.MapGroup("/api/admin/dlq")
            .WithTags("Admin");

        group.MapGet(string.Empty, GetJobs)
            .WithName("GetDlqJobs")
            .AllowAnonymous();

        group.MapGet("/stats", GetStats)
            .WithName("GetDlqStats")
            .AllowAnonymous();

        group.MapPost("/clear", ClearQueue)
            .WithName("ClearDlq")
            .AllowAnonymous();
    }

    private static async Task<IResult> GetJobs(
        [FromQuery] int count,
        [FromServices] IGenericDeadLetterQueue dlq)
    {
        count = count > 0 ? Math.Min(count, 100) : 10;
        var items = await dlq.PeekAsync(count).ConfigureAwait(false);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetStats([FromServices] IGenericDeadLetterQueue dlq)
    {
        var depth = await dlq.GetCountAsync().ConfigureAwait(false);
        return Results.Ok(new { ActiveCount = depth, Timestamp = DateTime.UtcNow });
    }

    private static async Task<IResult> ClearQueue([FromServices] IGenericDeadLetterQueue dlq)
    {
        await dlq.ClearAsync().ConfigureAwait(false);
        return Results.Ok(new { Message = "DLQ cleared successfully" });
    }
}
