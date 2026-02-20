using System.Security.Claims;

namespace Ghost.Cloud.Api.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private const string TenantIdHeader = "X-Tenant-Id";

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get tenant ID from header first
        if (context.Request.Headers.TryGetValue(TenantIdHeader, out Microsoft.Extensions.Primitives.StringValues headerValue) &&
            Guid.TryParse(headerValue.ToString(), out Guid tenantId) &&
            tenantId != Guid.Empty)
        {
            context.Items["TenantId"] = tenantId;
        }
        // Otherwise try to get from authenticated user claim
        else if (context.User.Identity?.IsAuthenticated == true)
        {
            Claim? tenantClaim = context.User.FindFirst("tenant_id");
            if (tenantClaim != null &&
                Guid.TryParse(tenantClaim.Value, out tenantId) &&
                tenantId != Guid.Empty)
            {
                context.Items["TenantId"] = tenantId;
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }

    public static Guid GetTenantId(this HttpContext context)
    {
        if (context.Items.TryGetValue("TenantId", out object? value) &&
            value is Guid tenantId &&
            tenantId != Guid.Empty)
        {
            return tenantId;
        }

        throw new InvalidOperationException("TenantId was not resolved for this request.");
    }

    public static bool TryGetTenantId(this HttpContext context, out Guid tenantId)
    {
        if (context.Items.TryGetValue("TenantId", out object? value) &&
            value is Guid resolvedTenantId &&
            resolvedTenantId != Guid.Empty)
        {
            tenantId = resolvedTenantId;
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }
}
