using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Hosting;

namespace Ghost.Hosting.Tests.Helpers;

// Marker interface for mock inference client
internal interface IMockInferenceClient { }

internal sealed class MockInferenceExtension : IExtension
{
    public string Name => "MockInference";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => [typeof(IMockInferenceClient)];
    public IReadOnlyList<Type> RequiredServices => [];
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IMockInferenceClient, MockInferenceClient>();
    }
}

internal sealed class MockInferenceClient : IMockInferenceClient { }

internal sealed class MockDependentExtension : IExtension
{
    public string Name => "MockDependent";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => [typeof(string)];
    public IReadOnlyList<Type> RequiredServices => [typeof(IMockInferenceClient)];
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton("provided-by-dependent");
    }
}

internal sealed class MockMissingDepExtension : IExtension
{
    public string Name => "MockMissingDep";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => [typeof(Guid)];
    public IReadOnlyList<Type> RequiredServices => [typeof(int)]; // nobody provides int
    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(typeof(Guid), Guid.Empty);
    }
}

internal sealed class ExtensionA : IExtension
{
    public string Name => "A";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => [typeof(AService)];
    public IReadOnlyList<Type> RequiredServices => [typeof(BService)];
    public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<AService>();
}

internal sealed class ExtensionB : IExtension
{
    public string Name => "B";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => [typeof(BService)];
    public IReadOnlyList<Type> RequiredServices => [];
    public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<BService>();
}

internal sealed class Circular1 : IExtension
{
    public string Name => "C1";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => [typeof(C1Service)];
    public IReadOnlyList<Type> RequiredServices => [typeof(C2Service)];
    public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<C1Service>();
}

internal sealed class Circular2 : IExtension
{
    public string Name => "C2";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => [typeof(C2Service)];
    public IReadOnlyList<Type> RequiredServices => [typeof(C1Service)];
    public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<C2Service>();
}

internal sealed class AService { }
internal sealed class BService { }
internal sealed class C1Service { }
internal sealed class C2Service { }
