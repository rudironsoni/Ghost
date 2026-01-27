using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.Google.Tests
{
    public class GoogleOptionsTests
    {
        [Fact]
        public void Defaults_AreReasonable()
        {
            var opts = new GoogleOptions();
            opts.ApiKey.Should().BeNull();
            opts.Timeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
        }

        [Fact]
        public void PropertySetters_Work()
        {
            var opts = new GoogleOptions { ApiKey = "k", Timeout = System.TimeSpan.FromSeconds(7) };
            opts.ApiKey.Should().Be("k");
            opts.Timeout.Should().Be(System.TimeSpan.FromSeconds(7));
        }
    }
}
