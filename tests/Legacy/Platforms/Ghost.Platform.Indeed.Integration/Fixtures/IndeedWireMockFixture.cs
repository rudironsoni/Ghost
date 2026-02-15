using Ghost.Platform.Indeed;
using Ghost.Platform.Indeed.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Ghost.Platform.Indeed.Integration.Fixtures;

/// <summary>
/// Per-test-class fixture that provides a WireMock server for Indeed integration tests.
/// Mocks the Indeed GraphQL API to avoid live network calls and authentication requirements.
/// </summary>
public sealed class IndeedWireMockFixture : IAsyncLifetime, IDisposable
{
    private WireMockServer? _server;
    private bool _disposed;

    public IndeedWireMockFixture()
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

            // Build service provider with Indeed platform pointing to WireMock
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // Configure IndeedOptions to point to WireMock server
            var indeedOptions = new IndeedOptions
            {
                ApiEndpoint = _server.Url!.TrimEnd('/') + "/graphql",
                ApiKey = "test-api-key"
            };

            services.AddSingleton(indeedOptions);

            // Create IndeedApiClient manually with internal constructor
            services.AddSingleton<IndeedApiClient>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<IndeedApiClient>>();

                // Use internal constructor with null proxyProvider and sessionOrchestrator
                var constructor = typeof(IndeedApiClient).GetConstructor(
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                    null,
                    new[] {
                        typeof(Ghost.Abstractions.IProxyProvider),
                        typeof(Ghost.Platform.Common.Session.ISessionOrchestrator),
                        typeof(IndeedOptions),
                        typeof(ILogger<IndeedApiClient>),
                        typeof(System.Net.Http.HttpMessageHandler),
                        typeof(TimeProvider)
                    },
                    null) ?? throw new InvalidOperationException("Could not find IndeedApiClient internal constructor");

                return (IndeedApiClient)constructor.Invoke(new object?[] {
                    null, // proxyProvider - null to disable proxy
                    null, // sessionOrchestrator
                    indeedOptions,
                    logger,
                    null, // handler - null to use default
                    TimeProvider.System
                });
            });

            services.AddScoped<IndeedJobClient>();

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

        // Mock GraphQL endpoint - matches Indeed's API structure
        _server
            .Given(Request.Create()
                .WithPath("/graphql")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(GetMockSearchResponse()));
    }

    private static string GetMockSearchResponse()
    {
        // Return a sample Indeed GraphQL API response with job listings
        // Structure matches IndeedJobParser expectations
        return """
        {
            "data": {
                "jobSearch": {
                    "pageInfo": {
                        "nextCursor": null,
                        "hasNextPage": false
                    },
                    "results": [
                        {
                            "job": {
                                "key": "test-job-1",
                                "title": "Software Engineer",
                                "employer": {
                                    "name": "Tech Corporation"
                                },
                                "location": {
                                    "formatted": {
                                        "long": "Austin, TX"
                                    }
                                },
                                "description": {
                                    "html": "<p>We are looking for a talented Software Engineer to join our team. Experience with C# and .NET required.</p>"
                                },
                                "compensation": {
                                    "baseSalary": {
                                        "range": {
                                            "min": 80000,
                                            "max": 120000,
                                            "currency": "USD"
                                        }
                                    }
                                },
                                "datePublished": "2026-02-10T10:00:00Z"
                            }
                        },
                        {
                            "job": {
                                "key": "test-job-2",
                                "title": "Senior Developer",
                                "employer": {
                                    "name": "Software Solutions Inc"
                                },
                                "location": {
                                    "formatted": {
                                        "long": "Boston, MA"
                                    }
                                },
                                "description": {
                                    "html": "<p>Senior developer position with excellent benefits. 5+ years experience required.</p>"
                                },
                                "compensation": {
                                    "baseSalary": {
                                        "range": {
                                            "min": 100000,
                                            "max": 150000,
                                            "currency": "USD"
                                        }
                                    }
                                },
                                "datePublished": "2026-02-09T15:30:00Z"
                            }
                        },
                        {
                            "job": {
                                "key": "test-job-3",
                                "title": "Data Analyst",
                                "employer": {
                                    "name": "Analytics Corp"
                                },
                                "location": {
                                    "formatted": {
                                        "long": "Chicago, IL"
                                    }
                                },
                                "description": {
                                    "html": "<p>Data analyst role with focus on business intelligence. SQL and Python skills needed.</p>"
                                },
                                "compensation": {
                                    "baseSalary": {
                                        "range": {
                                            "min": 70000,
                                            "max": 95000,
                                            "currency": "USD"
                                        }
                                    }
                                },
                                "datePublished": "2026-02-08T12:00:00Z"
                            }
                        },
                        {
                            "job": {
                                "key": "test-job-4",
                                "title": "DevOps Engineer",
                                "employer": {
                                    "name": "Cloud Services Ltd"
                                },
                                "location": {
                                    "formatted": {
                                        "long": "Denver, CO"
                                    }
                                },
                                "description": {
                                    "html": "<p>DevOps engineer needed for cloud infrastructure management. AWS and Kubernetes experience preferred.</p>"
                                },
                                "compensation": {
                                    "baseSalary": {
                                        "range": {
                                            "min": 90000,
                                            "max": 130000,
                                            "currency": "USD"
                                        }
                                    }
                                },
                                "datePublished": "2026-02-07T09:00:00Z"
                            }
                        },
                        {
                            "job": {
                                "key": "test-job-5",
                                "title": "Remote Software Developer",
                                "employer": {
                                    "name": "Digital Nomad Co"
                                },
                                "location": {
                                    "formatted": {
                                        "long": "Remote"
                                    }
                                },
                                "description": {
                                    "html": "<p>Fully remote position for experienced software developer. Work from anywhere!</p>"
                                },
                                "compensation": {
                                    "baseSalary": {
                                        "range": {
                                            "min": 85000,
                                            "max": 125000,
                                            "currency": "USD"
                                        }
                                    }
                                },
                                "datePublished": "2026-02-10T08:00:00Z"
                            }
                        }
                    ]
                }
            }
        }
        """;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

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
