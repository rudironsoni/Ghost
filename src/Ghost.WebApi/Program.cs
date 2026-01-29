using Ghost.Hosting;
using Ghost.Abstractions;
using Ghost.Utilities;
using Ghost.WebApi.Features.LinkedIn;
using Ghost.WebApi.Features.Jobs;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Health checks
builder.Services.AddHealthChecks();
// Register HTTP client and proxy provider required by Ghost
builder.Services.AddHttpClient();
// Bind ProxyOptions from configuration
builder.Services.Configure<Ghost.Core.ProxyOptions>(builder.Configuration.GetSection("Ghost:Proxy"));
builder.Services.AddSingleton<Ghost.Abstractions.IProxyProvider, Ghost.Services.RotatingProxyProvider>();

// Register available proxy sources using configuration sections
// Dynamic Proxy Source Registration
var proxySection = builder.Configuration.GetSection("Ghost:Proxy");
foreach (var child in proxySection.GetChildren())
{
    if (child.Key.Equals("Strategy", StringComparison.OrdinalIgnoreCase)) continue;

    var config = new Ghost.Core.ProxySourceConfig();
    child.Bind(config);

    if (!config.Enabled) continue;

    if (!string.IsNullOrEmpty(config.Url))
    {
        // Register API Source
        builder.Services.AddSingleton<Ghost.Abstractions.IProxySource>(sp =>
            new Ghost.Services.ApiProxySource(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
                config,
                sp.GetRequiredService<ILogger<Ghost.Services.ApiProxySource>>()
            ));
    }
    else if (config.Hosts != null && config.Hosts.Count > 0)
    {
        // Register Static Source
        builder.Services.AddSingleton<Ghost.Abstractions.IProxySource>(sp =>
            new Ghost.Services.StaticProxySource(
                config,
                sp.GetRequiredService<ILogger<Ghost.Services.StaticProxySource>>()
            ));
    }
}

// Configure Ghost
builder.Services.AddGhost(builder.Configuration, gw =>
{
    // Configure Kernel Options
    gw.ConfigureKernel(options =>
    {
        builder.Configuration.GetSection("Ghost:Kernel").Bind(options);
    });

    // Explicitly register LinkedIn extension when referenced directly
    var linkedInSection = builder.Configuration.GetSection("Ghost:Extensions:LinkedIn");
    var isEnabled = linkedInSection.GetValue<bool>("Enabled");
    
    if (linkedInSection.Exists() && isEnabled)
    {
        try
            {
                // Use the directly referenced extension type so its DI registrations run
                gw.UseExtension(new Ghost.Platform.LinkedIn.LinkedInExtension());
            }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Failed to register LinkedIn extension directly: {ex.Message}");
        }
    }

        // Dynamic Extension Loading
        var extensionsSection = builder.Configuration.GetSection("Ghost:Extensions");
        foreach (var section in extensionsSection.GetChildren())
        {
            var platformName = section.Key;
            // Skip LinkedIn here because it's explicitly registered above when enabled
            if (string.Equals(platformName, "LinkedIn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // Check if explicitly enabled
            var enabled = section.GetValue<bool>("Enabled");

        if (enabled)
        {
            try
            {
                var assemblyName = $"Ghost.Platform.{platformName}";
                var typeName = $"{assemblyName}.{platformName}Extension";

                // 1. Try to find the assembly if already loaded
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == assemblyName);

                // 2. If not found, try to load it
                if (assembly == null)
                {
                    try
                    {
                        assembly = Assembly.Load(assemblyName);
                    }
                    catch (FileNotFoundException)
                    {
                        Console.WriteLine($"[Warning] Extension assembly '{assemblyName}' not found.");
                        continue;
                    }
                }

                // 3. Find and instantiate the extension type
                if (assembly != null)
                {
                    var type = assembly.GetType(typeName);
                    if (type != null && Activator.CreateInstance(type) is IExtension extInstance)
                    {
                        gw.UseExtension(extInstance);
                        Console.WriteLine($"[Info] Loaded extension: {platformName}");
                    }
                    else
                    {
                        Console.WriteLine($"[Warning] Could not find extension type '{typeName}' in assembly '{assemblyName}'.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load extension '{platformName}': {ex.Message}");
            }
        }
    }
});
// Ensure IDeduplicationService is registered before AggregatedJobClient which depends on it.
// AddGhostKernel would register this, but to avoid duplicate kernel registrations register
// the deduplication service explicitly here so DI is satisfied regardless of AddGhostKernel.
builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();

// Register aggregator after extensions have been loaded so it can compose available scrapers
// If an AddGhostAggregator extension method exists it would be preferable to call that instead.
// In its absence register the AggregatedJobClient implementation used as the IJobClient.
builder.Services.AddScoped<Ghost.Contracts.Jobs.IJobClient, Ghost.Core.Services.AggregatedJobClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only map LinkedIn endpoints if the extension is enabled
var linkedInEnabled = builder.Configuration.GetSection("Ghost:Extensions:LinkedIn").GetValue<bool>("Enabled");
if (linkedInEnabled)
{
    app.MapLinkedInEndpoints();
}

// Map job endpoints and health checks
app.MapJobsEndpoints();
app.MapHealthChecks("/health");

app.Run();
