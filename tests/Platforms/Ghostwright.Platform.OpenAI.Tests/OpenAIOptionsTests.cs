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
            opts.BaseUrl.Should().NotBeNull();
            opts.ResponseTimeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
        }

        [Fact]
        public void PropertySetters_Work()
        {
            var opts = new OpenAIOptions
            {
                BaseUrl = "https://chatgpt.com",
                ResponseTimeout = System.TimeSpan.FromSeconds(5),
                DefaultModel = "gpt-3"
            };
            opts.BaseUrl.Should().Be("https://chatgpt.com");
            opts.ResponseTimeout.Should().Be(System.TimeSpan.FromSeconds(5));
            opts.DefaultModel.Should().Be("gpt-3");
        }
    }
}
