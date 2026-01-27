using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.Google.Tests;

public class GoogleOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var opts = new GoogleOptions();
        opts.BaseUrl.Should().NotBeNull();
        opts.ResponseTimeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
        opts.DefaultModel.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PropertySetters_Work()
    {
        var opts = new GoogleOptions { BaseUrl = "https://api.google.com", ResponseTimeout = System.TimeSpan.FromSeconds(7), DefaultModel = "gemini-test" };
        opts.BaseUrl.Should().Be("https://api.google.com");
        opts.ResponseTimeout.Should().Be(System.TimeSpan.FromSeconds(7));
        opts.DefaultModel.Should().Be("gemini-test");
    }
}
