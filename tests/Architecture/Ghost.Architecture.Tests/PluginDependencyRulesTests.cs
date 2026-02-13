using FluentAssertions;
using Ghost.Engine.Abstractions.Engine;
using NetArchTest.Rules;
using Xunit;

namespace Ghost.Architecture.Tests
{
    public sealed class PluginDependencyRulesTests
    {
        [Fact]
        public void EngineAbstractions_ShouldNotDependOnHosting()
        {
            var result = Types
                .InAssembly(typeof(IGhostEngine).Assembly)
                .ShouldNot()
                .HaveDependencyOn("Ghost.Hosting")
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }

        [Fact]
        public void NegativeControl_PluginTypeDependingOnHosting_ShouldFailRule()
        {
            var result = Types
                .InAssembly(typeof(Ghost.Plugin.Fixtures.IllegalPluginDependency).Assembly)
                .That()
                .ResideInNamespace("Ghost.Plugin.Fixtures")
                .ShouldNot()
                .HaveDependencyOn("Ghost.Hosting")
                .GetResult();

            result.IsSuccessful.Should().BeFalse();
        }
    }
}

namespace Ghost.Plugin.Fixtures
{
    internal sealed class IllegalPluginDependency
    {
        private readonly bool _configured;

        public IllegalPluginDependency()
        {
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Ghost.Hosting.ServiceCollectionExtensions.AddGhost(services, _ => { });
            _configured = services.Count > 0;
        }
    }
}
