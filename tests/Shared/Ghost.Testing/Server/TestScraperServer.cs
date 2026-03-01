using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Ghost.Testing.Server.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ghost.Testing.Server;

/// <summary>
/// Logger messages for TestScraperServer.
/// </summary>
public static partial class TestScraperServerLogMessages
{
    [LoggerMessage(Level = LogLevel.Information, Message = "TestScraperServer started at {BaseUrl}")]
    public static partial void ServerStarted(this ILogger logger, string baseUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "TestScraperServer disposed")]
    public static partial void ServerDisposed(this ILogger logger);
}

/// <summary>
/// Kestrel-based test server for serving realistic HTML fixtures for E2E plugin testing.
/// Mimics the structure of job sites like LinkedIn, Indeed, Glassdoor, Google, and InfoJobs.
/// </summary>
public sealed class TestScraperServer : IAsyncDisposable
{
    private static readonly object _portLock = new();
    private readonly IHost _host;
    private readonly ILogger<TestScraperServer> _logger;
    private bool _disposed;

    /// <summary>
    /// Gets the base URL of the server (e.g., "http://localhost:12345").
    /// </summary>
    public string BaseUrl { get; }

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Gets the HTML fixtures used by this server.
    /// </summary>
    public TestFixtures Fixtures { get; }

    private TestScraperServer(IHost host, string baseUrl, int port, ILogger<TestScraperServer> logger)
    {
        _host = host;
        BaseUrl = baseUrl;
        Port = port;
        _logger = logger;
        Fixtures = new TestFixtures(baseUrl);
    }

    /// <summary>
    /// Creates and starts a new test scraper server on a dynamic port.
    /// </summary>
    /// <param name="port">Optional specific port. If null, an available port is chosen automatically.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A running TestScraperServer instance.</returns>
    public static async Task<TestScraperServer> CreateAsync(int? port = null, CancellationToken cancellationToken = default)
    {
        int selectedPort = port ?? GetAvailablePort();
        string baseUrl = $"http://localhost:{selectedPort}";

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>(),
            EnvironmentName = Environments.Development
        });

        // Configure minimal logging
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        // Configure Kestrel
        builder.WebHost.UseUrls(baseUrl);
        builder.WebHost.UseKestrel(options =>
        {
            options.ListenLocalhost(selectedPort);
        });

        WebApplication app = builder.Build();

        // Create fixtures instance for route handlers
        TestFixtures fixtures = new(baseUrl);

        // Configure routes
        ConfigureRoutes(app, fixtures);

        // Start the server
        await app.StartAsync(cancellationToken);

    ILogger<TestScraperServer> logger = app.Services.GetRequiredService<ILogger<TestScraperServer>>();
    logger.ServerStarted(baseUrl);

        return new TestScraperServer(app, baseUrl, selectedPort, logger);
    }

    /// <summary>
    /// Configures all routes for the test server.
    /// </summary>
    private static void ConfigureRoutes(WebApplication app, TestFixtures fixtures)
    {
        // LinkedIn routes
        app.MapGet("/linkedin/jobs", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["keywords"].FirstOrDefault();
            string? location = context.Request.Query["location"].FirstOrDefault();
            int page = int.TryParse(context.Request.Query["page"].FirstOrDefault(), out int p) ? p : 1;
            int count = int.TryParse(context.Request.Query["count"].FirstOrDefault(), out int c) ? c : 10;

            string html = fixtures.LinkedIn.GenerateSearchResultsPage(searchTerm, location, page, count);
            return Results.Content(html, "text/html");
        });

    app.MapGet("/linkedin/jobs/{id}", (string id, HttpContext context) =>
    {
        string html = LinkedInHtmlFixture.GenerateJobDetailPage(id);
        return Results.Content(html, "text/html");
    });

        // Additional LinkedIn routes for client compatibility
        app.MapGet("/linkedin/jobs/search", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["keywords"].FirstOrDefault();
            string? location = context.Request.Query["location"].FirstOrDefault();
            int page = int.TryParse(context.Request.Query["page"].FirstOrDefault(), out int p) ? p : 1;
            int count = int.TryParse(context.Request.Query["count"].FirstOrDefault(), out int c) ? c : 10;

            string html = fixtures.LinkedIn.GenerateSearchResultsPage(searchTerm, location, page, count);
            return Results.Content(html, "text/html");
        });

    app.MapGet("/linkedin/jobs/view/{id}", (string id, HttpContext context) =>
    {
        string html = LinkedInHtmlFixture.GenerateJobDetailPage(id);
        return Results.Content(html, "text/html");
    });

        // LinkedIn Guest API routes (used by LinkedInQueryBuilder and GuestJobSearch)
        app.MapGet("/linkedin/jobs-guest/jobs/api/seeMoreJobPostings/search", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["keywords"].FirstOrDefault();
            string? location = context.Request.Query["location"].FirstOrDefault();
            int start = int.TryParse(context.Request.Query["start"].FirstOrDefault(), out int s) ? s : 0;
            int page = (start / 10) + 1;
            int count = 10;

            string html = fixtures.LinkedIn.GenerateSearchResultsPage(searchTerm, location, page, count);
            return Results.Content(html, "text/html");
        });

    app.MapGet("/linkedin/jobs-guest/jobs/api/jobPosting/{id}", (string id, HttpContext context) =>
    {
        string html = LinkedInHtmlFixture.GenerateJobDetailPage(id);
        return Results.Content(html, "text/html");
    });

        // Indeed routes
        app.MapGet("/indeed/jobs", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["q"].FirstOrDefault();
            string? location = context.Request.Query["l"].FirstOrDefault();
            int page = int.TryParse(context.Request.Query["start"].FirstOrDefault(), out int s) ? (s / 10) + 1 : 1;
            int count = int.TryParse(context.Request.Query["count"].FirstOrDefault(), out int c) ? c : 10;

            string html = fixtures.Indeed.GenerateSearchResultsPage(searchTerm, location, page, count);
            return Results.Content(html, "text/html");
        });

    app.MapGet("/indeed/viewjob", (HttpContext context) =>
    {
        string? jobId = context.Request.Query["jk"].FirstOrDefault() ?? "default-job";
        string html = IndeedHtmlFixture.GenerateJobDetailPage(jobId);
        return Results.Content(html, "text/html");
    });

        // Indeed GraphQL API endpoint for job search
        app.MapPost("/indeed/graphql", (HttpContext context) =>
        {
            // Return mock GraphQL response for Indeed job search
            string jsonResponse = """
            {
                "data": {
                    "jobSearch": {
                        "results": [
                            {
                                "job": {
                                    "key": "indeed-job-001",
                                    "title": "Software Engineer",
                                    "employer": {
                                        "name": "Tech Solutions Inc"
                                    },
                                    "location": {
                                        "formatted": {
                                            "long": "San Francisco, CA"
                                        }
                                    },
                                    "description": {
                                        "html": "<p>We are looking for a skilled software engineer to join our team.</p>"
                                    }
                                }
                            },
                            {
                                "job": {
                                    "key": "indeed-job-002",
                                    "title": "Senior Developer",
                                    "employer": {
                                        "name": "Digital Corp"
                                    },
                                    "location": {
                                        "formatted": {
                                            "long": "Remote"
                                        }
                                    },
                                    "description": {
                                        "html": "<p>Join our remote team as a senior developer.</p>"
                                    }
                                }
                            },
                            {
                                "job": {
                                    "key": "indeed-job-003",
                                    "title": "Full Stack Engineer",
                                    "employer": {
                                        "name": "StartupXYZ"
                                    },
                                    "location": {
                                        "formatted": {
                                            "long": "New York, NY"
                                        }
                                    },
                                    "description": {
                                        "html": "<p>Looking for a full stack engineer with React and .NET experience.</p>"
                                    }
                                }
                            }
                        ],
                        "pageInfo": {
                            "hasNextPage": true,
                            "nextCursor": "mock-cursor-123"
                        }
                    }
                }
            }
            """;
            return Results.Content(jsonResponse, "application/json");
        });

        // Glassdoor routes
        app.MapGet("/glassdoor/jobs", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["suggestCount"].FirstOrDefault()
                ?? context.Request.Query["keyword"].FirstOrDefault();
            string? location = context.Request.Query["locId"].FirstOrDefault();
            int page = int.TryParse(context.Request.Query["p"].FirstOrDefault(), out int p) ? p : 1;

            string html = fixtures.Glassdoor.GenerateSearchResultsPage(searchTerm, location, page);
            return Results.Content(html, "text/html");
        });

        // Glassdoor job search route matching real Glassdoor URL pattern (/Job/jobs.htm)
        app.MapGet("/glassdoor/Job/jobs.htm", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["sc.keyword"].FirstOrDefault();
            string? location = context.Request.Query["locKeyword"].FirstOrDefault();
            int page = int.TryParse(context.Request.Query["p"].FirstOrDefault(), out int p) ? p : 1;

            string html = fixtures.Glassdoor.GenerateSearchResultsPage(searchTerm, location, page);
            return Results.Content(html, "text/html");
        });

    app.MapGet("/glassdoor/job/{id}", (string id) =>
    {
        string html = GlassdoorHtmlFixture.GenerateJobDetailPage(id);
        return Results.Content(html, "text/html");
    });

        // Google routes
        app.MapGet("/google/jobs", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["q"].FirstOrDefault();
            int page = int.TryParse(context.Request.Query["page"].FirstOrDefault(), out int p) ? p : 1;

            string html = fixtures.Google.GenerateSearchResultsPage(searchTerm, page);
            return Results.Content(html, "text/html");
        });

        // InfoJobs routes
        app.MapGet("/infojobs/ofertas", (HttpContext context) =>
        {
            string? searchTerm = context.Request.Query["palabra"].FirstOrDefault();
            string? location = context.Request.Query["provincia"].FirstOrDefault();
            int page = int.TryParse(context.Request.Query["pagina"].FirstOrDefault(), out int p) ? p : 1;

            string html = fixtures.InfoJobs.GenerateSearchResultsPage(searchTerm, location, page);
            return Results.Content(html, "text/html");
        });

    app.MapGet("/infojobs/oferta/{id}", (string id) =>
    {
        string html = InfoJobsHtmlFixture.GenerateJobDetailPage(id);
        return Results.Content(html, "text/html");
    });

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

        // Root endpoint with documentation
        app.MapGet("/", () =>
        {
            StringBuilder sb = new();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><title>TestScraperServer</title></head><body>");
            sb.AppendLine("<h1>TestScraperServer</h1>");
            sb.AppendLine("<p>Available endpoints:</p>");
            sb.AppendLine("<ul>");
            sb.AppendLine("<li><b>LinkedIn:</b> /linkedin/jobs?keywords=&amp;location=&amp;page=&amp;count=</li>");
            sb.AppendLine("<li><b>LinkedIn Job:</b> /linkedin/jobs/{id}</li>");
            sb.AppendLine("<li><b>LinkedIn Guest API:</b> /linkedin/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords=&amp;location=&amp;start=</li>");
            sb.AppendLine("<li><b>LinkedIn Guest Job:</b> /linkedin/jobs-guest/jobs/api/jobPosting/{id}</li>");
            sb.AppendLine("<li><b>Indeed:</b> /indeed/jobs?q=&amp;l=&amp;start=&amp;count=</li>");
            sb.AppendLine("<li><b>Indeed Job:</b> /indeed/viewjob?jk={id}</li>");
            sb.AppendLine("<li><b>Glassdoor:</b> /glassdoor/jobs?keyword=&amp;p=</li>");
            sb.AppendLine("<li><b>Glassdoor (Real Pattern):</b> /glassdoor/Job/jobs.htm?sc.keyword=&amp;locT=C&amp;locKeyword=&amp;srs=</li>");
            sb.AppendLine("<li><b>Glassdoor Job:</b> /glassdoor/job/{id}</li>");
            sb.AppendLine("<li><b>Google:</b> /google/jobs?q=&amp;page=</li>");
            sb.AppendLine("<li><b>InfoJobs:</b> /infojobs/ofertas?palabra=&amp;provincia=&amp;pagina=</li>");
            sb.AppendLine("<li><b>InfoJobs Job:</b> /infojobs/oferta/{id}</li>");
            sb.AppendLine("<li><b>Health:</b> /health</li>");
            sb.AppendLine("</ul>");
            sb.AppendLine("</body></html>");
            return Results.Content(sb.ToString(), "text/html");
        });
    }

    /// <summary>
    /// Gets an available TCP port. Uses a lock to prevent race conditions.
    /// </summary>
    private static int GetAvailablePort()
    {
        lock (_portLock)
        {
            using TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    /// <summary>
    /// Gets the LinkedIn base URL for configuring plugins.
    /// </summary>
    public string GetLinkedInBaseUrl() => $"{BaseUrl}/linkedin";

    /// <summary>
    /// Gets the Indeed base URL for configuring plugins.
    /// </summary>
    public string GetIndeedBaseUrl() => $"{BaseUrl}/indeed";

    /// <summary>
    /// Gets the Glassdoor base URL for configuring plugins.
    /// </summary>
    public string GetGlassdoorBaseUrl() => $"{BaseUrl}/glassdoor";

    /// <summary>
    /// Gets the Google base URL for configuring plugins.
    /// </summary>
    public string GetGoogleBaseUrl() => $"{BaseUrl}/google";

    /// <summary>
    /// Gets the InfoJobs base URL for configuring plugins.
    /// </summary>
    public string GetInfoJobsBaseUrl() => $"{BaseUrl}/infojobs";

    /// <summary>
    /// Stops the server asynchronously.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _host.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

    _disposed = true;
    await _host.StopAsync();
    _host.Dispose();
    _logger.ServerDisposed();
    }
}

/// <summary>
/// Container for all HTML fixtures used by TestScraperServer.
/// </summary>
public sealed class TestFixtures
{
    /// <summary>
    /// Gets the LinkedIn HTML fixture.
    /// </summary>
    public LinkedInHtmlFixture LinkedIn { get; }

    /// <summary>
    /// Gets the Indeed HTML fixture.
    /// </summary>
    public IndeedHtmlFixture Indeed { get; }

    /// <summary>
    /// Gets the Glassdoor HTML fixture.
    /// </summary>
    public GlassdoorHtmlFixture Glassdoor { get; }

    /// <summary>
    /// Gets the Google HTML fixture.
    /// </summary>
    public GoogleHtmlFixture Google { get; }

    /// <summary>
    /// Gets the InfoJobs HTML fixture.
    /// </summary>
    public InfoJobsHtmlFixture InfoJobs { get; }

    /// <summary>
    /// Creates a new TestFixtures instance.
    /// </summary>
    /// <param name="baseUrl">The base URL for generating links.</param>
    public TestFixtures(string baseUrl)
    {
        LinkedIn = new LinkedInHtmlFixture(baseUrl);
        Indeed = new IndeedHtmlFixture(baseUrl);
        Glassdoor = new GlassdoorHtmlFixture(baseUrl);
        Google = new GoogleHtmlFixture(baseUrl);
        InfoJobs = new InfoJobsHtmlFixture(baseUrl);
    }
}
