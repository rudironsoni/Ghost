using FluentAssertions;
using Xunit;

namespace Ghost.Platform.Google.Tests;

[Trait("Category", "Unit")]
[Collection("GooglePlatformTests")]
public class GoogleOptionsTests
{
    [Fact]
    public void DefaultsAreReasonable()
    {
        var opts = new GoogleOptions();
        // Ensure sub-options are present
        opts.Gemini.Should().NotBeNull();
        opts.Jobs.Should().NotBeNull();

        opts.Gemini!.BaseUrl.Should().NotBeNull();
        opts.Gemini.ResponseTimeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
        opts.Gemini.DefaultModel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PropertySettersWork()
    {
        var g = new Ghost.Platform.Google.Gemini.GeminiOptions { BaseUrl = "https://api.google.com", ResponseTimeout = System.TimeSpan.FromSeconds(7), DefaultModel = "gemini-test" };
        var opts = new GoogleOptions { Gemini = g };
        opts.Gemini.Should().NotBeNull();
        opts.Gemini!.BaseUrl.Should().Be("https://api.google.com");
        opts.Gemini.ResponseTimeout.Should().Be(System.TimeSpan.FromSeconds(7));
        opts.Gemini.DefaultModel.Should().Be("gemini-test");
    }
}
