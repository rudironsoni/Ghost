using FastEndpoints;
using FastEndpoints.Swagger;
using Ghost.Hosting;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();

// Configure Ghost
builder.Services.AddGhost(builder.Configuration, gw =>
{
    // Configure Kernel Options
    gw.ConfigureKernel(options =>
    {
        builder.Configuration.GetSection("Ghost:Kernel").Bind(options);
    });

    // Dynamic Extension Loading
    var extensionsSection = builder.Configuration.GetSection("Ghost:Extensions");
    foreach (var section in extensionsSection.GetChildren())
    {
        var platformName = section.Key;
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
app.UseFastEndpoints();
app.UseSwaggerGen(); // Default UI at /swagger

app.Run();
