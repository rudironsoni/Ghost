using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.Anthropic.Tests;

public class AnthropicOptionsTests
{
        [Fact]
        public void Defaults_AreReasonable()
        {
            var opts = new AnthropicOptions();
            opts.BaseUrl.Should().NotBeNull();
            opts.ResponseTimeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
            opts.DefaultModel.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void PropertySetters_Work()
        {
            var opts = new AnthropicOptions
            {
                BaseUrl = "https://api.anthropic.com",
                ResponseTimeout = System.TimeSpan.FromSeconds(10),
                DefaultModel = "claude-test"
            };

            opts.BaseUrl.Should().Be("https://api.anthropic.com");
            opts.ResponseTimeout.Should().Be(System.TimeSpan.FromSeconds(10));
            opts.DefaultModel.Should().Be("claude-test");
        }
    }
