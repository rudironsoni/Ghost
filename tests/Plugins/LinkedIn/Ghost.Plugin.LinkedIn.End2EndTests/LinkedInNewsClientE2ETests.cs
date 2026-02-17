using Ghost.Contracts.News;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn News Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
public sealed class LinkedInNewsClientE2ETests : IAsyncLifetime, IClassFixture<LinkedInE2EFixture>
{
    private readonly LinkedInE2EFixture _fixture;
    private readonly RealBrowserFixture _browserFixture;

    public LinkedInNewsClientE2ETests(LinkedInE2EFixture fixture, RealBrowserFixture browserFixture)
    {
        _fixture = fixture;
        _browserFixture = browserFixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetArticles_ReturnsArticlesAsync()
    {
        // Arrange
        LinkedInNewsClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInNewsClient>();
        var filter = new NewsFilter { MaxResults = 10 };

        // Act
        IReadOnlyList<NewsArticle> results = await client.GetArticlesAsync(filter);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task Search_WithValidQuery_ReturnsArticlesAsync()
    {
        // Arrange
        LinkedInNewsClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInNewsClient>();
        string query = "technology";
        var options = new NewsSearchOptions { MaxResults = 5 };

        // Act
        IReadOnlyList<NewsArticle> results = await client.SearchAsync(query, options);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void PlatformName_ReturnsExpectedValue()
    {
        // Arrange
        LinkedInNewsClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInNewsClient>();

        // Act
        string platformName = client.PlatformName;

        // Assert
        Assert.Equal("LinkedIn", platformName);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetArticle_ThrowsNotImplementedExceptionAsync()
    {
        // Arrange
        LinkedInNewsClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInNewsClient>();
        string articleId = "article-001";

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetArticleAsync(articleId));
    }
}
