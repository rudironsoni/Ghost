using Ghost.Contracts.Jobs;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Smoke.Tests.Smoke;

/// <summary>
/// Test fixture that runs the Ghost WebAPI in-memory using WebApplicationFactory.
/// This eliminates the need for a manually started server for smoke tests.
/// </summary>
public sealed class GhostWebApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private HttpClient? _httpClient;

    /// <summary>
    /// Gets the base URL for the test server (always http://localhost for TestServer).
    /// </summary>
    public static string BaseUrl => "http://localhost";

    /// <summary>
    /// Gets the configured HttpClient for making requests to the test server.
    /// </summary>
    public HttpClient HttpClient
    {
        get
        {
            if (_httpClient is null)
            {
                _httpClient = CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = true,
                    HandleCookies = true,
                    MaxAutomaticRedirections = 7
                });
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ghost.Smoke.Tests", "1.0"));
            }
            return _httpClient;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Override configuration for testing
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Disable all external platforms - use stubs instead
                ["Ghost:Extensions:LinkedIn:Enabled"] = "false",
                ["Ghost:Extensions:Indeed:Enabled"] = "false",
                ["Ghost:Extensions:Google:Enabled"] = "false",
                ["Ghost:Extensions:Glassdoor:Enabled"] = "false",
                ["Ghost:Extensions:InfoJobs:Enabled"] = "false",

                // Disable external services that require credentials
                ["Ghost:Extensions:OpenAI:Enabled"] = "false",
                ["Ghost:Extensions:Anthropic:Enabled"] = "false",

                // Proxy settings - disabled for tests
                ["Ghost:Proxy:Strategy"] = "None"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove any existing IJobClient registrations
            var existingDescriptors = services.Where(d => d.ServiceType == typeof(IJobClient)).ToList();
            foreach (var descriptor in existingDescriptors)
            {
                services.Remove(descriptor);
            }

            // Register stub job clients as keyed services
            // The API uses GetKeyedService to get platform-specific clients
            services.AddKeyedSingleton<IJobClient, StubJobClient>("linkedin", (sp, key) => new StubJobClient("LinkedIn"));
            services.AddKeyedSingleton<IJobClient, StubJobClient>("indeed", (sp, key) => new StubJobClient("Indeed"));
            services.AddKeyedSingleton<IJobClient, StubJobClient>("google", (sp, key) => new StubJobClient("Google"));
            services.AddKeyedSingleton<IJobClient, StubJobClient>("glassdoor", (sp, key) => new StubJobClient("Glassdoor"));
            
            // Also register as non-keyed for backwards compatibility
            services.AddSingleton<IJobClient>(sp => new StubJobClient("All"));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure test environment is set
        builder.UseEnvironment("Testing");
        return base.CreateHost(builder);
    }

    public Task InitializeAsync()
    {
        // Verify the server is running
        _httpClient = HttpClient;
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        _httpClient = null;
        await base.DisposeAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        return DisposeAsync().AsTask();
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
        HttpResponseMessage response = await HttpClient.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
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
        HttpResponseMessage response = await HttpClient.PostAsJsonAsync(requestUri, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    /// <summary>
    /// Sends a POST request with JSON content to the specified URI.
    /// </summary>
    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T value)
    {
        return HttpClient.PostAsJsonAsync(requestUri, value);
    }
}

/// <summary>
/// Collection definition for HTTP smoke tests using the GhostWebApiFixture.
/// </summary>
[CollectionDefinition("GhostWebApiSmokeTests")]
public class GhostWebApiSmokeTestCollection : ICollectionFixture<GhostWebApiFixture>{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
