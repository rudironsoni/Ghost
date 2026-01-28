using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ghost.Hosting.WebApi;

/// <summary>
/// Endpoint route builder extensions for Ghost endpoints.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Map a simple health check endpoint used to indicate Ghost readiness.
    /// </summary>
    /// <param name="endpoints">Endpoint route builder.</param>
    /// <param name="pattern">Route pattern for the health check.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGhostHealthCheck(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/health/ghost")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(pattern, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"status\":\"ok\"}");
        }).WithName("GhostHealth");

        return endpoints;
    }
}
