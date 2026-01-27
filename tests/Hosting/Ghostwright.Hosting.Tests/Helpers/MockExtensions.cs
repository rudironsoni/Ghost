using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Hosting
{
    internal class MockInferenceExtension : IExtension
    {
        public string Name => "MockInference";
        public Version Version => new(1, 0, 0);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(IInferenceClient) };
        public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
        public void ConfigureServices(IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<IInferenceClient, MockInferenceClient>();
        }
    }

    internal class MockInferenceClient : IInferenceClient { }

    internal class MockDependentExtension : IExtension
    {
        public string Name => "MockDependent";
        public Version Version => new(1, 0, 0);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(string) };
        public IReadOnlyList<Type> RequiredServices => new[] { typeof(IInferenceClient) };
        public void ConfigureServices(IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<string>("provided-by-dependent");
        }
    }

    internal class MockMissingDepExtension : IExtension
    {
        public string Name => "MockMissingDep";
        public Version Version => new(1, 0, 0);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Guid) };
        public IReadOnlyList<Type> RequiredServices => new[] { typeof(Int32) }; // nobody provides int
        public void ConfigureServices(IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<Guid>(Guid.Empty);
        }
    }

    internal class ExtensionA : IExtension
    {
        public string Name => "A";
        public Version Version => new(1,0,0);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(AService) };
        public IReadOnlyList<Type> RequiredServices => new[] { typeof(BService) };
        public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<AService>();
    }

    internal class ExtensionB : IExtension
    {
        public string Name => "B";
        public Version Version => new(1,0,0);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(BService) };
        public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();
        public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<BService>();
    }

    internal class Circular1 : IExtension
    {
        public string Name => "C1";
        public Version Version => new(1,0,0);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(C1Service) };
        public IReadOnlyList<Type> RequiredServices => new[] { typeof(C2Service) };
        public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<C1Service>();
    }

    internal class Circular2 : IExtension
    {
        public string Name => "C2";
        public Version Version => new(1,0,0);
        public IReadOnlyList<Type> ProvidedServices => new[] { typeof(C2Service) };
        public IReadOnlyList<Type> RequiredServices => new[] { typeof(C1Service) };
        public void ConfigureServices(IServiceCollection services, IConfiguration config) => services.AddSingleton<C2Service>();
    }

    internal class AService { }
    internal class BService { }
    internal class C1Service { }
    internal class C2Service { }
}
