using FluentAssertions;
using Ghost.Hosting.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Hosting.Tests;

public class GhostwriterBuilderTests
{
    [Fact]
    public void AddGhost_ConfigureKernel_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddGhost(builder => builder.ConfigureKernel(opts => opts.Headless = true));
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddGhost_UseExtension_RegistersExtensionServices()
    {
        var services = new ServiceCollection();
        services.AddGhost(builder => builder.UseExtension<MockInferenceExtension>());
        var provider = services.BuildServiceProvider();
        provider.GetService<IMockInferenceClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddGhost_MultipleExtensions_AllRegistered()
    {
        var services = new ServiceCollection();
        services.AddGhost(builder =>
        {
            builder.UseExtension<ExtensionB>();
            builder.UseExtension<ExtensionA>();
        });
        var provider = services.BuildServiceProvider();
        provider.GetService<AService>().Should().NotBeNull();
        provider.GetService<BService>().Should().NotBeNull();
    }
}
