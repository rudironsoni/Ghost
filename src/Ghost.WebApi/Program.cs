using Ghost.Abstractions;
using Ghost.Core;
using Ghost.Hosting;
using Ghost.Hosting.WebApi;
using Ghost.Monitoring;
using Ghost.Resilience;
using Ghost.Utilities;
using Ghost.WebApi.Features.Admin;
using Ghost.WebApi.Features.Health;
using Ghost.WebApi.Features.Jobs;
using Ghost.WebApi.Features.LinkedIn;
using Ghost.WebApi.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
// Removed unused reflection/disk/culture usings after replacing dynamic loader with
// compile-time referenced extensions.

// Load environment variables from .env early in startup (after using directives)
// Requires DotNetEnv package
// Use TraversePath() so .env can be discovered in parent directories (works better in Docker/VS/CLI)
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
try
{
    Console.WriteLine($"[DEBUG] Environment: {builder.Environment.EnvironmentName}");
    Console.WriteLine($"[DEBUG] Ghost:Extensions:Google:Enabled = {builder.Configuration.GetValue<bool?>("Ghost:Extensions:Google:Enabled")}");
    Console.WriteLine($"[DEBUG] Indeed:Country = {builder.Configuration.GetValue<string>("Indeed:Country")}");
}
catch { }

// Force enable platforms for testing
Environment.SetEnvironmentVariable("GHOST__EXTENSIONS__LINKEDIN__ENABLED", "true");
Environment.SetEnvironmentVariable("GHOST__EXTENSIONS__GOOGLE__ENABLED", "true");
Environment.SetEnvironmentVariable("GHOST__EXTENSIONS__GOOGLE__JOBS__ENABLED", "true");
Environment.SetEnvironmentVariable("GHOST__EXTENSIONS__GLASSDOOR__ENABLED", "true");
Environment.SetEnvironmentVariable("GHOST__EXTENSIONS__GLASSDOOR__PROXYENABLED", "false");
Console.WriteLine("[DEBUG] Platforms force-enabled for testing, Glassdoor proxy disabled");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JSON serialization to handle enum strings
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddHealthChecks()
    .AddCheck("ghost-webapi", () => HealthCheckResult.Healthy("Ghost WebAPI is running"));
builder.Services.AddGhostResilience(builder.Configuration);
builder.Services.AddGhostMonitoring(builder.Configuration);
builder.Services.AddRedisQueueMetrics();
builder.Services.AddHttpClient();
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

    if (child.Key.Equals("NordVPN", StringComparison.OrdinalIgnoreCase))
    {
        var nordUser = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME");
        var nordPass = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD");

        if (!string.IsNullOrWhiteSpace(nordUser))
        {
            config.Username = nordUser;
        }

        if (!string.IsNullOrWhiteSpace(nordPass))
        {
            config.Password = nordPass;
        }
    }

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

// Configure Ghost with extensions first
builder.Services.AddGhost(builder.Configuration, gw =>
{
    // Configure Kernel Options
    gw.ConfigureKernel(options =>
    {
        builder.Configuration.GetSection("Ghost:Kernel").Bind(options);
    });

    // Explicit extension registration using compile-time referenced extensions.
    // This avoids dynamic assembly loading and ensures DI registrations from
    // referenced projects run at startup.

    // LinkedIn
    if (builder.Configuration.GetValue("Ghost:Extensions:LinkedIn:Enabled", false))
    {
        gw.UseExtension(new Ghost.Platform.LinkedIn.LinkedInExtension());
    }

    // Indeed
    if (builder.Configuration.GetValue("Ghost:Extensions:Indeed:Enabled", false))
    {
        gw.UseExtension(new Ghost.Platform.Indeed.IndeedExtension());
    }

    // Glassdoor
    if (builder.Configuration.GetValue("Ghost:Extensions:Glassdoor:Enabled", false))
    {
        gw.UseExtension(new Ghost.Platform.Glassdoor.GlassdoorExtension());
    }

    // Google
    if (builder.Configuration.GetValue("Ghost:Extensions:Google:Enabled", false))
    {
        gw.UseExtension(new Ghost.Platform.Google.GoogleExtension());
    }

    // InfoJobs
    if (builder.Configuration.GetValue("Ghost:Extensions:InfoJobs:Enabled", false))
    {
        gw.UseExtension(new Ghost.Platform.InfoJobs.InfoJobsExtension());
    }

});

// Ensure IDeduplicationService is registered (should already be registered by AddGhost)
// Register aggregator after extensions have been loaded so it can compose available scrapers
// Register as Scoped to match the lifetime of IJobScraper implementations
builder.Services.AddScoped<Ghost.Contracts.Jobs.IJobClient, Ghost.Core.Services.AggregatedJobClient>();

var app = builder.Build();

// Add correlation ID middleware (must be early in pipeline)
app.UseCorrelationId();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map LinkedIn endpoints (always enabled for testing)
app.MapLinkedInEndpoints();

// Map job endpoints and health checks
app.MapJobsEndpoints();
app.MapHealthEndpoints();
app.MapDetailedHealth();
app.MapCircuitBreakerHealth();
app.MapDlqEndpoints();
app.MapMetricsEndpoints();
app.MapRedisQueueMetricsEndpoint();
app.MapHealthChecks("/health");

app.Run();
