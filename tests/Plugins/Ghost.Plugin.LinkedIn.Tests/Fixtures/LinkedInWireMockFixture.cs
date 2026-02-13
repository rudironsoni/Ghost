using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests.Fixtures;

public sealed class LinkedInWireMockFixture : IAsyncLifetime, IDisposable
{
    private WireMockServer? _server;

    public string BaseUrl => _server?.Urls[0] ?? throw new InvalidOperationException("WireMock server not started");

    public Task InitializeAsync()
    {
        _server = WireMockServer.Start();

        _server
            .Given(Request.Create().WithPath("/linkedin/jobs/search").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"jobs\":[{\"id\":\"li-1\",\"title\":\"Software Engineer\",\"company\":\"Ghost\"}]}"));

        _server
            .Given(Request.Create().WithPath("/linkedin/jobs/rate-limited").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Retry-After", "2"));

        _server
            .Given(Request.Create().WithPath("/linkedin/jobs/unauthorized").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401));

        _server
            .Given(Request.Create().WithPath("/linkedin/jobs/error").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500));

        _server
            .Given(Request.Create().WithPath("/linkedin/jobs/malformed").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{not-json}"));

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _server?.Stop();
        _server?.Dispose();
        _server = null;
    }
}
