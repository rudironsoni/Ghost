using FluentAssertions;
using Ghost.Hosting.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Hosting.Tests;

public class ExtensionLoaderTests
{
    [Fact]
    public void ValidateExtensionsNoExtensionsSucceeds()
    {
        Action act = () => ExtensionLoader.ValidateExtensions([]);
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateExtensionsMissingDependencyThrowsExtensionException()
    {
        var extensions = new IExtension[] { new MockMissingDepExtension() };
        Action act = () => ExtensionLoader.ValidateExtensions(extensions);
        act.Should().Throw<ExtensionException>();
    }

    [Fact]
    public void ValidateExtensionsCircularDependencyThrowsExtensionException()
    {
        var extensions = new IExtension[] { new Circular1(), new Circular2() };
        Action act = () => ExtensionLoader.ValidateExtensions(extensions);
        act.Should().Throw<ExtensionException>();
    }

    [Fact]
    public void LoadExtensionsRegistersServicesInOrder()
    {
        var extensions = new IExtension[] { new ExtensionB(), new ExtensionA() };
        var services = new ServiceCollection();
        IConfigurationRoot config = new ConfigurationBuilder().Build();
        ExtensionLoader.LoadExtensions(extensions, services, config);
        services.Should().Contain(sd => sd.ServiceType == typeof(AService));
        services.Should().Contain(sd => sd.ServiceType == typeof(BService));
    }
}
