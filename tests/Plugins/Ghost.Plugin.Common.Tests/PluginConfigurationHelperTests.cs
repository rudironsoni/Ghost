using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ghost.Plugin.Common.Tests;

public class PluginConfigurationHelperTests
{
    [Fact]
    public void GetSectionName_ReturnsCorrectFormat()
    {
        // Arrange & Act
        var sectionName = PluginConfigurationHelper.GetSectionName<TestOptions>();

        // Assert
        Assert.Equal("Test", sectionName);
    }

    private sealed class TestOptions
    {
        public string Value { get; set; } = string.Empty;
    }
}
