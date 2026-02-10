using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Server.Middleware;

/// <summary>
/// Middleware that manages consent state and cookies.
/// </summary>
public sealed class ConsentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ConsentMiddleware> _logger;

    public ConsentMiddleware(RequestDelegate next, ILogger<ConsentMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check for consent cookie
        var hasConsent = context.Request.Cookies.ContainsKey("ghost_consent");

        // Add consent state to items for downstream handlers
        context.Items["HasConsent"] = hasConsent;

        if (hasConsent)
        {
            _logger.LogDebug("Request has consent cookie");
        }

        await _next(context);
    }
}
