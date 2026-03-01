using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Ghost.Architecture.Tests;

public sealed class CircularDependencyTests : ReliabilityTestBase
{
    public CircularDependencyTests(ITestOutputHelper output) : base(output) { }

    private static readonly ArchUnitNET.Domain.Architecture Arch = new ArchLoader()
        .LoadAssemblies(
            typeof(Ghost.Contracts.IExtension).Assembly,
            typeof(Ghost.Engine.Abstractions.Engine.IGhostEngine).Assembly,
            typeof(Ghost.Kernel.GhostKernel).Assembly,
            typeof(Ghost.Hosting.IExtension).Assembly,
            typeof(Ghost.Sdk.Throttling.AutoThrottle).Assembly
        )
        .Build();

    [Fact]
    public void EngineAbstractions_ShouldNotDependOn_Kernel()
    {
        Types().That().ResideInNamespace("Ghost.Engine.Abstractions..")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("Ghost")
                    .And().DoNotResideInNamespace("Ghost.Contracts..")
                    .And().DoNotResideInNamespace("Ghost.Engine.."))
            .Because("Engine Abstractions should not depend on Kernel to prevent circular dependencies")
            .WithoutRequiringPositiveResults()
            .Check(Arch);
    }

    [Fact]
    public void Kernel_ShouldNotDependOn_Hosting()
    {
        Types().That().ResideInNamespace("Ghost")
            .And().DoNotResideInNamespace("Ghost.Contracts..")
            .And().DoNotResideInNamespace("Ghost.Engine..")
            .And().DoNotResideInNamespace("Ghost.Hosting..")
            .And().DoNotResideInNamespace("Ghost.Sdk..")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("Ghost.Hosting.."))
            .Because("Kernel should not depend on Hosting to prevent circular dependencies")
            .WithoutRequiringPositiveResults()
            .Check(Arch);
    }

    [Fact]
    public void DependencyDirection_ShouldFollow_LayerHierarchy()
    {
        Types().That().ResideInNamespace("Ghost.Contracts..")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("Ghost.")
                    .And().DoNotResideInNamespace("Ghost.Contracts.."))
            .WithoutRequiringPositiveResults()
            .Check(Arch);

        Types().That().ResideInNamespace("Ghost.Engine.Abstractions..")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("Ghost.")
                    .And().DoNotResideInNamespace("Ghost.Contracts..")
                    .And().DoNotResideInNamespace("Ghost.Engine.Abstractions.."))
            .WithoutRequiringPositiveResults()
            .Check(Arch);

        Types().That().ResideInNamespace("Ghost")
            .And().DoNotResideInNamespace("Ghost.Contracts..")
            .And().DoNotResideInNamespace("Ghost.Engine..")
            .And().DoNotResideInNamespace("Ghost.Hosting..")
            .And().DoNotResideInNamespace("Ghost.Sdk..")
            .Should().NotDependOnAny(
                Types().That().ResideInNamespace("Ghost.Hosting..")
                    .Or().ResideInNamespace("Ghost.Sdk.."))
            .WithoutRequiringPositiveResults()
            .Check(Arch);
    }
}
