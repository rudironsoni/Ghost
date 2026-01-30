using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Hosting;

namespace Ghost.Platform.Tecnoempleo;

/// <summary>
/// Registers the Tecnoempleo extension.
/// </summary>
public sealed class TecnoempleoHostingExtension : Ghost.Hosting.IExtension
{
    public string Name => "Tecnoempleo";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // diagnostic logging to help determine whether extension is applied and options bound
        try
        {
            Console.WriteLine("Configuring TecnoempleoExtension...");
            Console.Out.Flush();
        }
        catch { }

        // bind using configuration section
        services.Configure<Jobs.TecnoempleoOptions>(configuration.GetSection("Tecnoempleo"));

        var rootOpts = new Jobs.TecnoempleoOptions();
        configuration.GetSection("Tecnoempleo").Bind(rootOpts);
        try
        {
            Console.WriteLine($"Tecnoempleo options: Enabled = {rootOpts.EnableRateLimiting}");
        }
        catch { }

        // Tecnoempleo Job Client
        services.AddTecnoempleo(configuration);
    }
}