using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.LinkedIn.Tests
{
    public class LinkedInOptionsTests
    {
        [Fact]
        public void Defaults_And_Setters()
        {
            var opts = new LinkedInOptions();
            opts.BaseUrl.Should().BeNull();
            opts.Timeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);

            opts.BaseUrl = "https://www.linkedin.com";
            opts.Timeout = System.TimeSpan.FromSeconds(20);
            opts.BaseUrl.Should().Be("https://www.linkedin.com");
            opts.Timeout.Should().Be(System.TimeSpan.FromSeconds(20));
        }
    }
}
