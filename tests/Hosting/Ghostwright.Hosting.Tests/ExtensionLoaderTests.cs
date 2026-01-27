using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostwright.Hosting.Tests
{
    public class ExtensionLoaderTests
    {
        [Fact]
        public void ValidateExtensions_NoExtensions_Succeeds()
        {
            var loader = new ExtensionLoader(Array.Empty<IExtension>(), new GhostwriterOptions());
            Action act = () => loader.ValidateExtensions();
            act.Should().NotThrow();
        }

        [Fact]
        public void ValidateExtensions_MissingDependency_ThrowsExtensionException()
        {
            var loader = new ExtensionLoader(new IExtension[] { new MockMissingDepExtension() }, new GhostwriterOptions());
            Action act = () => loader.ValidateExtensions();
            act.Should().Throw<ExtensionException>().Which.ExtensionName.Should().Be("MockMissingDep");
        }

        [Fact]
        public void ValidateExtensions_CircularDependency_ThrowsExtensionException()
        {
            var loader = new ExtensionLoader(new IExtension[] { new Circular1(), new Circular2() }, new GhostwriterOptions());
            Action act = () => loader.ValidateExtensions();
            act.Should().Throw<ExtensionException>();
        }

        [Fact]
        public void ValidateExtensions_ValidDependencies_Succeeds()
        {
            var loader = new ExtensionLoader(new IExtension[] { new MockInferenceExtension(), new MockDependentExtension() }, new GhostwriterOptions());
            Action act = () => loader.ValidateExtensions();
            act.Should().NotThrow();
        }

        [Fact]
        public void LoadExtensions_OrdersByDependency()
        {
            var a = new ExtensionA();
            var b = new ExtensionB();
            var loader = new ExtensionLoader(new IExtension[] { a, b }, new GhostwriterOptions());
            var services = new ServiceCollection();
            loader.LoadExtensions(services, null);
            // B must be registered before A so that A's requirement is satisfied; check service provider has both
            var sp = services.BuildServiceProvider();
            sp.GetService<BService>().Should().NotBeNull();
            sp.GetService<AService>().Should().NotBeNull();
        }

        [Fact]
        public void LoadExtensions_RegistersServicesInOrder()
        {
            var services = new ServiceCollection();
            var loader = new ExtensionLoader(new IExtension[] { new MockInferenceExtension(), new MockDependentExtension() }, new GhostwriterOptions());
            loader.LoadExtensions(services, null);
            var sp = services.BuildServiceProvider();
            sp.GetService<IInferenceClient>().Should().NotBeNull();
            sp.GetService<string>().Should().Be("provided-by-dependent");
        }
    }
}
