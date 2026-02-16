using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.Fixtures;

/// <summary>
/// Fixture class demonstrating an illegal plugin dependency pattern.
/// Used for negative control testing.
/// </summary>
internal sealed class IllegalPluginDependency
{
    private readonly bool _configured;

    public IllegalPluginDependency()
    {
        ServiceCollection services = new ServiceCollection();
        Ghost.Hosting.ServiceCollectionExtensions.AddGhost(services, _ => { });
        _configured = services.Count > 0;
    }
}
