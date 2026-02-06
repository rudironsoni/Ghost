using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Platform.Anthropic;

/// <summary>
/// Registers the Anthropic platform integration as an extension.
/// </summary>
public sealed class AnthropicExtension : IExtension
{
    /// <inheritdoc />
    public string Name => "Anthropic";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Inference.IInferenceClient) };

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AnthropicOptions>(configuration.GetSection("Anthropic"));
        services.AddScoped<Ghost.Contracts.Inference.IInferenceClient, AnthropicClient>();
    }
}
