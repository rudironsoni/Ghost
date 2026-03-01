using FluentAssertions;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInOptionsTests : ReliabilityTestBase
{
    public LinkedInOptionsTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void DefaultsAndSetters()
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
