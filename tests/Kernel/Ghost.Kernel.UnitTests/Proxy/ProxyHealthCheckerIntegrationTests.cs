using System;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Proxy;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.Proxy;

/// <summary>
/// Integration tests for <see cref="ProxyHealthChecker"/> covering health checks and latency measurement.
/// </summary>
public class ProxyHealthCheckerIntegrationTests : ReliabilityTestBase
{
    public ProxyHealthCheckerIntegrationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task CheckAllProxiesAsyncReturnsAllStatusesWhenCredentialsMissing()
    {
        string? previousUser = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME");
        string? previousPassword = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME", null);
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD", null);

            using var httpClient = new HttpClient();
            var checker = new ProxyHealthChecker(httpClient, NullLogger<ProxyHealthChecker>.Instance);
            ProxyHealthReport report = await checker.CheckAllProxiesAsync();

            report.Should().NotBeNull();
            report.Proxies.Should().HaveCount(12);
            report.HealthyCount.Should().BeGreaterOrEqualTo(0);
            report.UnhealthyCount.Should().BeGreaterOrEqualTo(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME", previousUser);
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD", previousPassword);
        }
    }

    [Fact]
    public async Task MeasureLatencyAsyncThrowsWhenProxyUrlMissing()
    {
        using var httpClient = new HttpClient();
        var checker = new ProxyHealthChecker(httpClient, NullLogger<ProxyHealthChecker>.Instance);
        await FluentActions.Invoking(() => checker.MeasureLatencyAsync(" "))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task MeasureLatencyAsyncHandlesUnreachableProxyGracefully()
    {
        string proxyHost = "127.0.0.1";
        int port = GetFreePort();

        string proxyUrl = $"socks5://{proxyHost}:{port}";
        string? previousUser = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME");
        string? previousPassword = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD");

        try
        {
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME", "test-user");
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD", "test-pass");

            using var httpClient = new HttpClient();
            var checker = new ProxyHealthChecker(httpClient, NullLogger<ProxyHealthChecker>.Instance);

            long latency = await checker.MeasureLatencyAsync(proxyUrl);

            latency.Should().BeLessThanOrEqualTo(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME", previousUser);
            Environment.SetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD", previousPassword);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return port;
    }


}
