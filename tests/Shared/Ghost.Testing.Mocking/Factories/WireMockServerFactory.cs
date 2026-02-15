using WireMock.Server;

namespace Ghost.Testing.Mocking.Factories;

/// <summary>
/// Factory for creating isolated WireMock server instances for tests.
/// </summary>
public static class WireMockServerFactory
{
    /// <summary>
    /// Creates a new WireMock server instance on a random available port.
    /// </summary>
    /// <param name="startImmediately">Whether to start the server immediately. Default is true.</param>
    /// <returns>A new WireMock server instance.</returns>
    public static WireMockServer Create(bool startImmediately = true)
    {
        if (startImmediately)
        {
            return WireMockServer.Start();
        }

        return WireMockServer.StartWithAdminInterface();
    }

    /// <summary>
    /// Creates a new WireMock server instance on a specific port.
    /// </summary>
    /// <param name="port">The port to bind the server to.</param>
    /// <returns>A new WireMock server instance.</returns>
    public static WireMockServer CreateOnPort(int port)
    {
        return WireMockServer.Start(port);
    }

    /// <summary>
    /// Creates a new WireMock server with HTTPS enabled.
    /// </summary>
    /// <param name="httpPort">The HTTP port. If null, uses a random port.</param>
    /// <param name="httpsPort">The HTTPS port. If null, uses a random port.</param>
    /// <returns>A new WireMock server instance with HTTPS enabled.</returns>
    public static WireMockServer CreateWithHttps(int? httpPort = null, int? httpsPort = null)
    {
        var settings = new WireMock.Settings.WireMockServerSettings
        {
            Port = httpPort,
            UseSSL = true,
            // WireMock.NET generates a self-signed certificate automatically
        };

        return WireMockServer.Start(settings);
    }

    /// <summary>
    /// Creates multiple isolated WireMock server instances.
    /// </summary>
    /// <param name="count">The number of server instances to create.</param>
    /// <returns>An array of WireMock server instances.</returns>
    public static WireMockServer[] CreateMany(int count)
    {
        var servers = new WireMockServer[count];
        for (int i = 0; i < count; i++)
        {
            servers[i] = Create();
        }
        return servers;
    }

    /// <summary>
    /// Creates a WireMock server with request logging enabled for debugging.
    /// </summary>
    /// <returns>A new WireMock server instance with logging enabled.</returns>
    public static WireMockServer CreateWithLogging()
    {
        var settings = new WireMock.Settings.WireMockServerSettings
        {
            Logger = new WireMock.Logging.WireMockConsoleLogger()
        };

        return WireMockServer.Start(settings);
    }

    /// <summary>
    /// Creates a WireMock server optimized for parallel test execution.
    /// Ensures port allocation doesn't conflict with other test instances.
    /// </summary>
    /// <returns>A new WireMock server instance safe for parallel execution.</returns>
    public static WireMockServer CreateForParallelExecution()
    {
        // WireMock.NET automatically allocates random available ports
        // This is inherently parallel-safe
        return WireMockServer.Start();
    }

    /// <summary>
    /// Safely disposes a WireMock server instance.
    /// </summary>
    /// <param name="server">The server to dispose.</param>
    public static void Dispose(WireMockServer? server)
    {
        if (server == null) return;

        try
        {
            server.Stop();
            server.Dispose();
        }
        catch
        {
            // Swallow disposal errors to avoid test failures
        }
    }

    /// <summary>
    /// Safely disposes multiple WireMock server instances.
    /// </summary>
    /// <param name="servers">The servers to dispose.</param>
    public static void DisposeMany(params WireMockServer?[] servers)
    {
        foreach (WireMockServer? server in servers)
        {
            Dispose(server);
        }
    }
}
