using Ghost.Testing.Scenarios.Server.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Scenarios.Server;

/// <summary>
/// Kestrel-based synthetic scenario server for browser testing.
/// Provides deterministic web scenarios without external dependencies.
/// </summary>
public sealed class ScenarioServer : IDisposable
{
    private readonly IHost _host;
    private bool _disposed;

    public string BaseUrl { get; }
    public int Port { get; }

    private ScenarioServer(IHost host, string baseUrl, int port)
    {
        _host = host;
        BaseUrl = baseUrl;
        Port = port;
    }

    /// <summary>
    /// Creates and starts a new scenario server on a dynamic port.
    /// </summary>
    public static async Task<ScenarioServer> CreateAsync(int? port = null, CancellationToken cancellationToken = default)
    {
        var selectedPort = port ?? GetAvailablePort();
        var baseUrl = $"http://localhost:{selectedPort}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            EnvironmentName = Environments.Development
        });

        // Configure services
        builder.Services.AddSingleton<ScenarioRegistry>();
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        // Configure Kestrel
        builder.WebHost.UseUrls(baseUrl);
        builder.WebHost.UseKestrel(options =>
        {
            options.ListenLocalhost(selectedPort);
        });

        var app = builder.Build();

        // Add middleware
        app.UseMiddleware<ConsentMiddleware>();
        app.UseMiddleware<InfiniteScrollMiddleware>();
        app.UseMiddleware<PaginationMiddleware>();

        // Configure routing
        var registry = app.Services.GetRequiredService<ScenarioRegistry>();
        registry.RegisterRoutes(app);

        // Start the server
        await app.StartAsync(cancellationToken);

        var logger = app.Services.GetRequiredService<ILogger<ScenarioServer>>();
        logger.LogInformation("Scenario server started at {BaseUrl}", baseUrl);

        return new ScenarioServer(app, baseUrl, selectedPort);
    }

    /// <summary>
    /// Gets an available TCP port.
    /// </summary>
    private static int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _host.StopAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _host.Dispose();
    }
}
