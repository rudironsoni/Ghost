using System;
using System.Collections.Generic;
using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Hosting.Tests.Helpers;

// Marker interface for mock inference client
internal interface IMockInferenceClient { }

internal sealed class MockInferenceExtension : IExtension
{
    public static string Name => "MockInference";
    public static Version Version => new(1, 0, 0);
    public static IReadOnlyList<Type> ProvidedServices => [typeof(IMockInferenceClient)];
    public static IReadOnlyList<Type> RequiredServices => [];
    public static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IMockInferenceClient, MockInferenceClient>();
    }
}

internal sealed class MockInferenceClient : IMockInferenceClient { }

internal sealed class MockDependentExtension : IExtension
{
    public static string Name => "MockDependent";
    public static Version Version => new(1, 0, 0);
    public static IReadOnlyList<Type> ProvidedServices => [typeof(string)];
    public static IReadOnlyList<Type> RequiredServices => [typeof(IMockInferenceClient)];
    public static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton("provided-by-dependent");
    }
}

internal sealed class MockMissingDepExtension : IExtension
{
    public static string Name => "MockMissingDep";
    public static Version Version => new(1, 0, 0);
    public static IReadOnlyList<Type> ProvidedServices => [typeof(Guid)];
    public static IReadOnlyList<Type> RequiredServices => [typeof(int)]; // nobody provides int
    public static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(typeof(Guid), Guid.Empty);
    }
}

internal sealed class ExtensionA : IExtension
{
    public static string Name => "A";
    public static Version Version => new(1, 0, 0);
    public static IReadOnlyList<Type> ProvidedServices => [typeof(AService)];
    public static IReadOnlyList<Type> RequiredServices => [typeof(BService)];
    public static void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<AService>();
}

internal sealed class ExtensionB : IExtension
{
    public static string Name => "B";
    public static Version Version => new(1, 0, 0);
    public static IReadOnlyList<Type> ProvidedServices => [typeof(BService)];
    public static IReadOnlyList<Type> RequiredServices => [];
    public static void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<BService>();
}

internal sealed class Circular1 : IExtension
{
    public static string Name => "C1";
    public static Version Version => new(1, 0, 0);
    public static IReadOnlyList<Type> ProvidedServices => [typeof(C1Service)];
    public static IReadOnlyList<Type> RequiredServices => [typeof(C2Service)];
    public static void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<C1Service>();
}

internal sealed class Circular2 : IExtension
{
    public static string Name => "C2";
    public static Version Version => new(1, 0, 0);
    public static IReadOnlyList<Type> ProvidedServices => [typeof(C2Service)];
    public static IReadOnlyList<Type> RequiredServices => [typeof(C1Service)];
    public static void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<C2Service>();
}

internal sealed class AService { }
internal sealed class BService { }
internal sealed class C1Service { }
internal sealed class C2Service { }
