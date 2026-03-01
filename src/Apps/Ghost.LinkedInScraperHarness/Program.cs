using Ghost.Contracts.Jobs;
using Ghost.LinkedInScraperHarness;
using Ghost.LinkedInScraperHarness.Configuration;
using Ghost.Plugin.LinkedIn;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddConsole();

// Load configuration from appsettings.json, user secrets, and environment variables
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Configure harness options
ScraperHarnessOptions harnessOptions = new();
builder.Configuration.GetSection("ScraperHarness").Bind(harnessOptions);
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(harnessOptions));

// Configure LinkedIn options
builder.Services.Configure<LinkedInOptions>(
    builder.Configuration.GetSection("Ghost:Extensions:LinkedIn"));

// Register LinkedIn plugin services
LinkedInPlugin plugin = new();
plugin.ConfigureServices(builder.Services, builder.Configuration);

// Register the harness runner
builder.Services.AddHostedService<ScraperHarnessRunner>();

IHost host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
