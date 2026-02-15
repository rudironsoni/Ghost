using Ghost.Platform.Google.Jobs;
using Ghost.Platform.Google.Jobs.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Ghost.Platform.Google.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides a WireMock server for Google Jobs integration tests.
/// Mocks Google Jobs HTML responses to avoid live network calls.
/// </summary>
public sealed class GoogleWireMockFixture : IAsyncLifetime, IDisposable
{
    private WireMockServer? _server;
    private HttpClient? _httpClient;
    private bool _disposed;

    public GoogleWireMockFixture()
    {
    }

    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public WireMockServer Server => _server ?? throw new InvalidOperationException("Fixture not initialized");

    public Task InitializeAsync()
    {
        try
        {
            // Create WireMock server
            _server = WireMockServer.Start();

            // Create HttpClient pointing to WireMock
            // This is the key: we need to intercept Google.com requests
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = false
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_server.Url!)
            };

            // Build service provider with Google Jobs platform pointing to WireMock
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // Configure GoogleJobsOptions with HttpOnly strategy to avoid browser usage
            var googleJobsOptions = new GoogleJobsOptions
            {
                Strategy = JobSearchStrategy.HttpOnly // Force HTTP-only to use mocked endpoint
            };

            services.AddSingleton(googleJobsOptions);
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(googleJobsOptions));
            services.AddSingleton(_httpClient);
            services.AddSingleton<GoogleJobsApiClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<GoogleJobsApiClient>>();
                // Use a custom HttpClient that redirects to WireMock
                var mockClient = CreateMockHttpClient();
                return new GoogleJobsApiClient(mockClient, googleJobsOptions, logger);
            });
            services.AddScoped<GoogleJobClient>();

            ServiceProvider = services.BuildServiceProvider();

            // Setup default mock responses
            SetupMockResponses();

            return Task.CompletedTask;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private HttpClient CreateMockHttpClient()
    {
        // Create a delegating handler that intercepts google.com requests and redirects to WireMock
        var mockHandler = new GoogleMockHandler(_server!.Url!);
        return new HttpClient(mockHandler);
    }

    private void SetupMockResponses()
    {
        if (_server == null) return;

        var widgetHtml = LoadFixtureHtml("google-jobs-widget.html");

        // Mock Google Jobs search endpoint - matches ANY /search request
        // The actual pattern is: /search?q={query}&ibp=htl;jobs&udm=8&gl=us&hl=en
        // But we'll be more permissive to catch all search requests
        _server
            .Given(Request.Create()
                .WithPath("/search")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody(widgetHtml));

        // Log all requests to help debugging
        _server.LogEntriesChanged += (sender, args) =>
        {
            if (args?.NewItems != null)
            {
                foreach (var entry in args.NewItems)
                {
                    if (entry is WireMock.Logging.LogEntry logEntry)
                    {
                        Console.WriteLine($"[WireMock] Request: {logEntry.RequestMessage.Method} {logEntry.RequestMessage.Url}");
                    }
                }
            }
        };
    }

    private static string LoadFixtureHtml(string filename)
    {
        var fixturesPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Html",
            filename);

        if (!File.Exists(fixturesPath))
        {
            throw new FileNotFoundException($"Fixture file not found: {fixturesPath}");
        }

        return File.ReadAllText(fixturesPath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _httpClient?.Dispose();
        _server?.Stop();
        _server?.Dispose();

        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// HTTP message handler that redirects Google.com requests to a WireMock server.
/// This allows us to intercept hardcoded Google URLs in the GoogleJobsApiClient.
/// </summary>
internal sealed class GoogleMockHandler : DelegatingHandler
{
    private readonly string _wireMockUrl;

    public GoogleMockHandler(string wireMockUrl)
        : base(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = false })
    {
        _wireMockUrl = wireMockUrl.TrimEnd('/');
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Intercept requests to google.com and redirect to WireMock
        if (request.RequestUri != null &&
            (request.RequestUri.Host.Contains("google.com", StringComparison.OrdinalIgnoreCase) ||
             request.RequestUri.Host.Contains("www.google", StringComparison.OrdinalIgnoreCase)))
        {
            // Build new URI pointing to WireMock server but preserving path and query
            var builder = new UriBuilder(_wireMockUrl)
            {
                Path = request.RequestUri.AbsolutePath,
                Query = request.RequestUri.Query
            };

            request.RequestUri = builder.Uri;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
