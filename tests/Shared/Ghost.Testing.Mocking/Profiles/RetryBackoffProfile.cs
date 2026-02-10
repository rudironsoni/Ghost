using System.Globalization;
using WireMock.Admin.Mappings;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Ghost.Testing.Mocking.Profiles;

/// <summary>
/// WireMock profile for testing retry and exponential backoff scenarios.
/// </summary>
public static class RetryBackoffProfile
{
    /// <summary>
    /// Configures the server to simulate exponential backoff delays (1s, 2s, 4s, 8s).
    /// </summary>
    public static WireMockServer WithExponentialBackoff(this WireMockServer server, string path = "/backoff")
    {
        // First request: 1 second delay
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Retry-Count", "0"))
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithHeader("Retry-After", "1")
                .WithBody("Service temporarily unavailable")
                .WithDelay(TimeSpan.FromSeconds(1)));

        // Second request: 2 second delay
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Retry-Count", "1"))
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithHeader("Retry-After", "2")
                .WithBody("Service temporarily unavailable")
                .WithDelay(TimeSpan.FromSeconds(2)));

        // Third request: 4 second delay
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Retry-Count", "2"))
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithHeader("Retry-After", "4")
                .WithBody("Service temporarily unavailable")
                .WithDelay(TimeSpan.FromSeconds(4)));

        // Fourth request: success
        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Retry-Count", "3"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Success after retries"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate transient failures that succeed after a specified number of retries.
    /// </summary>
    public static WireMockServer WithTransientFailures(
        this WireMockServer server,
        string path = "/transient",
        int failureCount = 2,
        int statusCode = 503)
    {
        for (int i = 0; i < failureCount; i++)
        {
            server
                .Given(Request.Create()
                    .WithPath(path)
                    .UsingGet()
                    .WithHeader("X-Request-Count", i.ToString(CultureInfo.InvariantCulture)))
                .RespondWith(Response.Create()
                    .WithStatusCode(statusCode)
                    .WithBody($"Transient failure {i + 1}"));
        }

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Request-Count", failureCount.ToString(CultureInfo.InvariantCulture)))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Success"));

        return server;
    }

    /// <summary>
    /// Configures the server to simulate jittered exponential backoff with randomized delays.
    /// </summary>
    public static WireMockServer WithJitteredBackoff(
        this WireMockServer server,
        string path = "/jittered",
        int attempts = 3)
    {
        var random = new Random(42); // Fixed seed for deterministic tests

        for (int i = 0; i < attempts; i++)
        {
            var baseDelay = Math.Pow(2, i);
            var jitter = random.NextDouble() * 0.5; // 0-50% jitter
            var totalDelay = baseDelay * (1 + jitter);

            server
                .Given(Request.Create()
                    .WithPath(path)
                    .UsingGet()
                    .WithHeader("X-Attempt", i.ToString(CultureInfo.InvariantCulture)))
                .RespondWith(Response.Create()
                    .WithStatusCode(503)
                    .WithHeader("Retry-After", ((int)totalDelay).ToString(CultureInfo.InvariantCulture))
                    .WithDelay(TimeSpan.FromSeconds(totalDelay))
                    .WithBody($"Attempt {i + 1} failed"));
        }

        server
            .Given(Request.Create()
                .WithPath(path)
                .UsingGet()
                .WithHeader("X-Attempt", attempts.ToString(CultureInfo.InvariantCulture)))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("Success after jittered backoff"));

        return server;
    }
}
