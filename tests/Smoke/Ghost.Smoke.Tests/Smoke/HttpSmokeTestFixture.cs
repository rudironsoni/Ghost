using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// Shared fixture for HTTP-based smoke tests that creates an HttpClient
/// configured to communicate with a running Ghost instance.
/// </summary>
public class HttpSmokeTestFixture : IAsyncLifetime
{
    public HttpClient HttpClient { get; private set; } = null!;
    public string BaseUrl { get; private set; } = null!;
    public string? ApiKey { get; private set; }
    public JsonSerializerOptions JsonSerializerOptions { get; private set; } = null!;

    public Task InitializeAsync()
    {
        // Get configuration from environment variables
        BaseUrl = Environment.GetEnvironmentVariable("GHOST_SMOKE_BASE_URL") ?? "http://localhost:8080";
        ApiKey = Environment.GetEnvironmentVariable("GHOST_ADMIN_API_KEY");

        // Configure JSON options
        JsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Create and configure HttpClient
        HttpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Add API key header if provided
        if (!string.IsNullOrEmpty(ApiKey))
        {
            HttpClient.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        }

        HttpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        HttpClient?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a POST request to the specified endpoint with JSON content.
    /// </summary>
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        var json = JsonSerializer.Serialize(request, JsonSerializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        output?.WriteLine($"POST {BaseUrl}{endpoint}");
        output?.WriteLine($"Request: {json}");

        var response = await HttpClient.PostAsync(endpoint, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        output?.WriteLine($"Response Status: {response.StatusCode}");
        output?.WriteLine($"Response: {responseContent}");

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<TResponse>(responseContent, JsonSerializerOptions);
    }

    /// <summary>
    /// Sends a GET request to the specified endpoint.
    /// </summary>
    public async Task<TResponse?> GetAsync<TResponse>(
        string endpoint,
        Xunit.Abstractions.ITestOutputHelper? output = null)
    {
        output?.WriteLine($"GET {BaseUrl}{endpoint}");

        var response = await HttpClient.GetAsync(endpoint);
        var responseContent = await response.Content.ReadAsStringAsync();

        output?.WriteLine($"Response Status: {response.StatusCode}");
        output?.WriteLine($"Response: {responseContent}");

        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<TResponse>(responseContent, JsonSerializerOptions);
    }
}
