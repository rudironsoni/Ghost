using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.Anthropic.Tests
{
    public class AnthropicOptionsTests
    {
        [Fact]
        public void Defaults_AreReasonable()
        {
            var opts = new AnthropicOptions();
            opts.ApiKey.Should().BeNull();
            opts.BaseUrl.Should().BeNull();
            opts.Timeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
        }

        [Fact]
        public void PropertySetters_Work()
        {
            var opts = new AnthropicOptions
            {
                ApiKey = "test",
                BaseUrl = "https://api.anthropic.com",
                Timeout = System.TimeSpan.FromSeconds(10)
            };

            opts.ApiKey.Should().Be("test");
            opts.BaseUrl.Should().Be("https://api.anthropic.com");
            opts.Timeout.Should().Be(System.TimeSpan.FromSeconds(10));
        }
    }
}
