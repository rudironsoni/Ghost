using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostwright.Hosting.Tests
{
    public class GhostwriterBuilderTests
    {
        [Fact]
        public void ConfigureKernel_SetsOptions_OptionsApplied()
        {
            var builder = new GhostwriterBuilder();
            builder.ConfigureKernel(o => o.Kernel = "MyKernel");
            var sp = builder.Build();

            var opts = sp.GetService<GhostwriterOptions>();
            opts.Should().NotBeNull();
            opts.Kernel.Should().Be("MyKernel");
        }

        [Fact]
        public void UseExtension_Generic_RegistersExtension()
        {
            var builder = new GhostwriterBuilder();
            builder.UseExtension<MockInferenceExtension>();
            var sp = builder.Build();

            sp.GetService<IInferenceClient>().Should().NotBeNull();
        }

        [Fact]
        public void UseExtension_Instance_RegistersExtension()
        {
            var builder = new GhostwriterBuilder();
            builder.UseExtension(new MockInferenceExtension());
            var sp = builder.Build();

            sp.GetService<IInferenceClient>().Should().NotBeNull();
        }

        [Fact]
        public void UseExtension_Multiple_AllRegistered()
        {
            var builder = new GhostwriterBuilder();
            builder.UseExtension<MockInferenceExtension>();
            builder.UseExtension<MockDependentExtension>();
            var sp = builder.Build();

            sp.GetService<IInferenceClient>().Should().NotBeNull();
            sp.GetService<string>().Should().Be("provided-by-dependent");
        }

        [Fact]
        public void Build_NoExtensions_RegistersKernelOnly()
        {
            var builder = new GhostwriterBuilder();
            builder.ConfigureKernel(o => o.Kernel = "K");
            var sp = builder.Build();
            sp.GetService<GhostwriterOptions>().Kernel.Should().Be("K");
        }

        [Fact]
        public void Build_WithExtensions_RegistersAll()
        {
            var builder = new GhostwriterBuilder();
            builder.UseExtension<MockInferenceExtension>();
            builder.UseExtension<MockDependentExtension>();
            var sp = builder.Build();

            sp.GetService<IInferenceClient>().Should().NotBeNull();
            sp.GetService<string>().Should().Be("provided-by-dependent");
        }
    }
}
