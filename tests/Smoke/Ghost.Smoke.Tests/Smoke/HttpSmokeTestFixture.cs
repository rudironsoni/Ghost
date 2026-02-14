using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// Test fixture for HTTP-based smoke tests against a running Ghost API instance.
/// Provides a configured HttpClient with aggressive timeouts to prevent hanging.
/// </summary>
public class HttpSmokeTestFixture : IAsyncLifetime
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Gets the base URL for the Ghost API.
    /// Defaults to localhost:8080 but can be overridden via GHOST_SMOKE_TEST_URL environment variable.
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Gets the configured HttpClient for making requests to the Ghost API.
    /// Configured with 5-second timeout to fail fast when server is unavailable.
    /// </summary>
    public HttpClient HttpClient => _httpClient;

    public HttpSmokeTestFixture()
    {
        BaseUrl = Environment.GetEnvironmentVariable("GHOST_SMOKE_TEST_URL") ?? "http://localhost:8080";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ghost.Smoke.Tests", "1.0"));
    }

    public Task InitializeAsync()
    {
        // No server health check here - tests will fail naturally if server is unavailable
        // The HttpClient timeout ensures they fail fast (within 5 seconds)
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a GET request to the specified URI and returns the response.
    /// </summary>
    public Task<HttpResponseMessage> GetAsync(string requestUri)
    {
        return _httpClient.GetAsync(requestUri);
    }

    /// <summary>
    /// Sends a POST request with JSON content to the specified URI.
    /// </summary>
    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T value)
    {
        return _httpClient.PostAsJsonAsync(requestUri, value);
    }
}

/// <summary>
/// Marks the test collection for HTTP smoke tests with shared fixture.
/// </summary>
[CollectionDefinition("HttpSmokeTests")]
public class HttpSmokeTestCollection : ICollectionFixture<HttpSmokeTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
