using Ghost.Cloud.Api.Canaries;
using Ghost.Cloud.Api.Endpoints;
using Ghost.Cloud.Api.Middleware;
using Ghost.Cloud.Api.Observability;
using Ghost.Cloud.Grains.Implementation;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Infrastructure.EventStore;
using Ghost.Cloud.Infrastructure.Idempotency;
using Ghost.Cloud.Infrastructure.Persistence;
using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Downloader;
using Ghost.Engine.Engine;
using Microsoft.Extensions.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add endpoint validation
builder.Services.AddEndpointValidation();

// Add infrastructure services
string connectionString = builder.Configuration.GetConnectionString("PostgreSql")
    ?? "Host=localhost;Database=ghost_cloud;Username=postgres;Password=postgres";

builder.Services.AddSingleton<IEventStore>(new PostgreSqlEventStore(connectionString));
builder.Services.AddSingleton<IIdempotencyService>(new PostgreSqlIdempotencyService(connectionString));
builder.Services.AddSingleton<IScrapeRunQueries>(new PostgreSqlReadStore(connectionString));
builder.Services.AddSingleton<IArtifactQueries>(new PostgreSqlReadStore(connectionString));
builder.Services.AddSingleton<IEndpointQueries>(new PostgreSqlReadStore(connectionString));
builder.Services.AddSingleton<ICanaryQueries>(new PostgreSqlReadStore(connectionString));

// Add Ghost Engine services for canary execution
builder.Services.AddSingleton<IDownloader>(sp =>
{
    // Use FakeDownloader for development/testing
    // In production, this would be a real HTTP downloader
    ILogger<FakeDownloader> logger = sp.GetRequiredService<ILogger<FakeDownloader>>();
    return new FakeDownloader(request => new Ghost.Engine.Abstractions.Transport.GhostResponse(
        Url: request.Url,
        StatusCode: 200,
        Headers: new Dictionary<string, string>
        {
            ["Content-Type"] = "text/html",
            ["X-Ghost-Canary"] = "true"
        },
        Content: $"<html><body><h1>Canary Health Check</h1><p>Endpoint validated at {DateTimeOffset.UtcNow:O}</p></body></html>",
        ReceivedAtUtc: DateTimeOffset.UtcNow));
});

builder.Services.AddSingleton<IGhostEngine>(sp =>
{
    IDownloader downloader = sp.GetRequiredService<IDownloader>();
    return new GhostEngine(
        options: new GhostEngineOptions
        {
            MaxInFlight = 5,
            MaxPendingItems = 100
        },
        downloader: downloader);
});

builder.Services.AddSingleton<IAssuranceCanaryRunner, AssuranceCanaryRunner>();
builder.Services.AddHostedService<ScheduledCanaryDispatcher>();
builder.Services.AddCloudObservability(builder.Configuration);
builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId |
        ActivityTrackingOptions.SpanId |
        ActivityTrackingOptions.ParentId |
        ActivityTrackingOptions.Baggage |
        ActivityTrackingOptions.Tags;
});

// Add Orleans
builder.Host.UseOrleans((context, siloBuilder) =>
{
    siloBuilder
        .UseLocalhostClustering()
        .AddMemoryGrainStorage("Default")
        .AddMemoryStreams("Memory")
        .AddMemoryGrainStorageAsDefault()
        .ConfigureLogging(logging => logging.AddConsole());
});

builder.Services.AddSingleton<IClusterClient>(sp =>
    sp.GetRequiredService<IGrainFactory>().GetType()
        .GetProperty("ClusterClient")?.GetValue(sp.GetRequiredService<IGrainFactory>()) as IClusterClient
        ?? throw new InvalidOperationException("ClusterClient not available"));

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add custom middleware
app.UseTenantResolution();
app.UseIdempotency();
app.UseSchemaValidation();

app.MapScrapeEndpoints();
app.MapRunEndpoints();
app.MapSchedulerEndpoints();
app.MapCanaryEndpoints();

app.Run();
