using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.OpenAI.Tests
{
    public class OpenAIOptionsTests
    {
        [Fact]
        public void Defaults_AreReasonable()
        {
            var opts = new OpenAIOptions();
            opts.ApiKey.Should().BeNull();
            opts.BaseUrl.Should().BeNull();
            opts.Timeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
        }

        [Fact]
        public void PropertySetters_Work()
        {
            var opts = new OpenAIOptions
            {
                ApiKey = "key",
                BaseUrl = "https://api.openai.com",
                Timeout = System.TimeSpan.FromSeconds(5)
            };
            opts.ApiKey.Should().Be("key");
            opts.BaseUrl.Should().Be("https://api.openai.com");
            opts.Timeout.Should().Be(System.TimeSpan.FromSeconds(5));
        }
    }
}
