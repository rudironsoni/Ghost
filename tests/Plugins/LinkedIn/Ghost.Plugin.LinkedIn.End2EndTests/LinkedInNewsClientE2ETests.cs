using Ghost.Contracts.News;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn News Client.
/// Tests full request/response lifecycle with mocked external services.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
public sealed class LinkedInNewsClientE2ETests
{
    private readonly LinkedInE2EFixture _fixture;

    public LinkedInNewsClientE2ETests(LinkedInE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public async Task GetArticlesAsync_ReturnsArticles()
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
    public async Task SearchAsync_WithValidQuery_ReturnsArticles()
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
    public async Task GetArticleAsync_ThrowsNotImplementedException()
    {
        // Arrange
        LinkedInNewsClient client = _fixture.ServiceProvider.GetRequiredService<LinkedInNewsClient>();
        string articleId = "article-001";

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => client.GetArticleAsync(articleId));
    }
}
