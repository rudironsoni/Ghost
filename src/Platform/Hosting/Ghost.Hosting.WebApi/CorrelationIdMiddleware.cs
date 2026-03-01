using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ghost.Hosting.WebApi;

/// <summary>
/// Logger messages for CorrelationIdMiddleware.
/// </summary>
public static partial class CorrelationIdMiddlewareLogMessages
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing request with correlation ID: {CorrelationId}")]
    public static partial void ProcessingRequest(this ILogger logger, string correlationId);
}

/// <summary>
/// Middleware for correlation ID propagation.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Processes the HTTP request and ensures correlation ID propagation.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Extract or generate correlation ID
        string correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // Add to response headers
        context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);

        // Add to current activity (OpenTelemetry span)
        Activity? activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("correlation.id", correlationId);
            activity.SetBaggage("correlation.id", correlationId);
        }

        // Add to logger scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.ProcessingRequest(correlationId);
        }
            await _next(context).ConfigureAwait(false);
        }
    }
}
