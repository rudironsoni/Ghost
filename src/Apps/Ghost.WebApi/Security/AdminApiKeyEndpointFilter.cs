using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Ghost.WebApi.Security;

public sealed class AdminApiKeyEndpointFilter : IEndpointFilter
{
    private static readonly Action<ILogger, Exception?> MissingAdminApiKeyConfigurationLog =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(6101, nameof(MissingAdminApiKeyConfigurationLog)),
            "Admin API key auth enabled but no key configured");

    private readonly IOptionsMonitor<AdminApiKeyOptions> _options;
    private readonly ILogger<AdminApiKeyEndpointFilter> _logger;

    public AdminApiKeyEndpointFilter(IOptionsMonitor<AdminApiKeyOptions> options, ILogger<AdminApiKeyEndpointFilter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        AdminApiKeyOptions options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return await next(context).ConfigureAwait(false);
        }

        string? expectedApiKey = options.ApiKey;
        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            expectedApiKey = Environment.GetEnvironmentVariable("GHOST_ADMIN_API_KEY");
        }

        if (string.IsNullOrWhiteSpace(expectedApiKey))
        {
            MissingAdminApiKeyConfigurationLog(_logger, null);
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Admin endpoint unavailable",
                detail: "Admin API key is not configured.");
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(options.HeaderName, out StringValues providedKey))
        {
            return Results.Unauthorized();
        }

        if (!string.Equals(providedKey.ToString(), expectedApiKey, StringComparison.Ordinal))
        {
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }
}
