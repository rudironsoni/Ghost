using FluentAssertions;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.OpenAI.Tests;

public class OpenAIOptionsTests : ReliabilityTestBase
{
    public OpenAIOptionsTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void DefaultsAreReasonable()
    {
        var opts = new OpenAIOptions();
        opts.BaseUrl.Should().NotBeNull();
        opts.ResponseTimeout.Should().BeGreaterOrEqualTo(System.TimeSpan.Zero);
    }

    [Fact]
    public void PropertySettersWork()
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
