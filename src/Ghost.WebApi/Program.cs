using Ghost.Hosting;
using Ghost.WebApi.Features.LinkedIn;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Register HTTP client and proxy provider required by Ghost
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Ghost.Abstractions.IProxyProvider, Ghost.Services.RotatingProxyProvider>();

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

app.Run();
