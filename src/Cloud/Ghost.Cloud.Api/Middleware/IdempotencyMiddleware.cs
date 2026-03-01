using System.Security.Cryptography;
using System.Text;
using Ghost.Cloud.Infrastructure.Idempotency;

namespace Ghost.Cloud.Api.Middleware;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;
    private readonly IIdempotencyService _idempotencyService;
    private static readonly TimeSpan s_defaultTtl = TimeSpan.FromHours(24);

    public IdempotencyMiddleware(
        RequestDelegate next,
        ILogger<IdempotencyMiddleware> logger,
        IIdempotencyService idempotencyService)
    {
        _next = next;
        _logger = logger;
        _idempotencyService = idempotencyService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply idempotency to POST requests
        if (context.Request.Method != HttpMethods.Post)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Check for idempotency key header
        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out Microsoft.Extensions.Primitives.StringValues keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            // No idempotency key, proceed normally
            await _next(context).ConfigureAwait(false);
            return;
        }

        string idempotencyKey = keyValues.ToString();

        // Generate a unique key combining the idempotency key and the request path
        string compositeKey = $"{context.Request.Path}:{idempotencyKey}";
        string hashedKey = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(compositeKey)));

        // Check if we have a stored response for this key
        string? existingRunId = await _idempotencyService.GetRunIdAsync(hashedKey).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(existingRunId))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new { RunId = existingRunId, Status = "Cached" }).ConfigureAwait(false);
            return;
        }

        // Store the idempotency key before processing
        // We use a temporary RunId that will be updated with the actual one after processing
        string tempRunId = Guid.NewGuid().ToString("N");
        IdempotencyCheckResult checkResult = await _idempotencyService.CheckAndStoreAsync(
            hashedKey, tempRunId, s_defaultTtl).ConfigureAwait(false);

        if (!checkResult.IsNew && !checkResult.IsExpired)
        {
            // Another request is processing with the same key
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                Error = "A request with the same idempotency key is already being processed",
                RunId = checkResult.ExistingRunId
            }).ConfigureAwait(false);
            return;
        }

        // Store the idempotency key in the context for later use
        context.Items["IdempotencyKey"] = hashedKey;
        context.Items["OriginalIdempotencyKey"] = idempotencyKey;

        await _next(context).ConfigureAwait(false);
    }
}

public static class IdempotencyMiddlewareExtensions
{
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IdempotencyMiddleware>();
    }
}
