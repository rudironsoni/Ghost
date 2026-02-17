using System.Net;
using Ghost.Models;
using Ghost.Plugin.Indeed.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace Ghost.Plugin.Indeed.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for Indeed plugin.
/// Sets up dependency injection container with mocked external services.
/// </summary>
public sealed class IndeedE2EFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public WireMockServer WireMockServer { get; }
    public IConfiguration Configuration { get; }

    public IndeedE2EFixture()
    {
        WireMockServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 9093,
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
        var options = new IndeedOptions
        {
            Enabled = true,
            Country = CountryCode.US,
            BaseUrl = $"http://localhost:{WireMockServer.Port}",
            ApiEndpoint = $"http://localhost:{WireMockServer.Port}/graphql",
            ApiKey = "test-api-key",
            RequestTimeoutMs = 30000,
            MaxRetries = 3,
            ResultsPerPage = 25
        };
        services.AddSingleton(options);

        // Register HTTP client
        services.AddHttpClient<IndeedApiClient>(client =>
        {
            client.BaseAddress = new Uri($"http://localhost:{WireMockServer.Port}");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register Indeed services
        services.AddSingleton<IndeedApiClient>(sp =>
        {
            ILogger<IndeedApiClient> logger = sp.GetRequiredService<ILogger<IndeedApiClient>>();
            return new IndeedApiClient(sp.GetService<IProxyProvider>()!, sp.GetRequiredService<IndeedOptions>(), logger);
        });

        services.AddScoped<IndeedJobClient>();
    }

    private void SetupMockEndpoints()
    {
        // Mock Indeed GraphQL endpoint for job search
        WireMockServer
            .Given(Request.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockGraphQLSearchResponse()));
    }

    private static string GetMockGraphQLSearchResponse()
    {
        return """
        {
            "data": {
                "jobSearch": {
                    "results": [
                        {
                            "job": {
                                "key": "indeed-job-001",
                                "title": "Software Engineer",
                                "employer": {
                                    "name": "Tech Solutions Inc"
                                },
                                "location": {
                                    "formatted": {
                                        "long": "San Francisco, CA"
                                    }
                                },
                                "description": {
                                    "html": "<p>We are looking for a skilled software engineer</p>"
                                }
                            }
                        },
                        {
                            "job": {
                                "key": "indeed-job-002",
                                "title": "Senior Developer",
                                "employer": {
                                    "name": "Digital Corp"
                                },
                                "location": {
                                    "formatted": {
                                        "long": "Remote"
                                    }
                                },
                                "description": {
                                    "html": "<p>Join our remote team as a senior developer</p>"
                                }
                            }
                        }
                    ],
                    "pageInfo": {
                        "hasNextPage": true,
                        "nextCursor": "mock-cursor-123"
                    }
                }
            }
        }
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
/// Collection attribute for Indeed E2E tests.
/// </summary>
[CollectionDefinition("IndeedEnd2End")]
public class IndeedE2EFixtures : ICollectionFixture<IndeedE2EFixture>
{
}
