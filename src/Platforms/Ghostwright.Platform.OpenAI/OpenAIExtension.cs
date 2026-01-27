using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Platform.OpenAI;

/// <summary>
/// Registers the OpenAI (chatgpt.com) extension.
/// </summary>
public sealed class OpenAIExtension : Ghostwright.Contracts.IExtension
{
    public string Name => "OpenAI";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghostwright.Contracts.Inference.IInferenceClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghostwright.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAIOptions>(configuration.GetSection("OpenAI"));
        services.AddScoped<Ghostwright.Contracts.Inference.IInferenceClient, OpenAIClient>();
    }
}
