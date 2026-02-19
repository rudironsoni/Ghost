using Ghost.Contracts.News;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Ghost.Testing.End2End;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn News Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
public sealed class LinkedInNewsClientE2ETests : IAsyncLifetime
{
    private readonly RealBrowserFixture _browserFixture;
    private readonly ITestOutputHelper _output;
    private LinkedInE2EFixture? _fixture;

    public LinkedInNewsClientE2ETests(RealBrowserFixture browserFixture, ITestOutputHelper output)
    {
        _browserFixture = browserFixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _fixture = new LinkedInE2EFixture(_browserFixture);
        await _fixture.InitializeAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_fixture != null)
        {
            await _fixture.DisposeAsync().ConfigureAwait(false);
        }
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetArticles_ReturnsArticlesAsync()
    {
        // Arrange
        LinkedInNewsClient client = _fixture!.ServiceProvider.GetRequiredService<LinkedInNewsClient>();
        var filter = new NewsFilter { MaxResults = 10 };
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(20));

        // Act
        IReadOnlyList<NewsArticle> results;
        try
        {
            results = await client.GetArticlesAsync(filter, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            throw new XunitException("GetArticlesAsync timed out after 20 seconds.");
        }

        // Assert
        Assert.NotNull(results);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task Search_WithValidQuery_ReturnsArticlesAsync()
    {
        // Arrange
        LinkedInNewsClient client = _fixture!.ServiceProvider.GetRequiredService<LinkedInNewsClient>();
        string query = "technology";
        var options = new NewsSearchOptions { MaxResults = 5 };
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(20));

        // Act
        IReadOnlyList<NewsArticle> results;
        try
        {
            results = await client.SearchAsync(query, options, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            throw new XunitException("SearchAsync timed out after 20 seconds.");
        }

        // Assert
        Assert.NotNull(results);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        LinkedInNewsClient client = _fixture!.ServiceProvider.GetRequiredService<LinkedInNewsClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("LinkedIn", platformName);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public async Task GetArticle_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        LinkedInNewsClient client = _fixture!.ServiceProvider.GetRequiredService<LinkedInNewsClient>();
        string articleId = "article-001";

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetArticleAsync(articleId));
    }
}
