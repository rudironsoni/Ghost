using FluentAssertions;
using Ghostwright.Hosting.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostwright.Hosting.Tests;

public class GhostwriterBuilderTests
{
    [Fact]
    public void AddGhostwright_ConfigureKernel_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddGhostwright(builder => builder.ConfigureKernel(opts => opts.Headless = true));
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddGhostwright_UseExtension_RegistersExtensionServices()
    {
        var services = new ServiceCollection();
        services.AddGhostwright(builder => builder.UseExtension<MockInferenceExtension>());
        var provider = services.BuildServiceProvider();
        provider.GetService<IMockInferenceClient>().Should().NotBeNull();
    }

    [Fact]
    public void AddGhostwright_MultipleExtensions_AllRegistered()
    {
        var services = new ServiceCollection();
        services.AddGhostwright(builder =>
        {
            builder.UseExtension<ExtensionB>();
            builder.UseExtension<ExtensionA>();
        });
        var provider = services.BuildServiceProvider();
        provider.GetService<AService>().Should().NotBeNull();
        provider.GetService<BService>().Should().NotBeNull();
    }
}
