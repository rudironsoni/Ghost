using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Hosting;

namespace Ghost.Platform.LinkedIn;

/// <summary>
/// Registers LinkedIn-related clients.
/// </summary>
public sealed class LinkedInExtension : IExtension
{
    public string Name => "LinkedIn";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Social.ISocialClient), typeof(Ghost.Contracts.Jobs.IJobClient), typeof(Ghost.Contracts.News.INewsClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LinkedInOptions>(configuration.GetSection("LinkedIn"));
        // Authenticator used by LinkedInSocialClient for logging in / cookie handling
        services.AddTransient<Internal.LinkedInAuthenticator>();
        // GuestJobSearch implements guest API scraping logic
        services.AddScoped<Internal.GuestJobSearch>();
        services.AddScoped<Ghost.Contracts.Social.ISocialClient, LinkedInSocialClient>();
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient, LinkedInJobClient>();
        services.AddScoped<Ghost.Contracts.News.INewsClient, LinkedInNewsClient>();
    }
}
