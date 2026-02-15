using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Server.Middleware;

/// <summary>
/// Middleware that handles pagination state.
/// </summary>
public sealed class PaginationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PaginationMiddleware> _logger;

    public PaginationMiddleware(RequestDelegate next, ILogger<PaginationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add pagination state to context
        if (context.Request.Path.StartsWithSegments("/scenario/pagination"))
        {
            int page = int.TryParse(context.Request.Query["page"], out int p) ? p : 1;
            int pageSize = int.TryParse(context.Request.Query["pageSize"], out int ps) ? ps : 10;
            string cursor = context.Request.Query["cursor"].ToString();

            context.Items["Page"] = page;
            context.Items["PageSize"] = pageSize;
            context.Items["Cursor"] = cursor;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Pagination request with page={Page}, pageSize={PageSize}, cursor={Cursor}",
                    page, pageSize, cursor);
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
