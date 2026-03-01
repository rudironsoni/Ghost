using System.Net;
using Ghost.Plugin.InfoJobs.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.Net;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace Ghost.Plugin.InfoJobs.End2EndTests.Fixtures;

/// <summary>
/// End-to-End test fixture for InfoJobs plugin using mocked external services.
/// InfoJobs uses API-based integration, not browser-based scraping.
/// </summary>
public sealed class InfoJobsE2EFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public WireMockServer WireMockServer { get; }
    public IConfiguration Configuration { get; }

    public InfoJobsE2EFixture()
    {
        WireMockServer = WireMockServer.Start(new WireMockServerSettings
        {
            Port = 9095,
            UseSSL = false
        });

        Configuration = new ConfigurationBuilder()
            .AddJsonFile("testsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        SetupMockEndpoints();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Configuration
        var options = new InfoJobsOptions
        {
            Enabled = true,
            ApiKey = "test-api-key",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            BaseUrl = $"http://localhost:{WireMockServer.Port}/",
            ApiEndpoint = $"http://localhost:{WireMockServer.Port}/api/",
            RequestTimeoutMs = 30000,
            MaxRetries = 3
        };
        services.AddSingleton(options);
        services.AddSingleton<Microsoft.Extensions.Options.IOptions<InfoJobsOptions>>(
            new Microsoft.Extensions.Options.OptionsWrapper<InfoJobsOptions>(options));

        // Register HTTP client
        services.AddHttpClient<InfoJobsApiClient>(client =>
        {
            client.BaseAddress = new Uri($"http://localhost:{WireMockServer.Port}");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Authorization", "Basic dGVzdC1jbGllbnQtaWQ6dGVzdC1jbGllbnQtc2VjcmV0");
        });

        // Register InfoJobs services
        services.AddSingleton<InfoJobsApiClient>(sp =>
        {
            HttpClient httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(InfoJobsApiClient));
            InfoJobsOptions opts = sp.GetRequiredService<InfoJobsOptions>();
            ILogger<InfoJobsApiClient> logger = sp.GetRequiredService<ILogger<InfoJobsApiClient>>();
            return new InfoJobsApiClient(httpClient, opts, logger);
        });

        services.AddScoped<InfoJobClient>();
    }

    private void SetupMockEndpoints()
    {
        // Mock InfoJobs OAuth token endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/oauth/token")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                    "access_token": "mock-access-token",
                    "token_type": "Bearer",
                    "expires_in": 3600
                }
                """));

        // Mock InfoJobs search API endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/api/1/offer")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockSearchResponse()));

        // Mock InfoJobs job details endpoint
        WireMockServer
            .Given(Request.Create()
                .WithPath("/api/1/offer/*")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockJobDetailsResponse()));
    }

    private static string GetMockSearchResponse()
    {
        return """
        {
            "offers": [
                {
                    "id": "infojobs-job-001",
                    "title": "Desarrollador Software",
                    "description": "Buscamos desarrollador de software con experiencia",
                    "author": {
                        "name": "Tech Solutions Spain",
                        "corporateWebsiteUrl": "https://techsolutions.es"
                    },
                    "city": "Madrid",
                    "province": {
                        "value": "Madrid"
                    },
                    "salaryMin": {
                        "value": "30000"
                    },
                    "salaryMax": {
                        "value": "45000"
                    },
                    "salaryPeriod": {
                        "value": "Bruto/año"
                    },
                    "experienceMin": {
                        "value": "2 años"
                    },
                    "contractType": {
                        "value": "Indefinido"
                    },
                    "journey": {
                        "value": "Jornada completa"
                    },
                    "creationDate": "2024-01-15T10:00:00.000+01:00",
                    "updated": "2024-01-15T10:00:00.000+01:00",
                    "link": "https://www.infojobs.net/job-001"
                },
                {
                    "id": "infojobs-job-002",
                    "title": "Programador .NET",
                    "description": "Desarrollador .NET para proyecto internacional",
                    "author": {
                        "name": "Digital Innovators",
                        "corporateWebsiteUrl": "https://digitalinnovators.es"
                    },
                    "city": "Barcelona",
                    "province": {
                        "value": "Barcelona"
                    },
                    "salaryMin": {
                        "value": "35000"
                    },
                    "salaryMax": {
                        "value": "50000"
                    },
                    "salaryPeriod": {
                        "value": "Bruto/año"
                    },
                    "experienceMin": {
                        "value": "3 años"
                    },
                    "contractType": {
                        "value": "Indefinido"
                    },
                    "journey": {
                        "value": "Jornada completa"
                    },
                    "creationDate": "2024-01-14T08:00:00.000+01:00",
                    "updated": "2024-01-14T08:00:00.000+01:00",
                    "link": "https://www.infojobs.net/job-002"
                }
            ],
            "totalResults": 2,
            "page": 1,
            "totalPages": 1
        }
        """;
    }

    private static string GetMockJobDetailsResponse()
    {
        return """
        {
            "id": "infojobs-job-001",
            "title": "Desarrollador Software",
            "description": "Buscamos desarrollador de software con experiencia en C# y .NET.\n\nRequisitos:\n- 2+ años de experiencia\n- Conocimiento de .NET Core\n- SQL Server\n\nOfrecemos:\n- Contrato indefinido\n- Salario competitivo\n- Formación continua",
            "author": {
                "name": "Tech Solutions Spain",
                "description": "Empresa líder en soluciones tecnológicas",
                "corporateWebsiteUrl": "https://techsolutions.es"
            },
            "city": "Madrid",
            "address": "Calle Principal 123",
            "zipCode": "28001",
            "province": {
                "value": "Madrid"
            },
            "salaryMin": {
                "value": "30000"
            },
            "salaryMax": {
                "value": "45000"
            },
            "salaryPeriod": {
                "value": "Bruto/año"
            },
            "experienceMin": {
                "value": "2 años"
            },
            "experienceMax": {
                "value": "5 años"
            },
            "contractType": {
                "value": "Indefinido"
            },
            "journey": {
                "value": "Jornada completa"
            },
            "category": {
                "value": "Informática y telecomunicaciones"
            },
            "subcategory": {
                "value": "Programación"
            },
            "creationDate": "2024-01-15T10:00:00.000+01:00",
            "updated": "2024-01-15T10:00:00.000+01:00",
            "applications": 25,
            "link": "https://www.infojobs.net/job-001",
            "requirementMin": "2 años de experiencia en desarrollo .NET"
        }
        """;
    }

    public void Dispose()
    {
        WireMockServer?.Stop();
        WireMockServer?.Dispose();

        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Collection attribute for InfoJobs E2E tests.
/// </summary>
[CollectionDefinition("InfoJobsEnd2End")]
public class InfoJobsE2EFixtures : ICollectionFixture<InfoJobsE2EFixture>
{
}
