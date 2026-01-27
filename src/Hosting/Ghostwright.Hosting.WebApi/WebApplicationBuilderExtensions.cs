using Microsoft.AspNetCore.Builder;

namespace Ghostwright.Hosting.WebApi;

/// <summary>
/// Extensions for WebApplicationBuilder to register Ghostwright hosting.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Ghostwright hosting to an ASP.NET Core application.
    /// </summary>
    /// <param name="builder">Web application builder.</param>
    /// <param name="configure">Builder configure callback.</param>
    /// <returns>The same builder instance.</returns>
    public static WebApplicationBuilder AddGhostwright(
        this WebApplicationBuilder builder,
        Action<GhostwriterBuilder> configure)
    {
        builder.Services.AddGhostwright(builder.Configuration, configure);
        return builder;
    }
}
