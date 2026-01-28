using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Platform.OpenAI;

/// <summary>
/// Registers the OpenAI (chatgpt.com) extension.
/// </summary>
public sealed class OpenAIExtension : Ghost.Contracts.IExtension
{
    public string Name => "OpenAI";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Inference.IInferenceClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenAIOptions>(configuration.GetSection("OpenAI"));
        services.AddScoped<Ghost.Contracts.Inference.IInferenceClient, OpenAIClient>();
    }
}
