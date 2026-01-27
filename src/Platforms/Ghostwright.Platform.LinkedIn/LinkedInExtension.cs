using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Platform.LinkedIn;

/// <summary>
/// Registers LinkedIn-related clients.
/// </summary>
public sealed class LinkedInExtension : Ghostwright.Contracts.IExtension
{
    public string Name => "LinkedIn";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghostwright.Contracts.Social.ISocialClient), typeof(Ghostwright.Contracts.Jobs.IJobClient), typeof(Ghostwright.Contracts.News.INewsClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghostwright.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LinkedInOptions>(configuration.GetSection("LinkedIn"));
        services.AddScoped<Ghostwright.Contracts.Social.ISocialClient, LinkedInSocialClient>();
        services.AddScoped<Ghostwright.Contracts.Jobs.IJobClient, LinkedInJobClient>();
        services.AddScoped<Ghostwright.Contracts.News.INewsClient, LinkedInNewsClient>();
    }
}
