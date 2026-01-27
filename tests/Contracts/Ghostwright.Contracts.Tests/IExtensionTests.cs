using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace Ghostwright.Contracts.Tests;

public class IExtensionTests
{
    private sealed class FakeExtension : IExtension
    {
        public string Name => "fake";
        public Version Version => new(1, 2, 3);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(string) };
        public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<FakeService>(new FakeService(42));
        }
    }

    private sealed class FakeService
    {
        public int Value { get; }
        public FakeService(int value) => Value = value;
    }

    [Fact]
    public void Implementation_Properties_WorkAndConfigureServicesExecutes()
    {
        var ext = new FakeExtension();
        ext.Name.Should().Be("fake");
        ext.Version.Should().Be(new Version(1,2,3));
        ext.ProvidedServices.Should().Contain(typeof(string));

        var services = new ServiceCollection();
        ext.ConfigureServices(services, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.Should().Contain(sd => sd.ServiceType == typeof(int));
    }
}
