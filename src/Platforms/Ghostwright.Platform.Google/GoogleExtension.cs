using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Platform.Google;

/// <summary>
/// Registers the Google/Gemini extension.
/// </summary>
public sealed class GoogleExtension : Ghostwright.Contracts.IExtension
{
    public string Name => "Google";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghostwright.Contracts.Inference.IInferenceClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghostwright.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // bind using configuration section
        services.Configure<GoogleOptions>(configuration.GetSection("Google"));
        services.AddScoped<Ghostwright.Contracts.Inference.IInferenceClient, GoogleClient>();
    }
}
