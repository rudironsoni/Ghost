using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Hosting;
using Ghost.Abstractions;
using Ghost.Http;
using System.Net.Http;

namespace Ghost.Platform.Glassdoor;

public sealed class GlassdoorExtension : IExtension
{
    public string Name => "Glassdoor";
    public Version Version => new(1,0,0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<GlassdoorOptions>(configuration.GetSection("Ghost:Extensions:Glassdoor"));
            services.AddHttpClient<Internal.GlassdoorApiClient>()
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var provider = sp.GetRequiredService<IProxyProvider>();
                    return new HttpClientHandler
                    {
                        Proxy = new RotatingWebProxy(provider),
                        UseProxy = true
                    };
                });

            services.AddScoped<Ghost.Contracts.Jobs.IJobClient, GlassdoorJobClient>();
        }
}
