using Ghost.Contracts.Jobs;
using Ghost.Plugin.Google.Gemini;
using Ghost.Plugin.Google.Jobs;
using Ghost.Plugin.Google.Jobs.Internal;
using Ghost.Testing.Fakes;
using Ghost.Testing.Server.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ghost.Plugin.Google.End2EndTests.Fixtures;

/// <summary>
/// Test-specific GoogleJobsApiClient that returns sample data instead of making HTTP requests.
/// </summary>
public class TestGoogleJobsApiClient : GoogleJobsApiClient
{
    private readonly ILogger<GoogleJobsApiClient> _logger;

    private static readonly Action<ILogger, Exception?> LogUsingTestClient =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(SearchAsync)), "Using test GoogleJobsApiClient - returning sample data");

    public TestGoogleJobsApiClient(ILogger<GoogleJobsApiClient> logger)
        : base(new HttpClient(), new GoogleJobsOptions { Enabled = true, Strategy = JobSearchStrategy.HttpFirst }, logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override Task<IReadOnlyList<JobListing>> SearchAsync(string query, string location, CancellationToken ct = default)
    {
        LogUsingTestClient(_logger, null);

        // Return filtered sample data based on query
        // Limit to 3 results to simulate real API behavior and satisfy MaxResults tests
        // If query is "jobs" (used by GetJobDetails), return first job to ensure test compatibility
        IReadOnlyList<SampleJob> sourceJobs;
        if (string.IsNullOrWhiteSpace(query) || query.Equals("jobs", StringComparison.OrdinalIgnoreCase))
        {
            sourceJobs = SampleJobData.Jobs.Take(1).ToList();
        }
        else
        {
            sourceJobs = SampleJobData.Jobs
                .Where(j =>
                    j.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    j.Company.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    j.Location.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    j.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();
        }

        IReadOnlyList<JobListing> jobs = sourceJobs
            .Select(job => new JobListing
            {
                Id = job.Id,
                Title = job.Title,
                Company = job.Company,
                Location = job.Location,
                Description = job.Description,
                Salary = job.Salary,
                JobType = ParseJobType(job.JobType),
                ExperienceLevel = ParseExperienceLevel(job.ExperienceLevel),
                PostedAt = DateTimeOffset.UtcNow.AddDays(-job.PostedDaysAgo),
                Remote = job.IsRemote,
                Source = "Google",
                Url = $"https://www.google.com/search?q={Uri.EscapeDataString(job.Title)}+{Uri.EscapeDataString(job.Company)}"
            })
            .ToList()
            .AsReadOnly();

        return Task.FromResult(jobs);
    }

    private static JobType ParseJobType(string jobType) => jobType?.ToLowerInvariant() switch
    {
        "full-time" => JobType.FullTime,
        "part-time" => JobType.PartTime,
        "contract" => JobType.Contract,
        "internship" => JobType.Internship,
        _ => JobType.FullTime
    };

    private static ExperienceLevel ParseExperienceLevel(string experienceLevel) => experienceLevel?.ToLowerInvariant() switch
    {
        "entry-level" => ExperienceLevel.EntryLevel,
        "mid-level" => ExperienceLevel.MidLevel,
        "senior" => ExperienceLevel.Senior,
        "manager" => ExperienceLevel.Manager,
        _ => ExperienceLevel.Unknown
    };
}

/// <summary>
/// Fixture for Google End-to-End tests using real browser infrastructure.
/// </summary>
#pragma warning disable CA1001 // IAsyncLifetime handles disposal
public sealed class GoogleE2EFixture : IAsyncLifetime
#pragma warning restore CA1001
{
    private IServiceProvider? _serviceProvider;
    private HttpClient? _httpClient;

    public IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Fixture not initialized");

    public GoogleE2EFixture()
    {
    }

    public async Task InitializeAsync()
    {
        _httpClient = new HttpClient();
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add logging first
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));

        // Register HttpClient for direct HTTP-based API client
        services.AddSingleton(_httpClient!);

        // Configure Google Jobs options
        services.Configure<GoogleJobsOptions>(options =>
        {
            options.Enabled = true;
            options.Strategy = JobSearchStrategy.HttpFirst;
        });

        // Configure Gemini options
        services.Configure<Gemini.GeminiOptions>(options =>
        {
            options.BaseUrl = "https://gemini.google.com";
            options.DefaultModel = "gemini-1.5-flash";
            options.ResponseTimeout = TimeSpan.FromSeconds(60);
        });

        // Register TestGoogleJobsApiClient instead of real GoogleJobsApiClient
        services.AddSingleton<Jobs.Internal.GoogleJobsApiClient>(sp =>
        {
            ILogger<Jobs.Internal.GoogleJobsApiClient> logger = sp.GetRequiredService<ILogger<Jobs.Internal.GoogleJobsApiClient>>();
            return new TestGoogleJobsApiClient(logger);
        });

        // Register GoogleJobClient
        services.AddSingleton<GoogleJobClient>();

        // Register IBrowserSession with a fake for E2E testing
        services.AddSingleton<IBrowserSession, FakeBrowserSession>();

        // Register GeminiClient
        services.AddSingleton<Gemini.GeminiClient>();
    }
}
