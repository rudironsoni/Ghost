using Ghost.Core;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Indeed.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides an isolated browser context for Indeed integration tests.
/// Each test class gets a fresh browser session with no shared state.
/// Use with [Collection("SharedKernel")] and IClassFixture&lt;IndeedContextFixture&gt;.
/// </summary>
public sealed class IndeedContextFixture : IAsyncLifetime
{
    private readonly SharedGhostKernelFixture _kernelFixture;
    private IBrowserSession? _session;

    public IndeedContextFixture(SharedGhostKernelFixture kernelFixture)
    {
        _kernelFixture = kernelFixture;
    }

    public IBrowserSession Session => _session ?? throw new InvalidOperationException("Fixture not initialized");
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            // Create a fresh session for this test class
            _session = await _kernelFixture.CreateSessionAsync();

            // Build service provider with Indeed platform
            var services = new ServiceCollection();
            services.AddSingleton(_session);
            services.AddSingleton<IBrowserSession>(_session);
            services.AddLogging();

            // Register Indeed services manually
            var indeedOptions = new Ghost.Platform.Indeed.IndeedOptions
            {
                ApiKey = "test-api-key-for-integration-tests" // Required by IndeedConstants.GetHeaders
            };
            var proxyProvider = Ghost.Proxy.StaticProxyProvider.Empty;

            services.AddSingleton(indeedOptions);
            services.AddSingleton<Ghost.Abstractions.IProxyProvider>(proxyProvider);
            services.AddSingleton<Ghost.Platform.Indeed.Internal.IndeedApiClient>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Ghost.Platform.Indeed.Internal.IndeedApiClient>>();
                return new Ghost.Platform.Indeed.Internal.IndeedApiClient(proxyProvider, indeedOptions, logger);
            });
            services.AddScoped<Ghost.Platform.Indeed.IndeedJobClient>();

            ServiceProvider = services.BuildServiceProvider();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_session != null)
        {
            await _session.DisposeAsync();
        }
    }
}
