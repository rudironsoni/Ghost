using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghostwright.Platform.Anthropic;

/// <summary>
/// Registers the Anthropic platform integration as an extension.
/// </summary>
public sealed class AnthropicExtension : Ghostwright.Contracts.IExtension
{
    /// <inheritdoc />
    public string Name => "Anthropic";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghostwright.Contracts.Inference.IInferenceClient) };

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghostwright.IBrowserSession) };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AnthropicOptions>(configuration.GetSection("Anthropic"));
        services.AddScoped<Ghostwright.Contracts.Inference.IInferenceClient, AnthropicClient>();
    }
}
