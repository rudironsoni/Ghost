using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// Test fixture for HTTP-based smoke tests against a running Ghost API instance.
/// Provides a configured HttpClient with aggressive timeouts to prevent hanging.
/// </summary>
public sealed class HttpSmokeTestFixture : IAsyncLifetime, IDisposable
{
    private HttpClient HttpClient { get; }

    /// <summary>
    /// Gets the base URL for the Ghost API.
    /// Defaults to localhost:8080 but can be overridden via GHOST_SMOKE_TEST_URL environment variable.
    /// </summary>
    public string BaseUrl { get; }

    public HttpSmokeTestFixture()
    {
        BaseUrl = Environment.GetEnvironmentVariable("GHOST_SMOKE_TEST_URL") ?? "http://localhost:8080";

        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };

        HttpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        HttpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ghost.Smoke.Tests", "1.0"));
    }

    public Task InitializeAsync()
    {
        // No server health check here - tests will fail naturally if server is unavailable
        // The HttpClient timeout ensures they fail fast (within 5 seconds)
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        HttpClient.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a GET request to the specified URI and returns the response.
    /// </summary>
    public Task<HttpResponseMessage> GetAsync(string requestUri)
    {
        return HttpClient.GetAsync(requestUri);
    }

    /// <summary>
    /// Sends a GET request to the specified URI and deserializes the JSON response.
    /// </summary>
    public async Task<TResponse?> GetAsync<TResponse>(string requestUri, ITestOutputHelper? output = null)
    {
        output?.WriteLine($"GET {requestUri}");
        var response = await HttpClient.GetAsync(requestUri).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request to the specified URI with the given content.
    /// </summary>
    public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
    {
        return HttpClient.PostAsync(requestUri, content);
    }

    /// <summary>
    /// Sends a POST request with JSON content to the specified URI and deserializes the response.
    /// </summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        ITestOutputHelper? output = null)
    {
        output?.WriteLine($"POST {requestUri}");
        var response = await HttpClient.PostAsJsonAsync(requestUri, request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>().ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request with JSON content to the specified URI.
    /// </summary>
    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T value)
    {
        return HttpClient.PostAsJsonAsync(requestUri, value);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        HttpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Marks the test collection for HTTP smoke tests with shared fixture.
/// </summary>
[CollectionDefinition("HttpSmokeTests")]
public class HttpSmokeTestDefinitions : ICollectionFixture<HttpSmokeTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
