using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Contracts.Tests;

public class IExtensionTests
{
    private sealed class FakeExtension : IExtension
    {
        public static string Name => "fake";
        public static Version Version => new(1, 2, 3);
        public static IReadOnlyList<Type> ProvidedServices => new[] { typeof(string) };
        public static IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
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
    public void ImplementationPropertiesWorkAndConfigureServicesExecutes()
    {
        var ext = new FakeExtension();
        FakeExtension.Name.Should().Be("fake");
        FakeExtension.Version.Should().Be(new Version(1, 2, 3));
        FakeExtension.ProvidedServices.Should().Contain(typeof(string));

        var services = new ServiceCollection();
        ext.ConfigureServices(services, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        services.Should().Contain(sd => sd.ServiceType == typeof(FakeService));
    }
}
