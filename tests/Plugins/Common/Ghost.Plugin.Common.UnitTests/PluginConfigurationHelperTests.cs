using Ghost.Testing.Reliability;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.Common.Tests;

public class PluginConfigurationHelperTests : ReliabilityTestBase
{
    public PluginConfigurationHelperTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void GetSectionName_ReturnsCorrectFormat()
    {
        // Arrange & Act
        string sectionName = PluginConfigurationHelper.GetSectionName<TestOptions>();

        // Assert
        Assert.Equal("Test", sectionName);
    }

    private sealed class TestOptions
    {
        public string Value { get; set; } = string.Empty;
    }
}
