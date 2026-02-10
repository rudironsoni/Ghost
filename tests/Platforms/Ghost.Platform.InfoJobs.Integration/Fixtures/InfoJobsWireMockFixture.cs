using Ghost.Platform.InfoJobs.Jobs;
using Ghost.Platform.InfoJobs.Jobs.Internal;
using Ghost.Testing.Mocking.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Ghost.Platform.InfoJobs.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides a WireMock server for InfoJobs integration tests.
/// Mocks the InfoJobs API to avoid live network calls and authentication requirements.
/// </summary>
public sealed class InfoJobsWireMockFixture : IAsyncLifetime, IDisposable
{
    private WireMockServer? _server;
    private HttpClient? _httpClient;
    private bool _disposed;

    public InfoJobsWireMockFixture()
    {
    }

    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public WireMockServer Server => _server ?? throw new InvalidOperationException("Fixture not initialized");

    public Task InitializeAsync()
    {
        try
        {
            // Create WireMock server
            _server = WireMockServerFactory.Create();

            // Create HttpClient pointing to WireMock
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_server.Url!)
            };

            // Build service provider with InfoJobs platform pointing to WireMock
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // Configure InfoJobsOptions to point to WireMock server
            var infoJobsOptions = new InfoJobsOptions
            {
                ApiEndpoint = _server.Url!.TrimEnd('/') + "/",
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret"
            };

            services.AddSingleton(infoJobsOptions);
            services.AddSingleton(_httpClient);
            services.AddSingleton<InfoJobsApiClient>();
            services.AddScoped<InfoJobClient>();

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

    private void SetupMockResponses()
    {
        if (_server == null) return;

        // Mock search endpoint - returns sample job listings
        _server
            .Given(Request.Create()
                .WithPath("/1/offer")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockSearchResponse()));

        // Mock job details endpoint - returns sample job details
        _server
            .Given(Request.Create()
                .WithPath("/1/offer/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockJobDetailsResponse()));
    }

    private static string GetMockSearchResponse()
    {
        // Return a sample InfoJobs API response with job listings
        return """
        {
            "currentPage": 1,
            "pageSize": 5,
            "totalResults": 100,
            "currentResults": 5,
            "totalPages": 20,
            "offers": [
                {
                    "id": "test-job-1",
                    "title": "Desarrollador Software Senior",
                    "author": {
                        "name": "Tech Company S.L.",
                        "id": "company-1"
                    },
                    "city": "Madrid",
                    "province": {
                        "value": "Madrid",
                        "id": 28
                    },
                    "link": "https://www.infojobs.net/test-job-1.html",
                    "salaryMin": {
                        "value": "40000"
                    },
                    "salaryMax": {
                        "value": "60000"
                    },
                    "contractType": {
                        "value": "Indefinido"
                    },
                    "requirementMin": "Experiencia con .NET y C#",
                    "updated": "2026-02-10T10:00:00Z"
                },
                {
                    "id": "test-job-2",
                    "title": "Ingeniero de Software",
                    "author": {
                        "name": "Software Corp",
                        "id": "company-2"
                    },
                    "city": "Barcelona",
                    "province": {
                        "value": "Barcelona",
                        "id": 8
                    },
                    "link": "https://www.infojobs.net/test-job-2.html",
                    "salaryMin": {
                        "value": "35000"
                    },
                    "contractType": {
                        "value": "Temporal"
                    },
                    "requirementMin": "Conocimientos de Java y Spring",
                    "updated": "2026-02-09T15:30:00Z"
                },
                {
                    "id": "test-job-3",
                    "title": "Programador Web",
                    "author": {
                        "name": "Digital Solutions",
                        "id": "company-3"
                    },
                    "city": "Valencia",
                    "province": {
                        "value": "Valencia",
                        "id": 46
                    },
                    "link": "https://www.infojobs.net/test-job-3.html",
                    "contractType": {
                        "value": "Indefinido"
                    },
                    "requirementMin": "HTML, CSS, JavaScript",
                    "updated": "2026-02-08T12:00:00Z"
                },
                {
                    "id": "test-job-4",
                    "title": "Analista de Datos",
                    "author": {
                        "name": "Data Analytics Ltd",
                        "id": "company-4"
                    },
                    "city": "Bilbao",
                    "province": {
                        "value": "Vizcaya",
                        "id": 48
                    },
                    "link": "https://www.infojobs.net/test-job-4.html",
                    "salaryMin": {
                        "value": "30000"
                    },
                    "salaryMax": {
                        "value": "45000"
                    },
                    "contractType": {
                        "value": "Indefinido"
                    },
                    "requirementMin": "Python, SQL, Power BI",
                    "updated": "2026-02-07T09:00:00Z"
                },
                {
                    "id": "test-job-5",
                    "title": "Diseñador UX/UI",
                    "author": {
                        "name": "Creative Studio",
                        "id": "company-5"
                    },
                    "city": "Madrid",
                    "province": {
                        "value": "Madrid",
                        "id": 28
                    },
                    "link": "https://www.infojobs.net/test-job-5.html",
                    "salaryMin": {
                        "value": "28000"
                    },
                    "contractType": {
                        "value": "Temporal"
                    },
                    "requirementMin": "Figma, Adobe XD",
                    "updated": "2026-02-10T08:00:00Z"
                }
            ]
        }
        """;
    }

    private static string GetMockJobDetailsResponse()
    {
        // Return a sample InfoJobs API response for job details
        return """
        {
            "id": "test-job-1",
            "title": "Desarrollador Software Senior",
            "author": {
                "name": "Tech Company S.L.",
                "id": "company-1"
            },
            "city": "Madrid",
            "province": {
                "value": "Madrid",
                "id": 28
            },
            "link": "https://www.infojobs.net/test-job-1.html",
            "salaryMin": {
                "value": "40000"
            },
            "salaryMax": {
                "value": "60000"
            },
            "contractType": {
                "value": "Indefinido"
            },
            "description": "Buscamos desarrollador senior con experiencia en .NET y C#. Ambiente dinámico y proyectos innovadores.",
            "requirementMin": "Experiencia con .NET y C#",
            "updated": "2026-02-10T10:00:00Z",
            "experienceMin": {
                "value": "5 años"
            }
        }
        """;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _httpClient?.Dispose();
        WireMockServerFactory.Dispose(_server);

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
