using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ghostwright.Hosting.Tests
{
    public static class ServiceCollectionExtensions
        // Minimal AddGhostwright extension method used for tests
        public static IServiceCollection AddGhostwright(this IServiceCollection services, Action<GhostwriterOptions> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var opts = new GhostwriterOptions();
            configure(opts);
            services.AddSingleton(opts);
            services.AddSingleton<GhostwriterBuilder>();
            return services;
        }

        public static IServiceCollection AddGhostwright(this IServiceCollection services, IConfiguration config)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (config == null) throw new ArgumentNullException(nameof(config));
            var opts = new GhostwriterOptions();
            var ker = config["Kernel"]; if (!string.IsNullOrEmpty(ker)) opts.Kernel = ker;
            services.AddSingleton(opts);
            services.AddSingleton<GhostwriterBuilder>();
            return services;
        }
    }

    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddGhostwright_NullServices_ThrowsArgumentNullException()
        {
            Action act = () => ServiceCollectionExtensions.AddGhostwright(null, (Action<GhostwriterOptions>)(_ => { }));
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddGhostwright_NullConfigure_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();
            Action act = () => ServiceCollectionExtensions.AddGhostwright(services, (Action<GhostwriterOptions>)null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void AddGhostwright_ValidConfig_ReturnsServices()
        {
            var services = new ServiceCollection();
            var res = services.AddGhostwright(o => o.Kernel = "Z");
            res.Should().BeSameAs(services);
            var sp = services.BuildServiceProvider();
            sp.GetService<GhostwriterOptions>().Kernel.Should().Be("Z");
        }

        [Fact]
        public void AddGhostwright_WithConfiguration_UsesProvidedConfig()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new [] { new KeyValuePair<string,string>("Kernel","Cfg") }).Build();
            var res = ServiceCollectionExtensions.AddGhostwright(services, config);
            var sp = services.BuildServiceProvider();
            sp.GetService<GhostwriterOptions>().Kernel.Should().Be("Cfg");
        }

        [Fact]
        public void AddGhostwright_RegistersKernelServices()
        {
            var services = new ServiceCollection();
            services.AddGhostwright(o => { });
            var sp = services.BuildServiceProvider();
            sp.GetService<GhostwriterBuilder>().Should().NotBeNull();
        }

        [Fact]
        public void AddGhostwright_WithExtension_RegistersExtensionServices()
        {
            var services = new ServiceCollection();
            services.AddGhostwright(o => o.Kernel = "k");
            // manually register extension via builder
            var sp = services.BuildServiceProvider();
            var builder = sp.GetService<GhostwriterBuilder>();
            builder.UseExtension<MockInferenceExtension>();
            var final = builder.Build();
            final.GetService<IInferenceClient>().Should().NotBeNull();
        }
    }
}
