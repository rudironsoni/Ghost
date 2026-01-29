using Microsoft.Extensions.DependencyInjection;
using Ghost.Models;
using Microsoft.Extensions.Configuration;
using Ghost.Platform.Indeed.Internal;
using Ghost.Contracts.Jobs;
using Ghost.Abstractions;
using Ghost.Http;
using System.Net.Http;

namespace Ghost.Platform.Indeed;

public static class IndeedExtension
{
        public static IServiceCollection AddIndeed(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<IndeedOptions>(config.GetSection("Indeed"));
            var opts = config.GetSection("Indeed").Get<IndeedOptions>() ?? new IndeedOptions();

            if (!opts.Enabled) return services;

            // register IndeedOptions for ApiClient constructor
            services.AddSingleton(opts);

            services.AddHttpClient<IndeedApiClient>(client => { client.Timeout = System.TimeSpan.FromSeconds(30); })
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var provider = sp.GetRequiredService<IProxyProvider>();
                    return new HttpClientHandler
                    {
                        Proxy = new RotatingWebProxy(provider),
                        UseProxy = true
                    };
                });

            services.AddSingleton<IndeedJobClient>();
            services.AddSingleton<IJobClient>(sp => sp.GetRequiredService<IndeedJobClient>());

            return services;
        }
}
