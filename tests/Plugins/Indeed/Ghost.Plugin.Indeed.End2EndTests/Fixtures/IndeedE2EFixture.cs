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
            return new IndeedApiClient(sp.GetService<IProxyProvider>(), sp.GetRequiredService<IndeedOptions>(), logger);
        });

        services.AddScoped<IndeedJobClient>();
    }

    private void SetupMockEndpoints()
    {
        // Mock Indeed search API endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/api/jobs/search")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockSearchResponse()));

        // Mock Indeed job details endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/viewjob")
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
                            "id": "indeed-job-001",
                            "title": "Software Engineer",
                            "company": {
                                "name": "Tech Solutions Inc"
                            },
                            "location": {
                                "displayName": "San Francisco, CA"
                            },
                            "description": "We are looking for a skilled software engineer",
                            "url": "https://www.indeed.com/viewjob?jk=indeed-job-001",
                            "date": "2024-01-15T10:00:00Z"
                        },
                        {
                            "id": "indeed-job-002",
                            "title": "Senior Developer",
                            "company": {
                                "name": "Digital Corp"
                            },
                            "location": {
                                "displayName": "Remote"
                            },
                            "description": "Join our remote team as a senior developer",
                            "url": "https://www.indeed.com/viewjob?jk=indeed-job-002",
                            "date": "2024-01-14T08:00:00Z"
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

    private static string GetMockJobDetailsHtml()
    {
        return """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Software Engineer - Tech Solutions Inc</title>
            <script type="application/ld+json">
            {
                "@context": "https://schema.org",
                "@type": "JobPosting",
                "title": "Software Engineer",
                "description": "We are seeking a talented Software Engineer to join our team. You will be responsible for developing high-quality applications.",
                "hiringOrganization": {
                    "@type": "Organization",
                    "name": "Tech Solutions Inc"
                },
                "jobLocation": {
                    "@type": "Place",
                    "address": {
                        "@type": "PostalAddress",
                        "addressLocality": "San Francisco",
                        "addressRegion": "CA"
                    }
                },
                "employmentType": "FULL_TIME",
                "datePosted": "2024-01-15"
            }
            </script>
        </head>
        <body>
            <div class="jobsearch-JobInfoHeader">
                <h1 class="jobsearch-JobInfoHeader-title">Software Engineer</h1>
                <div class="jobsearch-CompanyInfo">
                    <span class="company">Tech Solutions Inc</span>
                    <span class="location">San Francisco, CA</span>
                </div>
            </div>
            <div class="jobsearch-JobComponent-description">
                <p>We are seeking a talented Software Engineer to join our team.</p>
                <ul>
                    <li>Develop high-quality applications</li>
                    <li>Collaborate with cross-functional teams</li>
                    <li>Mentor junior developers</li>
                </ul>
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
/// Collection attribute for Indeed E2E tests.
/// </summary>
[CollectionDefinition("IndeedEnd2End")]
public class IndeedE2ECollection : ICollectionFixture<IndeedE2EFixture>
{
}
