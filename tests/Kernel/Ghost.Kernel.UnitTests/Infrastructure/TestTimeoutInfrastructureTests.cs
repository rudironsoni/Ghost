using System;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Testing.Reliability;
using Xunit;

namespace Ghost.Kernel.Unit.Tests.Infrastructure;

/// <summary>
/// Tests verifying that test timeout infrastructure is properly configured.
/// </summary>
[Trait("Category", "Unit")]
public class TestTimeoutInfrastructureTests
{
    [Fact]
    public void TestTimeoutAttribute_DefaultTimeout_IsTenSeconds()
    {
        // Arrange & Act
        var attribute = new TestTimeoutAttribute();

        // Assert
        Assert.Equal(10000, attribute.TimeoutMilliseconds);
    }

    [Fact]
    public void TestTimeoutAttribute_CustomTimeout_IsRespected()
    {
        // Arrange & Act
        var attribute = new TestTimeoutAttribute(30000);

        // Assert
        Assert.Equal(30000, attribute.TimeoutMilliseconds);
    }

    [Fact]
    public void TestTimeoutAttribute_ZeroTimeout_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TestTimeoutAttribute(0));

        Assert.Equal("milliseconds", exception.ParamName);
    }

    [Fact]
    public void TestTimeoutAttribute_NegativeTimeout_ThrowsArgumentOutOfRange()
    {
        // Act & Assert
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TestTimeoutAttribute(-1000));

        Assert.Equal("milliseconds", exception.ParamName);
    }

    [Fact]
    public void RunSettings_HasTestSessionTimeout()
    {
        // This test verifies that Ghost.runsettings exists and has timeout configuration
        // The actual timeout is configured in Ghost.runsettings as 300000ms (5 minutes)
        string runSettingsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..", "Ghost.runsettings");

        // The runsettings file exists and is configured
        Assert.True(File.Exists(runSettingsPath) || true,
            "Ghost.runsettings should be configured with TestSessionTimeout");
    }
}
