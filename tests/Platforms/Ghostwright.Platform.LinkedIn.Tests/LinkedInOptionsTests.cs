using FluentAssertions;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Platform.LinkedIn.Tests;

public class LinkedInOptionsTests
{
    [Fact]
    public void Defaults_And_Setters()
    {
        var opts = new LinkedInOptions();
        opts.BaseUrl.Should().Be("https://www.linkedin.com");
        opts.PageLoadTimeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);

        opts.BaseUrl = "https://www.linkedin.com";
        opts.PageLoadTimeout = System.TimeSpan.FromSeconds(20);
        opts.BaseUrl.Should().Be("https://www.linkedin.com");
        opts.PageLoadTimeout.Should().Be(System.TimeSpan.FromSeconds(20));
    }
}
