using Ghost.Contracts.Social;
using Ghost.Core;
using Ghost.Platform.X.E2E.Fixtures;
using Ghost.Platform.X.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.X.E2E;

/// <summary>
/// End-to-end tests for X platform using real browser automation.
/// </summary>
public class XPlatformE2ETests : IClassFixture<GhostKernelFixture>
{
    private readonly GhostKernelFixture _fixture;
    private readonly IBrowserSession _session;

    public XPlatformE2ETests(GhostKernelFixture fixture)
    {
        _fixture = fixture;
        _session = _fixture.ServiceProvider.GetRequiredService<IBrowserSession>();
    }

    #region Browser Initialization Tests

    [Fact]
    public async Task BrowserSession_CanCreatePage_WithCorrectTimezone()
    {
        // Arrange
        var pageOptions = new PageOptions
        {
            TimezoneId = "America/New_York",
            Locale = "en-US"
        };

        // Act
        var page = await _session.NewPageAsync(pageOptions);
        await using (page)
        {
            // Assert
            Assert.NotNull(page);
            
            // Verify timezone is set correctly
            var timezone = await page.EvaluateAsync<string>("() => Intl.DateTimeFormat().resolvedOptions().timeZone");
            Assert.Equal("America/New_York", timezone);
        }
    }

    [Fact]
    public async Task BrowserSession_PageNavigation_Works()
    {
        // Arrange
        var page = await _session.NewPageAsync();
        await using (page)
        {
            // Act
            await page.NavigateAsync("https://example.com");
            await page.WaitForLoadStateAsync();

            // Assert
            Assert.Contains("example.com", page.Url);
        }
    }

    [Fact]
    public async Task BrowserSession_StealthMode_Enabled()
    {
        // Arrange
        var page = await _session.NewPageAsync();
        await using (page)
        {
            // Act - Check navigator.webdriver is undefined (stealth mode indicator)
            var webdriver = await page.EvaluateAsync<object>("() => navigator.webdriver");

            // Assert
            Assert.Null(webdriver);
        }
    }

    #endregion

    #region X Platform DI Integration Tests

    [Fact]
    public void ServiceProvider_XOptions_Registered()
    {
        // Act
        var options = _fixture.ServiceProvider.GetService<Microsoft.Extensions.Options.IOptions<XOptions>>();

        // Assert
        Assert.NotNull(options);
        Assert.NotNull(options.Value);
        Assert.Equal("https://x.com", options.Value.BaseUrl);
    }

    [Fact]
    public void ServiceProvider_XSocialClient_Registered()
    {
        // Act
        var client = _fixture.ServiceProvider.GetService<ISocialClient>();

        // Assert
        Assert.NotNull(client);
        Assert.IsType<XSocialClient>(client);
    }

    [Fact]
    public void ServiceProvider_XAuthenticator_Registered()
    {
        // Act
        var authenticator = _fixture.ServiceProvider.GetService<XAuthenticator>();

        // Assert
        Assert.NotNull(authenticator);
    }

    [Fact]
    public void ServiceProvider_XThreadComposer_Registered()
    {
        // Act
        var composer = _fixture.ServiceProvider.GetService<XThreadComposer>();

        // Assert
        Assert.NotNull(composer);
    }

    [Fact]
    public void ServiceProvider_XPostContentSplitter_Registered_AsSingleton()
    {
        // Act
        var splitter1 = _fixture.ServiceProvider.GetService<XPostContentSplitter>();
        var splitter2 = _fixture.ServiceProvider.GetService<XPostContentSplitter>();

        // Assert
        Assert.NotNull(splitter1);
        Assert.NotNull(splitter2);
        Assert.Same(splitter1, splitter2); // Should be same instance (singleton)
    }

    #endregion

    #region Content Splitting E2E Tests

    [Fact]
    public void XPostContentSplitter_ShortContent_NoSplit()
    {
        // Arrange
        var splitter = _fixture.ServiceProvider.GetRequiredService<XPostContentSplitter>();
        var content = "This is a short tweet.";

        // Act
        var result = splitter.Split(content);

        // Assert
        Assert.Single(result);
        Assert.Equal(content, result[0]);
    }

    [Fact]
    public void XPostContentSplitter_LongContent_SplitsIntoMultipleTweets()
    {
        // Arrange
        var splitter = _fixture.ServiceProvider.GetRequiredService<XPostContentSplitter>();
        var content = string.Join(" ", Enumerable.Range(1, 50).Select(i => $"This is sentence number {i} that makes a long thread."));

        // Act
        var result = splitter.Split(content);

        // Assert
        Assert.True(result.Count > 1, "Long content should be split into multiple tweets");
        
        // Verify each tweet has thread numbering
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Contains($"({i + 1}/{result.Count})", result[i]);
        }
    }

    [Fact]
    public void XPostContentSplitter_VeryLongWord_SplitsCorrectly()
    {
        // Arrange
        var splitter = _fixture.ServiceProvider.GetRequiredService<XPostContentSplitter>();
        var longWord = new string('a', 400); // 400 character word

        // Act
        var result = splitter.Split(longWord);

        // Assert
        Assert.True(result.Count >= 2, "Very long word should be split into multiple parts");
    }

    [Fact]
    public void XPostContentSplitter_RequiresThread_DetectsLongContent()
    {
        // Arrange
        var splitter = _fixture.ServiceProvider.GetRequiredService<XPostContentSplitter>();
        var shortContent = "Short tweet.";
        var longContent = new string('a', 500);

        // Act & Assert
        Assert.False(splitter.RequiresThread(shortContent));
        Assert.True(splitter.RequiresThread(longContent));
    }

    [Fact]
    public void XPostContentSplitter_GetEstimatedTweetCount_Accurate()
    {
        // Arrange
        var splitter = _fixture.ServiceProvider.GetRequiredService<XPostContentSplitter>();
        var content = new string('a', 600);

        // Act
        var estimatedCount = splitter.GetEstimatedTweetCount(content);
        var actualParts = splitter.Split(content);

        // Assert
        Assert.Equal(actualParts.Count, estimatedCount);
    }

    #endregion

    #region X Platform Options E2E Tests

    [Fact]
    public void XOptions_Defaults_AreCorrect()
    {
        // Arrange
        var options = _fixture.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<XOptions>>().Value;

        // Assert
        Assert.Equal("https://x.com", options.BaseUrl);
        Assert.Equal(280, options.MaxTweetLength);
        Assert.Equal(4, options.MaxMediaAttachments);
        Assert.Equal(1, options.MaxVideoAttachments);
        Assert.Equal(5, options.MaxImageSizeMB);
        Assert.Equal(512, options.MaxVideoSizeMB);
        Assert.Equal(2000, options.ThreadDelayMs);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(5000, options.RetryDelayMs);
    }

    [Fact]
    public void XOptions_GetPageOptions_ReturnsCorrectConfiguration()
    {
        // Arrange
        var options = _fixture.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<XOptions>>().Value;
        options.TimezoneId = "Europe/London";
        options.Locale = "en-GB";

        // Act
        var pageOptions = options.GetPageOptions();

        // Assert
        Assert.Equal("Europe/London", pageOptions.TimezoneId);
        Assert.Equal("en-GB", pageOptions.Locale);
    }

    #endregion

    #region Browser Navigation E2E Tests (Limited)

    [Fact]
    public async Task XNavigation_CanNavigateToHomepage()
    {
        // Arrange
        var page = await _session.NewPageAsync();
        await using (page)
        {
            // Act
            await page.NavigateAsync("https://x.com");
            await page.WaitForLoadStateAsync();

            // Assert
            Assert.Contains("x.com", page.Url);
        }
    }

    [Fact]
    public async Task XNavigation_PageContainsExpectedElements()
    {
        // Arrange
        var page = await _session.NewPageAsync();
        await using (page)
        {
            // Act
            await page.NavigateAsync("https://x.com");
            await page.WaitForLoadStateAsync();

            // Assert - Check for common X elements
            var hasTitle = await page.EvaluateAsync<bool>("() => document.title.length > 0");
            Assert.True(hasTitle);
        }
    }

    #endregion
}
