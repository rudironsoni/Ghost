using System.Net;
using Ghost.Plugin.Google.Gemini;
using Ghost.Plugin.Google.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WireMock.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace Ghost.Plugin.Google.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for Google plugin.
/// Sets up dependency injection container with mocked external services.
/// </summary>
public sealed class GoogleE2EFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public WireMockServer WireMockServer { get; }
    public IConfiguration Configuration { get; }

    public GoogleE2EFixture()
    {
        WireMockServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 9092,
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

        // Jobs configuration
        services.Configure<GoogleJobsOptions>(options =>
        {
            options.Enabled = true;
            options.ApiKey = "test-api-key";
            options.BaseUrl = $"http://localhost:{WireMockServer.Port}";
            options.Strategy = JobSearchStrategy.HttpFirst;
        });

        // Gemini configuration
        services.Configure<GeminiOptions>(options =>
        {
            options.Enabled = true;
            options.ApiKey = "test-api-key";
            options.BaseUrl = $"http://localhost:{WireMockServer.Port}/gemini";
            options.DefaultModel = "gemini-pro";
            options.ResponseTimeout = TimeSpan.FromMinutes(2);
        });

        // Mock IBrowserSession for Gemini
        IBrowserSession mockBrowserSession = NSubstitute.Substitute.For<Ghost.IBrowserSession>();
        services.AddSingleton(mockBrowserSession);

        // Register Google services
        services.AddSingleton<GoogleJobClient>();
        services.AddSingleton<GeminiClient>();
    }

    private void SetupMockEndpoints()
    {
        // Mock Google Jobs API search endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/v1/jobs:search")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockJobsSearchResponse()));

        // Mock Google Jobs API get job endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/v1/jobs/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockJobDetailsResponse()));

        // Mock Gemini API
        WireMockServer
            .Given(Request.Create()
                .WithPath("/gemini/v1/models/gemini-pro:generateContent")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockGeminiResponse()));
    }

    private static string GetMockJobsSearchResponse()
    {
        return """
        {
            "jobs": [
                {
                    "name": "projects/test/jobs/job-001",
                    "title": "Senior Software Engineer",
                    "company": {
                        "name": "Google",
                        "displayName": "Google"
                    },
                    "location": {
                        "displayName": "Mountain View, CA"
                    },
                    "description": "Join our engineering team...",
                    "jobBenefits": ["Health insurance", "401k"],
                    "employmentTypes": ["FULL_TIME"],
                    "createTime": "2024-01-15T10:00:00Z"
                },
                {
                    "name": "projects/test/jobs/job-002",
                    "title": "Software Engineer",
                    "company": {
                        "name": "TechCorp",
                        "displayName": "TechCorp"
                    },
                    "location": {
                        "displayName": "Remote"
                    },
                    "description": "Build scalable systems...",
                    "employmentTypes": ["FULL_TIME", "CONTRACT"],
                    "createTime": "2024-01-14T08:00:00Z"
                }
            ],
            "nextPageToken": "mock-next-page-token"
        }
        """;
    }

    private static string GetMockJobDetailsResponse()
    {
        return """
        {
            "name": "projects/test/jobs/job-001",
            "title": "Senior Software Engineer",
            "company": {
                "name": "Google",
                "displayName": "Google"
            },
            "location": {
                "displayName": "Mountain View, CA"
            },
            "description": "Join our engineering team to build amazing products...",
            "qualifications": ["Bachelor's degree in CS", "5+ years experience"],
            "responsibilities": ["Design systems", "Write code", "Mentor engineers"],
            "jobBenefits": ["Health insurance", "401k matching", "Free meals"],
            "employmentTypes": ["FULL_TIME"],
            "createTime": "2024-01-15T10:00:00Z"
        }
        """;
    }

    private static string GetMockGeminiResponse()
    {
        return """
        {
            "candidates": [
                {
                    "content": {
                        "parts": [
                            {
                                "text": "This is a test response from the Gemini API."
                            }
                        ],
                        "role": "model"
                    },
                    "finishReason": "STOP",
                    "safetyRatings": [
                        {
                            "category": "HARM_CATEGORY_HARASSMENT",
                            "probability": "NEGLIGIBLE"
                        }
                    ]
                }
            ],
            "usageMetadata": {
                "promptTokenCount": 10,
                "candidatesTokenCount": 15,
                "totalTokenCount": 25
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
/// Collection attribute for Google E2E tests.
/// </summary>
[CollectionDefinition("GoogleEnd2End")]
public class GoogleE2ECollection : ICollectionFixture<GoogleE2EFixture>
{
}
