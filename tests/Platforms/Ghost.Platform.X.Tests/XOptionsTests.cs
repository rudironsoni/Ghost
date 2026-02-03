using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ghost.Platform.X.Tests;

public class XOptionsTests
{
    [Fact]
    public void XOptions_Defaults_AreCorrect()
    {
        // Arrange
        var options = new XOptions();

        // Assert
        Assert.Equal("https://x.com", options.BaseUrl);
        Assert.Equal(30, options.PageLoadTimeout);
        Assert.Equal(ScrapingStrategy.Resilient, options.ScrapingStrategy);
        Assert.Equal("America/New_York", options.TimezoneId);
        Assert.Equal("en-US", options.Locale);
        Assert.False(options.ProxyEnabled);
        Assert.True(options.WarmUpEnabled);
        Assert.Equal("US", options.Country);
        Assert.Equal(280, options.MaxTweetLength);
        Assert.Equal(4, options.MaxMediaAttachments);
        Assert.Equal(1, options.MaxVideoAttachments);
        Assert.Equal(5, options.MaxImageSizeMB);
        Assert.Equal(512, options.MaxVideoSizeMB);
        Assert.Equal(2000, options.ThreadDelayMs);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(5000, options.RetryDelayMs);
    }

    [Theory]
    [InlineData("https://twitter.com")]
    [InlineData("https://x.com")]
    [InlineData("http://localhost:3000")]
    public void XOptions_BaseUrl_CanBeSet(string url)
    {
        // Arrange
        var options = new XOptions { BaseUrl = url };

        // Assert
        Assert.Equal(url, options.BaseUrl);
    }

    [Fact]
    public void XOptions_SupportedImageFormats_ContainsExpectedFormats()
    {
        // Arrange
        var options = new XOptions();

        // Assert
        Assert.Contains(".jpg", options.SupportedImageFormats);
        Assert.Contains(".jpeg", options.SupportedImageFormats);
        Assert.Contains(".png", options.SupportedImageFormats);
        Assert.Contains(".gif", options.SupportedImageFormats);
        Assert.Contains(".webp", options.SupportedImageFormats);
        Assert.Equal(5, options.SupportedImageFormats.Count);
    }

    [Fact]
    public void XOptions_SupportedVideoFormats_ContainsExpectedFormats()
    {
        // Arrange
        var options = new XOptions();

        // Assert
        Assert.Contains(".mp4", options.SupportedVideoFormats);
        Assert.Contains(".mov", options.SupportedVideoFormats);
        Assert.Contains(".webm", options.SupportedVideoFormats);
        Assert.Equal(3, options.SupportedVideoFormats.Count);
    }

    [Fact]
    public void XOptions_GetPageOptions_ReturnsConfiguredOptions()
    {
        // Arrange
        var options = new XOptions
        {
            TimezoneId = "Europe/London",
            Locale = "en-GB"
        };

        // Act
        var pageOptions = options.GetPageOptions();

        // Assert
        Assert.Equal("Europe/London", pageOptions.TimezoneId);
        Assert.Equal("en-GB", pageOptions.Locale);
    }

    [Theory]
    [InlineData(ScrapingStrategy.Fast)]
    [InlineData(ScrapingStrategy.Resilient)]
    [InlineData(ScrapingStrategy.Stealth)]
    public void XOptions_ScrapingStrategy_CanBeSet(ScrapingStrategy strategy)
    {
        // Arrange
        var options = new XOptions { ScrapingStrategy = strategy };

        // Assert
        Assert.Equal(strategy, options.ScrapingStrategy);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void XOptions_MaxRetries_CanBeSet(int retries)
    {
        // Arrange
        var options = new XOptions { MaxRetries = retries };

        // Assert
        Assert.Equal(retries, options.MaxRetries);
    }

    [Fact]
    public void XOptions_StorageStatePath_CanBeNull()
    {
        // Arrange
        var options = new XOptions();

        // Assert
        Assert.Null(options.StorageStatePath);
    }

    [Fact]
    public void XOptions_StorageStatePath_CanBeSet()
    {
        // Arrange
        var path = "/data/x-auth.json";
        var options = new XOptions { StorageStatePath = path };

        // Assert
        Assert.Equal(path, options.StorageStatePath);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(2000)]
    [InlineData(5000)]
    public void XOptions_ThreadDelayMs_CanBeConfigured(int delay)
    {
        // Arrange
        var options = new XOptions { ThreadDelayMs = delay };

        // Assert
        Assert.Equal(delay, options.ThreadDelayMs);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(5, 20)]
    [InlineData(10, 50)]
    public void XOptions_MediaSizeLimits_CanBeConfigured(int imageSize, int videoSize)
    {
        // Arrange
        var options = new XOptions
        {
            MaxImageSizeMB = imageSize,
            MaxVideoSizeMB = videoSize
        };

        // Assert
        Assert.Equal(imageSize, options.MaxImageSizeMB);
        Assert.Equal(videoSize, options.MaxVideoSizeMB);
    }
}
