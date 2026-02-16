using System.Net;
using Ghost.Plugin.Glassdoor.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Ghost.Plugin.Glassdoor.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for Glassdoor plugin.
/// Sets up dependency injection container with mocked external services.
/// </summary>
public sealed class GlassdoorE2EFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public WireMockServer WireMockServer { get; }
    public IConfiguration Configuration { get; }

    public GlassdoorE2EFixture()
    {
        WireMockServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 9091,
            UseSSL = false
        });

        Configuration = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        SetupMockEndpoints();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Configuration
        services.Configure<GlassdoorOptions>(options =>
        {
            options.Enabled = true;
            options.BaseUrl = $"http://localhost:{WireMockServer.Port}";
            options.RequestTimeoutMs = 30000;
            options.MaxRetries = 3;
            options.Strategy = JobSearchStrategy.HttpFirst;
            options.ProxyEnabled = false;
        });

        // Register HTTP client with WireMock base URL
        services.AddHttpClient<GlassdoorApiClient>(client =>
        {
            client.BaseAddress = new Uri($"http://localhost:{WireMockServer.Port}");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register plugin services
        services.AddScoped<GlassdoorApiClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(GlassdoorApiClient));
            var logger = sp.GetRequiredService<ILogger<GlassdoorApiClient>>();
            return new GlassdoorApiClient(httpClient, logger);
        });

        services.AddScoped<GlassdoorJobClient>();
    }

    private void SetupMockEndpoints()
    {
        // Mock Glassdoor search endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/graph.json")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockSearchResponse()));

        // Mock Glassdoor job details endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/job/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "text/html")
                .WithBody(GetMockJobDetailsHtml()));
    }

    private static string GetMockSearchResponse()
    {
        return """
        {
            "data": {
                "jobSearch": {
                    "results": [
                        {
                            "id": "123456",
                            "title": "Software Engineer",
                            "employer": {
                                "name": "Tech Corp"
                            },
                            "location": {
                                "displayName": "San Francisco, CA"
                            },
                            "description": "Looking for a skilled software engineer",
                            "jobview": {
                                "url": "/job/123456/software-engineer"
                            }
                        }
                    ]
                }
            }
        }
        """;
    }

    private static string GetMockJobDetailsHtml()
    {
        return """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Software Engineer Job</title>
            <meta name="csrf-token" content="mock-csrf-token-12345" />
        </head>
        <body>
            <div class="jobContent">
                <h1 class="jobTitle">Software Engineer</h1>
                <div class="employerName">Tech Corp</div>
                <div class="location">San Francisco, CA</div>
                <div class="description">
                    <p>We are looking for a skilled software engineer to join our team.</p>
                    <p>Requirements:</p>
                    <ul>
                        <li>5+ years of experience</li>
                        <li>Strong C# skills</li>
                    </ul>
                </div>
            </div>
        </body>
        </html>
        """;
    }

    public void Dispose()
    {
        WireMockServer?.Stop();
        WireMockServer?.Dispose();

        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Collection attribute for Glassdoor E2E tests.
/// Ensures tests run sequentially with shared fixture.
/// </summary>
[CollectionDefinition("GlassdoorEnd2End")]
public class GlassdoorE2ECollection : ICollectionFixture<GlassdoorE2EFixture>
{
}
