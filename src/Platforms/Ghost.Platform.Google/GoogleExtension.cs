using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Platform.Google;

/// <summary>
/// Registers the Google/Gemini extension.
/// </summary>
public sealed class GoogleExtension : Ghost.Contracts.IExtension
{
    public string Name => "Google";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Inference.IInferenceClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // bind using configuration section
        services.Configure<GoogleOptions>(configuration.GetSection("Google"));
        services.AddScoped<Ghost.Contracts.Inference.IInferenceClient, GoogleClient>();
    }
}
