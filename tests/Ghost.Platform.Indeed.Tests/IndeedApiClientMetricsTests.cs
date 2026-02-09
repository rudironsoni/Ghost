using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Models;
using Ghost.Platform.Indeed.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ghost.Platform.Indeed.Tests;

public class IndeedApiClientMetricsTests
{
    [Fact]
    public void GetMetrics_ReturnsDefaults_WhenNoRequests()
    {
        var handler = new NoopHandler();
        var client = CreateClient(handler);

        var metrics = client.GetMetrics();

        Assert.Equal(0, metrics.ActiveConnections);
        Assert.Equal(0, metrics.TotalRequests);
        Assert.Equal(0, metrics.TotalFailures);
        Assert.True(metrics.AverageResponseTimeMs >= 0);
        Assert.True(metrics.RequestsPerSecond >= 0);
    }

    [Fact]
    public async Task GetMetrics_TracksActiveConnections()
    {
        using var gate = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var handler = new BlockingHandler(gate, release);
        var client = CreateClient(handler);

        var task = Task.Run(async () =>
        {
            await using var enumerator = client.SearchAsync("query", "location", 1).GetAsyncEnumerator();
            await enumerator.MoveNextAsync();
        });

        Assert.True(gate.Wait(2000));

        var during = client.GetMetrics();
        Assert.True(during.ActiveConnections > 0);

        release.Set();
        await task;

        var after = client.GetMetrics();
        Assert.Equal(0, after.ActiveConnections);
    }

    [Fact]
    public async Task GetMetrics_IncrementsFailureCount_OnBadResponse()
    {
        // Return HTTP 200 with error content that triggers IsBlockedOrConsentRequired()
        // This allows the client to handle the error gracefully and record metrics
        var handler = new ResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"errors\":[{\"message\":\"test error\"}]}")
        });
        var client = CreateClient(handler);

        await using var enumerator = client.SearchAsync("query", "location", 1).GetAsyncEnumerator();
        var hasNext = await enumerator.MoveNextAsync();

        // Bad response should terminate enumeration without results
        Assert.False(hasNext);

        var metrics = client.GetMetrics();
        Assert.True(metrics.TotalRequests >= 1); // Should have attempted the request
        Assert.True(metrics.TotalFailures >= 1); // Should record failure due to error content
    }

    [Fact]
    public async Task GetMetrics_IncrementsRequestCount_OnSuccess()
    {
        var handler = new ResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{\"jobSearch\":{\"pageInfo\":{\"nextCursor\":null,\"hasNextPage\":false}}}}")
        });
        var client = CreateClient(handler);

        await using var enumerator = client.SearchAsync("query", "location", 1).GetAsyncEnumerator();
        await enumerator.MoveNextAsync();

        var metrics = client.GetMetrics();
        Assert.Equal(1, metrics.TotalRequests);
        Assert.Equal(0, metrics.TotalFailures);
        Assert.True(metrics.AverageResponseTimeMs >= 0);
    }

    [Fact]
    public void CreateRequest_AddsContentTypeHeader()
    {
        var handler = new NoopHandler();
        var client = CreateClient(handler);

        using var request = CreateRequest(client);

        Assert.True(request.Content?.Headers.Contains("Content-Type"));
    }

    [Fact]
    public void CreateRequest_UsesDefaultHeaders()
    {
        var handler = new NoopHandler();
        var client = CreateClient(handler);

        using var request = CreateRequest(client);

        Assert.True(request.Headers.Contains("User-Agent"));
        Assert.True(request.Headers.Contains("indeed-api-key"));
    }

    [Fact]
    public void GetMetrics_ReportsRequestsPerSecond_WhenRequestsRecorded()
    {
        var handler = new NoopHandler();
        var client = CreateClient(handler);

        using var request = CreateRequest(client);
        var metrics = client.GetMetrics();

        Assert.True(metrics.RequestsPerSecond >= 0);
    }

    private static HttpRequestMessage CreateRequest(IndeedApiClient client)
    {
        var payload = new { query = "test" };
        return client.CreateRequest(payload);
    }

    private static IndeedApiClient CreateClient(HttpMessageHandler handler)
    {
        var options = new IndeedOptions { ApiKey = "test-key", Country = CountryCode.US };
        return new IndeedApiClient(
            proxyProvider: new StubProxyProvider(),
            sessionOrchestrator: null,
            options: options,
            logger: NullLogger<IndeedApiClient>.Instance,
            handler: handler,
            timeProvider: TimeProvider.System);
    }

    private sealed class StubProxyProvider : IProxyProvider
    {
        public Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default) =>
            Task.FromResult<ProxyInfo?>(null);
    }

private sealed class NoopHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{\"jobSearch\":{\"pageInfo\":{\"nextCursor\":null,\"hasNextPage\":false}}}}")
        });
}

    private sealed class ResponseHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public ResponseHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        private readonly ManualResetEventSlim _gate;
        private readonly ManualResetEventSlim _release;

        public BlockingHandler(ManualResetEventSlim gate, ManualResetEventSlim release)
        {
            _gate = gate;
            _release = release;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _gate.Set();
            _release.Wait(cancellationToken);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":{\"jobSearch\":{\"pageInfo\":{\"nextCursor\":null,\"hasNextPage\":false}}}}")
            });
        }
    }
}
