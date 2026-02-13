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
        var path = context.Request.Path.Value ?? string.Empty;
        var isStatefulScenario = path.Contains("/scenario/consent/stateful-persistence", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/scenario/consent/reconsent-policy-change", StringComparison.OrdinalIgnoreCase);

        // Most consent scenarios should always render a first-visit experience.
        // Only stateful/re-consent scenarios should honor persisted consent cookies.
        var hasConsent = isStatefulScenario
            && context.Request.Cookies.TryGetValue("ghost_consent", out var consentValue)
            && !string.IsNullOrWhiteSpace(consentValue);

        // Add consent state to items for downstream handlers
        context.Items["HasConsent"] = hasConsent;

        if (hasConsent)
        {
            _logger.LogDebug("Request has consent cookie");
        }

        await _next(context);
    }
}
