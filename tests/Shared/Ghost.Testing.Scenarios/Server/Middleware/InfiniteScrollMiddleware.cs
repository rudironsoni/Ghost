using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Server.Middleware;

/// <summary>
/// Middleware that adds scroll-related headers and state.
/// </summary>
public sealed class InfiniteScrollMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InfiniteScrollMiddleware> _logger;

    public InfiniteScrollMiddleware(RequestDelegate next, ILogger<InfiniteScrollMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add scroll state to context
        if (context.Request.Path.StartsWithSegments("/scenario/scroll"))
        {
            var offset = int.TryParse(context.Request.Query["offset"], out var o) ? o : 0;
            var limit = int.TryParse(context.Request.Query["limit"], out var l) ? l : 20;

            context.Items["ScrollOffset"] = offset;
            context.Items["ScrollLimit"] = limit;

            _logger.LogDebug("Scroll request with offset={Offset}, limit={Limit}", offset, limit);
        }

        await _next(context);
    }
}
