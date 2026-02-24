using FluentAssertions;
using Ghost.Resilience;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Tests.Resilience;

public class RetryPolicyOptionsTests : ReliabilityTestBase
{
    public RetryPolicyOptionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DefaultsAreExpected()
    {
        var options = new RetryPolicyOptions();

        options.MaxRetries.Should().Be(3);
        options.BaseDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxDelay.Should().Be(TimeSpan.FromSeconds(30));
        options.UseJitter.Should().BeTrue();
    }
}
