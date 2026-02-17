using Ghost.Cloud.Api.Endpoints;
using Ghost.Cloud.Api.Middleware;
using Ghost.Cloud.Grains.Implementation;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Infrastructure.EventStore;
using Ghost.Cloud.Infrastructure.Idempotency;
using Ghost.Cloud.Infrastructure.Persistence;

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

app.Run();
