using System.Globalization;
using WireMock.Admin.Mappings;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Ghost.Testing.Mocking.Profiles;

/// <summary>
/// WireMock profile for testing rate limiting scenarios (429 with Retry-After).
/// </summary>
public static class RateLimitProfile
{
    /// <summary>
    /// Configures the server to simulate rate limiting with 429 responses and Retry-After header.
    /// </summary>
    public static WireMockServer WithRateLimiting(
        this WireMockServer server,
        string path = "/api",
        int retryAfterSeconds = 60)
    {
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Rate-Limited", "true"))
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Retry-After", retryAfterSeconds.ToString(CultureInfo.InvariantCulture))
                .WithHeader("X-RateLimit-Limit", "100")
                .WithHeader("X-RateLimit-Remaining", "0")
                .WithHeader("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddSeconds(retryAfterSeconds).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
                .WithBody("{\"error\":\"Rate limit exceeded\"}"));

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("X-RateLimit-Limit", "100")
                .WithHeader("X-RateLimit-Remaining", "99")
                .WithBody("{\"status\":\"success\"}"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate progressive rate limiting (multiple 429s before success).
    /// </summary>
    public static WireMockServer WithProgressiveRateLimiting(
        this WireMockServer server,
        string path = "/progressive",
        int limitCount = 3)
    {
        for (int i = 0; i < limitCount; i++)
        {
            server
                .Given(Request.Create()
                    .WithPath(path)
                    .UsingGet()
                    .WithHeader("X-Attempt", i.ToString(CultureInfo.InvariantCulture)))
                .RespondWith(Response.Create()
                    .WithStatusCode(429)
                    .WithHeader("Retry-After", (30 * (i + 1)).ToString(CultureInfo.InvariantCulture))
                    .WithHeader("X-RateLimit-Remaining", "0")
                    .WithBody($"{{\"error\":\"Rate limited (attempt {i + 1})\"}}"));
        }

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Attempt", limitCount.ToString(CultureInfo.InvariantCulture)))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("X-RateLimit-Remaining", "50")
                .WithBody("{\"status\":\"success\"}"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate rate limiting with Retry-After as HTTP date.
    /// </summary>
    public static WireMockServer WithDateBasedRateLimiting(
        this WireMockServer server,
        string path = "/date-limit",
        int delaySeconds = 120)
    {
        var retryAfterDate = DateTimeOffset.UtcNow.AddSeconds(delaySeconds).ToString("R");

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Rate-Limited", "true"))
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Retry-After", retryAfterDate)
                .WithBody("{\"error\":\"Rate limit exceeded\",\"retry_after\":\"" + retryAfterDate + "\"}"));

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{\"status\":\"success\"}"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate per-endpoint rate limits with different quotas.
    /// </summary>
    public static WireMockServer WithPerEndpointLimits(this WireMockServer server)
    {
        // Strict limit endpoint: 10 requests
        server
            .Given(Request.Create()
                .WithPath("/strict")
                .UsingGet()
                .WithHeader("X-Over-Limit", "true"))
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("X-RateLimit-Limit", "10")
                .WithHeader("X-RateLimit-Remaining", "0")
                .WithHeader("Retry-After", "300")
                .WithBody("{\"error\":\"Strict rate limit exceeded\"}"));

        // Lenient limit endpoint: 1000 requests
        server
            .Given(Request.Create()
                .WithPath("/lenient")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("X-RateLimit-Limit", "1000")
                .WithHeader("X-RateLimit-Remaining", "999")
                .WithBody("{\"status\":\"success\"}"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate burst rate limiting (quota reset after time window).
    /// </summary>
    public static WireMockServer WithBurstRateLimiting(
        this WireMockServer server,
        string path = "/burst",
        int burstSize = 5)
    {
        for (int i = 0; i < burstSize; i++)
        {
            server
                .Given(Request.Create()
                    .WithPath(path)
                    .UsingGet()
                    .WithHeader("X-Burst-Count", i.ToString(CultureInfo.InvariantCulture)))
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("X-RateLimit-Remaining", (burstSize - i - 1).ToString(CultureInfo.InvariantCulture))
                    .WithBody($"{{\"request\":{i + 1}}}"));
        }

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Burst-Count", burstSize.ToString(CultureInfo.InvariantCulture)))
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("X-RateLimit-Remaining", "0")
                .WithHeader("Retry-After", "60")
                .WithBody("{\"error\":\"Burst limit exceeded\"}"));

        return server;
    }
}
