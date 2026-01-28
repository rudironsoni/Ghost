using Microsoft.AspNetCore.Builder;

namespace Ghost.Hosting.WebApi;

/// <summary>
/// Extensions for WebApplicationBuilder to register Ghost hosting.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Ghost hosting to an ASP.NET Core application.
    /// </summary>
    /// <param name="builder">Web application builder.</param>
    /// <param name="configure">Builder configure callback.</param>
    /// <returns>The same builder instance.</returns>
    public static WebApplicationBuilder AddGhost(
        this WebApplicationBuilder builder,
        Action<GhostwriterBuilder> configure)
    {
        builder.Services.AddGhost(builder.Configuration, configure);
        return builder;
    }
}
