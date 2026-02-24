using FluentAssertions;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInSessionPoolOptionsTests : ReliabilityTestBase
{
    public LinkedInSessionPoolOptionsTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void DefaultsAndSetters()
    {
        var options = new LinkedInSessionPoolOptions();

        options.MaxSize.Should().Be(20);
        options.WarmCount.Should().Be(5);
        options.MaxIdleTime.Should().Be(System.TimeSpan.FromMinutes(5));
        options.MaxLifetime.Should().Be(System.TimeSpan.FromHours(1));
        options.HealthCheckInterval.Should().Be(System.TimeSpan.FromMinutes(5));

        options.MaxSize = 10;
        options.WarmCount = 3;
        options.MaxIdleTime = System.TimeSpan.FromSeconds(30);
        options.MaxLifetime = System.TimeSpan.FromMinutes(10);
        options.HealthCheckInterval = System.TimeSpan.FromSeconds(15);

        options.MaxSize.Should().Be(10);
        options.WarmCount.Should().Be(3);
        options.MaxIdleTime.Should().Be(System.TimeSpan.FromSeconds(30));
        options.MaxLifetime.Should().Be(System.TimeSpan.FromMinutes(10));
        options.HealthCheckInterval.Should().Be(System.TimeSpan.FromSeconds(15));
    }
}
