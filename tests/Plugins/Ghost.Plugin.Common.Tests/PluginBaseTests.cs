using Ghost.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Common.Tests;

public class PluginBaseTests
{
    [Fact]
    public void PluginBase_Implements_IExtension()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Assert
        Assert.IsAssignableFrom<IExtension>(plugin);
    }

    [Fact]
    public void PluginBase_ConfigureServices_DoesNotThrow()
    {
        // Arrange
        var plugin = new TestPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act & Assert
        var exception = Record.Exception(() =>
            plugin.ConfigureServices(services, configuration));
        
        Assert.Null(exception);
    }

    private sealed class TestPlugin : PluginBase
    {
        public override string Name => "TestPlugin";
        public override System.Version Version => new System.Version(1, 0, 0);
    }
}
