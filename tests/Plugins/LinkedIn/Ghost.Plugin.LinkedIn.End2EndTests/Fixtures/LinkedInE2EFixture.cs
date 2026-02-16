using Ghost.Contracts.Jobs;
using Ghost.Contracts.News;
using Ghost.Contracts.Social;
using Ghost.Plugin.LinkedIn.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using WireMock.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for LinkedIn plugin.
/// Sets up dependency injection container with mocked external services.
/// </summary>
public sealed class LinkedInE2EFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public WireMockServer WireMockServer { get; }
    public IConfiguration Configuration { get; }

    public LinkedInE2EFixture()
    {
        WireMockServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 9094,
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
        services.Configure<LinkedInOptions>(options =>
        {
            options.Enabled = true;
            options.BaseUrl = $"http://localhost:{WireMockServer.Port}";
            options.ScrapingStrategy = JobScrapingStrategy.Browser;
        });

        services.Configure<LinkedInSessionPoolOptions>(options =>
        {
            options.MaxSessions = 2;
            options.SessionTimeoutMinutes = 30;
        });

        // Mock IBrowserSession
        var mockBrowserSession = Substitute.For<Ghost.IBrowserSession>();
        var mockPage = Substitute.For<Ghost.IPage>();

        // Setup mock page behavior
        mockPage.NavigateAsync(Arg.Any<string>(), Arg.Any<Ghost.NavigationOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        mockPage.WaitForLoadStateAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("LinkedIn Jobs");
        mockPage.GetContentAsync(Arg.Any<CancellationToken>())
            .Returns(GetMockJobSearchHtml());
        mockPage.QuerySelectorAllAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([]);
        mockPage.DisposeAsync()
            .Returns(Task.CompletedTask);

        mockBrowserSession.NewPageAsync(Arg.Any<Ghost.PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(mockPage);

        services.AddSingleton(mockBrowserSession);
        services.AddSingleton(mockPage);

        // Register LinkedIn services
        services.AddSingleton<JavaScriptAdapter>();
        services.AddSingleton<EntityParser>();
        services.AddScoped<LinkedInJobClient>();
        services.AddScoped<LinkedInSocialClient>();
        services.AddScoped<LinkedInNewsClient>();
    }

    private void SetupMockEndpoints()
    {
        // Mock LinkedIn feed
        WireMockServer
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/feed/")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(System.Net.HttpStatusCode.OK)
                .WithHeader("Content-Type", "text/html")
                .WithBody(GetMockFeedHtml()));

        // Mock LinkedIn jobs search
        WireMockServer
            .Given(WireMock.RequestBuilders.Request.Create()
                .WithPath("/jobs/search")
                .UsingGet())
            .RespondWith(WireMock.ResponseBuilders.Response.Create()
                .WithStatusCode(System.Net.HttpStatusCode.OK)
                .WithHeader("Content-Type", "text/html")
                .WithBody(GetMockJobSearchHtml()));
    }

    private static string GetMockJobSearchHtml()
    {
        return """
        <!DOCTYPE html>
        <html>
        <head><title>LinkedIn Jobs</title></head>
        <body>
            <ul class="jobs-search-results__list">
                <li class="jobs-search-results__list-item" data-id="linkedin-job-001">
                    <div class="job-card-container">
                        <a href="/jobs/view/linkedin-job-001" class="job-card-list__title">Software Engineer</a>
                        <span class="job-card-container__company-name">Tech Corp</span>
                        <span class="job-card-container__metadata-item">San Francisco, CA</span>
                    </div>
                </li>
                <li class="jobs-search-results__list-item" data-id="linkedin-job-002">
                    <div class="job-card-container">
                        <a href="/jobs/view/linkedin-job-002" class="job-card-list__title">Senior Developer</a>
                        <span class="job-card-container__company-name">Innovation Labs</span>
                        <span class="job-card-container__metadata-item">Remote</span>
                    </div>
                </li>
            </ul>
        </body>
        </html>
        """;
    }

    private static string GetMockFeedHtml()
    {
        return """
        <!DOCTYPE html>
        <html>
        <head><title>LinkedIn Feed</title></head>
        <body>
            <div class="feed-shared-update-v2">
                <h3>Tech Industry Sees Major Growth</h3>
                <a href="/pulse/article-001">Read more</a>
            </div>
            <div class="feed-shared-update-v2">
                <h3>Remote Work Trends Continue</h3>
                <a href="/pulse/article-002">Read more</a>
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
/// Collection attribute for LinkedIn E2E tests.
/// </summary>
[CollectionDefinition("LinkedInEnd2End")]
public class LinkedInE2ECollection : ICollectionFixture<LinkedInE2EFixture>
{
}
